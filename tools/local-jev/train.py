"""Reproducible bounded fine-tuning. Select checkpoints on validation, test only afterward."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import random
import time
import numpy as np
import torch
from transformers import AutoTokenizer, AutoModelForSequenceClassification
from data import LABELS, MODEL, MODEL_REVISION, blocked, generate, text, unpack
from model import MAX_TOKENS, sync


def read(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def evaluate(model, encoded, rows, device):
    model.eval(); all_logits = []
    with torch.inference_mode():
        for start in range(0, len(rows), 32):
            all_logits.extend(model(**{k: v[start:start+32].to(device) for k, v in encoded.items()}).logits.cpu().tolist())
    raw_correct = masked_correct = acceptable = unsafe = unavailable = guarded_correct = 0
    confusion, predictions = Counter(), []
    for row, values in zip(rows, all_logits):
        raw = LABELS[int(np.argmax(values))]
        choices = unpack(row['payload'])[-1]
        selected = max(choices, key=lambda k: values[LABELS.index(k)])
        gated = 'wait' if blocked(row['payload']) else selected
        raw_correct += raw == row['label']; masked_correct += selected == row['label']
        acceptable += selected in row['acceptable']; guarded_correct += gated == row['label']
        unavailable += raw not in choices
        unsafe += blocked(row['payload']) and selected != 'wait'
        confusion[f'{row["label"]}->{selected}'] += 1
        predictions.append(dict(id=row['id'], expected=row['label'], raw=raw, masked=selected, guarded=gated))
    return dict(n=len(rows), raw_accuracy=raw_correct/len(rows), masked_accuracy=masked_correct/len(rows),
                acceptable_accuracy=acceptable/len(rows), guarded_accuracy=guarded_correct/len(rows),
                raw_unoffered=unavailable, model_gate_violations=unsafe, confusion=dict(confusion), predictions=predictions)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--epochs', type=int, default=6)
    parser.add_argument('--max-seconds', type=float, default=1200)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    random.seed(20261025); np.random.seed(20261025); torch.manual_seed(20261025); torch.set_num_threads(4)
    manifest = generate(args.output/'data')
    device = 'mps' if torch.backends.mps.is_available() else 'cpu'
    tokenizer = AutoTokenizer.from_pretrained(MODEL, revision=MODEL_REVISION)
    model = AutoModelForSequenceClassification.from_pretrained(MODEL, revision=MODEL_REVISION, num_labels=5,
                id2label=dict(enumerate(LABELS)), label2id={k:i for i,k in enumerate(LABELS)}).to(device)
    revision = model.config._commit_hash
    # Never tokenize/evaluate external test rows during model selection.
    rows = {s:read(args.output/'data'/f'{s}.jsonl') for s in ('train','valid')}
    encoded = {s:tokenizer([text(r['payload']) for r in rr], padding=True, return_tensors='pt', truncation=False) for s,rr in rows.items()}
    lengths = {s:v['input_ids'].shape[1] for s,v in encoded.items()}
    if max(lengths.values()) > MAX_TOKENS: raise ValueError(f'Context too long: {lengths}')
    labels = torch.tensor([LABELS.index(r['label']) for r in rows['train']])
    pre = evaluate(model, encoded['valid'], rows['valid'], device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=4e-5, weight_decay=.01)
    best = -1; epochs=[]; start=time.perf_counter(); stopped=False
    for epoch in range(args.epochs):
        model.train(); losses=[]
        for batch in torch.randperm(len(labels)).split(24):
            if time.perf_counter()-start > args.max_seconds: stopped=True; break
            optimizer.zero_grad(set_to_none=True)
            loss=model(**{k:v[batch].to(device) for k,v in encoded['train'].items()},labels=labels[batch].to(device)).loss
            loss.backward(); torch.nn.utils.clip_grad_norm_(model.parameters(),1.0); optimizer.step()
            losses.append(float(loss.detach().cpu()))
        validation=evaluate(model, encoded['valid'], rows['valid'], device)
        record=dict(epoch=epoch+1,loss=float(np.mean(losses)) if losses else None,
                    validation_accuracy=validation['masked_accuracy'], elapsed_seconds=time.perf_counter()-start)
        epochs.append(record); print(json.dumps(record),flush=True)
        (args.output/'training.json').write_text(json.dumps(epochs,indent=2)+'\n')
        if validation['masked_accuracy'] > best:
            best=validation['masked_accuracy']; model.save_pretrained(args.output/'model',safe_serialization=True)
            tokenizer.save_pretrained(args.output/'model')
        if stopped: break
    model=AutoModelForSequenceClassification.from_pretrained(args.output/'model').to(device)
    results={}
    for split in ('valid','test','production'):
        rr=read(args.output/'data'/f'{split}.jsonl')
        ee=tokenizer([text(r['payload']) for r in rr],padding=True,return_tensors='pt',truncation=False)
        if ee['input_ids'].shape[1] > MAX_TOKENS: raise ValueError('Holdout exceeds context')
        results[split]=evaluate(model,ee,rr,device)
    weights=args.output/'model/model.safetensors'
    report=dict(schema=1,checkpoint='76d7cda',model=MODEL,base_revision=revision,device=device,
                parameters=sum(p.numel() for p in model.parameters()),weight_bytes=weights.stat().st_size,
                weights_sha256=hashlib.sha256(weights.read_bytes()).hexdigest(),
                training_seconds=time.perf_counter()-start,epochs=epochs,stopped_by_cap=stopped,
                untrained_head_validation=pre['masked_accuracy'],input_tokens=lengths,dataset=manifest,results=results,
                caveats=['Synthetic authored policy labels, not validated participant helpfulness.',
                         'Only navigation_action; no phrase-choice, interruption or encouragement-summary replacement.',
                         'Mandatory Unity playback revalidation must remain in place; no study runtime changed.'])
    (args.output/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({s:{k:v for k,v in r.items() if k not in ('predictions','confusion')} for s,r in results.items()},indent=2),flush=True)


if __name__ == '__main__': main()
