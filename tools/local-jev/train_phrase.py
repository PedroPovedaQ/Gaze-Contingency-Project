"""Train a scalar MiniLM candidate ranker from exported Unity menus and synthetic preferences."""
import argparse
import hashlib
import json
from pathlib import Path
import random
import time
import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer
import phrase_data as data


def main():
    p=argparse.ArgumentParser(); p.add_argument('--parent',type=Path,required=True); p.add_argument('--output',type=Path,required=True)
    p.add_argument('--menus',type=Path,required=True); p.add_argument('--epochs',type=int,default=4)
    a=p.parse_args(); a.output.mkdir(exist_ok=False); (a.output/'data').mkdir()
    torch.set_num_threads(4); torch.manual_seed(26102660); random.seed(26102660)
    seen=set(); menus=json.loads(a.menus.read_text())
    splits={name:data.examples(menus,n,seed,seen) for name,n,seed in [('train',768,26102661),('valid',160,26102662),('test',160,26102663)]}
    for name,rows in splits.items(): (a.output/'data'/f'{name}.jsonl').write_text(''.join(json.dumps(r)+'\n' for r in rows))
    tokenizer=AutoTokenizer.from_pretrained(a.parent,local_files_only=True)
    model=AutoModelForSequenceClassification.from_pretrained(a.parent,local_files_only=True)
    model.classifier=torch.nn.Linear(model.classifier.in_features,1); model.num_labels=1
    model.config.num_labels=1; model.config.id2label={0:'suitability'}; model.config.label2id={'suitability':0}
    model.config.phrase_feature_version=data.VERSION
    device='mps' if torch.backends.mps.is_available() else 'cpu'; model.to(device)
    # All candidates for each state stay in one ranking group.
    def encode(rows):
        texts=[]; groups=[]
        for row in rows:
            f=data.features(row['payload']); groups.append((len(texts),len(f))); texts.extend(f)
        x=tokenizer(texts,padding=True,truncation=False,return_tensors='pt')
        if x['input_ids'].shape[1]>384: raise ValueError('Refusing context truncation')
        return x,groups
    encoded={name:encode(rows) for name,rows in splits.items()}
    def evaluate(rows, enc):
        model.eval(); x,groups=enc; values=[]
        with torch.inference_mode():
            for start in range(0,len(x['input_ids']),32):
                values.extend(model(**{k:v[start:start+32].to(device) for k,v in x.items()}).logits[:,0].cpu().tolist())
        correct=avoid=opportunities=0
        for row,(start,n) in zip(rows,groups):
            winner=max(range(n),key=lambda j:values[start+j]); scores=row['scores']
            correct+=scores[winner]>=max(scores)-1e-6
            _,choices,history,_=data.unpack(row['payload']); counts={h['phrase']:h['count'] for h in history}
            used=[counts.get(t,0)>0 for t in choices]
            if any(used) and not all(used): opportunities+=1; avoid+=not used[winner]
        return dict(n=len(rows),top_choice_agreement=correct/len(rows),repeat_opportunities=opportunities,
                    avoided_repeats=avoid,repeat_avoidance=avoid/opportunities if opportunities else None)
    opt=torch.optim.AdamW(model.parameters(),lr=2e-5,weight_decay=.01)
    best=-1; reports=[]; start=time.perf_counter()
    for epoch in range(a.epochs):
        model.train(); losses=[]; order=torch.randperm(len(splits['train'])).tolist(); x,groups=encoded['train']
        for batch in range(0,len(order),8):
            selected=order[batch:batch+8]; ids=[i for j in selected for i in range(groups[j][0],sum(groups[j]))]
            opt.zero_grad(set_to_none=True)
            logits=model(**{k:v[ids].to(device) for k,v in x.items()}).logits[:,0]
            loss=0; offset=0
            for j in selected:
                n=groups[j][1]; scores=torch.tensor(splits['train'][j]['scores'],device=device)
                target=(scores>=scores.max()-1e-6).float(); target/=target.sum()
                loss-= (target*torch.log_softmax(logits[offset:offset+n],dim=0)).sum(); offset+=n
            loss/=len(selected); loss.backward(); torch.nn.utils.clip_grad_norm_(model.parameters(),1); opt.step(); losses.append(loss.item())
        report=dict(epoch=epoch+1,loss=sum(losses)/len(losses),validation=evaluate(splits['valid'],encoded['valid']))
        reports.append(report); print(json.dumps(report),flush=True)
        if report['validation']['top_choice_agreement']>best:
            best=report['validation']['top_choice_agreement']; model.save_pretrained(a.output/'model'); tokenizer.save_pretrained(a.output/'model')
    model=AutoModelForSequenceClassification.from_pretrained(a.output/'model',local_files_only=True).to(device).eval()
    report=dict(supervision='Authored synthetic preferences, not Jev distillation or human outcomes',features=data.VERSION,
                parent=str(a.parent),parent_sha256=hashlib.sha256((a.parent/'model.safetensors').read_bytes()).hexdigest(),
                menus_sha256=hashlib.sha256(a.menus.read_bytes()).hexdigest(),device=device,epochs=reports,
                training_seconds=time.perf_counter()-start,test=evaluate(splits['test'],encoded['test']))
    (a.output/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report['test']),flush=True)


if __name__=='__main__': main()
