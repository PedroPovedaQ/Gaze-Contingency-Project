"""Transfer the v4 encoder to nine assistance choices; freeze selection before evaluation."""
import argparse
from collections import Counter
import hashlib
import importlib.metadata
import json
from pathlib import Path
import random
import shutil
import time
import numpy as np
import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer, get_cosine_schedule_with_warmup
import adaptive_data as data
from model import MAX_TOKENS


def encode(tokenizer, rows):
    result=tokenizer([data.text(r['payload']) for r in rows],padding=True,return_tensors='pt',truncation=False)
    if result['input_ids'].shape[1]>MAX_TOKENS: raise ValueError('Refusing context truncation')
    return result


def evaluate(model, encoded, rows, device):
    model.eval(); values=[]
    with torch.inference_mode():
        for start in range(0,len(rows),32):
            values.extend(model(**{k:v[start:start+32].to(device) for k,v in encoded.items()}).logits.cpu().tolist())
    results=[]; confusion=Counter()
    for row,logits in zip(rows,values):
        choices=data.unpack(row['payload'])[-1]
        raw=data.LABELS[int(np.argmax(logits))]
        masked=max(choices,key=lambda k:logits[data.LABELS.index(k)])
        gated='observe_1s' if data.blocked(row['payload']) else masked
        results.append(dict(id=row['id'],expected=row['label'],raw=raw,masked=masked,gated=gated))
        confusion[row['label']+'->'+masked]+=1
    return dict(n=len(rows),**{kind+'_accuracy':sum(r[kind]==r['expected'] for r in results)/len(rows) for kind in ['raw','masked','gated']},
                model_gate_violations=sum(data.blocked(row['payload']) and not r['masked'].startswith('observe_') for row,r in zip(rows,results)),
                confusion=dict(confusion),predictions=results)


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--parent',type=Path,required=True); parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--epochs',type=int,default=8); parser.add_argument('--max-seconds',type=float,default=1200)
    parser.add_argument('--head-learning-rate',type=float,default=2e-5)
    args=parser.parse_args()
    if args.epochs<1 or args.max_seconds<=0 or args.head_learning_rate<=0: raise ValueError('Positive bounds required')
    args.output.mkdir(parents=True,exist_ok=False)
    seed=26102604; random.seed(seed); np.random.seed(seed); torch.manual_seed(seed); torch.set_num_threads(4)
    manifest=data.generate(args.output/'data')
    device='mps' if torch.backends.mps.is_available() else 'cpu'
    tokenizer=AutoTokenizer.from_pretrained(args.parent/'model',local_files_only=True)
    model=AutoModelForSequenceClassification.from_pretrained(args.parent/'model',local_files_only=True)
    old=model.classifier
    # Keep the fully fine-tuned encoder and transfer corresponding head rows; all weights train.
    mapping=data.LABELS if set(model.config.label2id)==set(data.LABELS) else ['wait','wait','wait','orient','orient','refine','refine','correct','encourage']
    head=torch.nn.Linear(old.in_features,len(data.LABELS))
    with torch.no_grad():
        for i,label in enumerate(mapping):
            j=model.config.label2id[label]; head.weight[i].copy_(old.weight[j]); head.bias[i].copy_(old.bias[j])
    model.classifier=head; model.num_labels=len(data.LABELS); model.config.num_labels=len(data.LABELS)
    model.config.id2label=dict(enumerate(data.LABELS)); model.config.label2id={v:i for i,v in enumerate(data.LABELS)}
    model.config.local_jev_feature_version=data.FEATURE_VERSION; model.config.local_jev_policy=data.POLICY
    model=model.to(device)
    provenance=dict(parent=str(args.parent),parent_sha256=hashlib.sha256((args.parent/'model/model.safetensors').read_bytes()).hexdigest(),
                    seed=seed,learning_rate=2e-5,head_learning_rate=args.head_learning_rate,batch_size=24,epochs=args.epochs,max_seconds=args.max_seconds,
                    initialization='v4 encoder and corresponding five-class head rows expanded to nine; full fine-tune',
                    selection='validation masked accuracy only, first tie retained',
                    packages={k:importlib.metadata.version(k) for k in ['torch','transformers','numpy','safetensors']},
                    code_sha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in Path(__file__).parent.glob('*.py')})
    (args.output/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
    (args.output/'code').mkdir()
    for path in Path(__file__).parent.glob('*.py'): shutil.copy2(path,args.output/'code'/path.name)
    shutil.copy2(data.SOURCE,args.output/'code'/data.SOURCE.name)
    rows={s:[json.loads(l) for l in (args.output/'data'/f'{s}.jsonl').read_text().splitlines()] for s in ['train','valid']}
    encoded={s:encode(tokenizer,r) for s,r in rows.items()}
    y=torch.tensor([data.LABELS.index(r['label']) for r in rows['train']])
    initial=evaluate(model,encoded['valid'],rows['valid'],device)
    optimizer=torch.optim.AdamW([{'params':[p for n,p in model.named_parameters() if not n.startswith('classifier.')],'lr':2e-5},
                                {'params':model.classifier.parameters(),'lr':args.head_learning_rate}],weight_decay=.01)
    steps=args.epochs*((len(y)+23)//24); scheduler=get_cosine_schedule_with_warmup(optimizer,int(steps*.06),steps)
    best=initial['masked_accuracy']; best_epoch=0
    model.save_pretrained(args.output/'model',safe_serialization=True); tokenizer.save_pretrained(args.output/'model')
    start=time.perf_counter(); epochs=[]; stopped=False
    print(json.dumps(dict(status='training',device=device,tokens={s:v['input_ids'].shape[1] for s,v in encoded.items()},initial_validation=best)),flush=True)
    for epoch in range(args.epochs):
        model.train(); losses=[]
        for batch in torch.randperm(len(y)).split(24):
            if time.perf_counter()-start>args.max_seconds: stopped=True; break
            optimizer.zero_grad(set_to_none=True)
            loss=model(**{k:v[batch].to(device) for k,v in encoded['train'].items()},labels=y[batch].to(device)).loss
            loss.backward(); torch.nn.utils.clip_grad_norm_(model.parameters(),1); optimizer.step(); scheduler.step(); losses.append(float(loss.detach().cpu()))
        validation=evaluate(model,encoded['valid'],rows['valid'],device)
        item=dict(epoch=epoch+1,loss=float(np.mean(losses)) if losses else None,validation=validation['masked_accuracy'],elapsed_seconds=time.perf_counter()-start)
        epochs.append(item); print(json.dumps(item),flush=True)
        (args.output/'training.json').write_text(json.dumps(epochs,indent=2)+'\n')
        if validation['masked_accuracy']>best:
            best=validation['masked_accuracy']; best_epoch=epoch+1; model.save_pretrained(args.output/'model',safe_serialization=True)
        if stopped: break
    (args.output/'selection.json').write_text(json.dumps(dict(epoch=best_epoch,validation_accuracy=best))+'\n')
    del model, optimizer, scheduler
    model=AutoModelForSequenceClassification.from_pretrained(args.output/'model',local_files_only=True).to(device).eval()
    results={}
    for split in ['valid','test']:
        rr=[json.loads(l) for l in (args.output/'data'/f'{split}.jsonl').read_text().splitlines()]
        results[split]=evaluate(model,encode(tokenizer,rr),rr,device)
    report=dict(policy=data.POLICY,features=data.FEATURE_VERSION,dataset=manifest,provenance=provenance,device=device,
                training_seconds=time.perf_counter()-start,epochs=epochs,best_epoch=best_epoch,stopped_by_cap=stopped,
                initial_validation=initial['masked_accuracy'],weights_sha256=hashlib.sha256((args.output/'model/model.safetensors').read_bytes()).hexdigest(),results=results)
    (args.output/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({s:{k:v for k,v in r.items() if k not in ['predictions','confusion']} for s,r in results.items()}),flush=True)


if __name__=='__main__': main()
