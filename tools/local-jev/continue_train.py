"""Continue saved weights; validation-only selection, then compare frozen models."""
import argparse
import hashlib
import json
from pathlib import Path
import random
import shutil
import time
import numpy as np
import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer, get_cosine_schedule_with_warmup
from continuation_data import FEATURE_VERSION, generate_continuation, model_text
from data import LABELS
from model import MAX_TOKENS
from train import evaluate, read


def encode(tokenizer, rows, version):
    result = tokenizer([model_text(row['payload'], version) for row in rows], padding=True, return_tensors='pt', truncation=False)
    if result['input_ids'].shape[1] > MAX_TOKENS:
        raise ValueError('Input exceeds supported context')
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--parent', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--epochs', type=int, default=8)
    parser.add_argument('--max-seconds', type=float, default=1200)
    args = parser.parse_args()
    if args.epochs < 1 or args.max_seconds <= 0: raise ValueError('Positive training bounds required')
    parent_weights = args.parent/'model/model.safetensors'
    if not parent_weights.is_file(): raise ValueError('Saved parent checkpoint required')
    args.output.mkdir(parents=True, exist_ok=False)
    random.seed(202610251); np.random.seed(202610251); torch.manual_seed(202610251); torch.set_num_threads(4)
    manifest = generate_continuation(args.output/'data')
    # Previously inspected tests are explicitly regression evidence, never new holdouts.
    shutil.copy2(args.parent/'data/test.jsonl', args.output/'data/regression.jsonl')
    shutil.copy2(args.parent/'data/production.jsonl', args.output/'data/production.jsonl')
    provenance = dict(parent=str(args.parent), parent_weights_sha256=hashlib.sha256(parent_weights.read_bytes()).hexdigest(),
                      feature_version=FEATURE_VERSION, epochs=args.epochs, max_seconds=args.max_seconds,
                      seed=202610251, optimizer='AdamW', learning_rate=2e-5, weight_decay=.01, batch_size=24,
                      schedule='6% warmup then cosine decay', selection='highest validation masked accuracy; first tie retained',
                      code_sha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(Path(__file__).parent.glob('*.py'))})
    (args.output/'provenance.json').write_text(json.dumps(provenance, indent=2)+'\n')
    device = 'mps' if torch.backends.mps.is_available() else 'cpu'
    tokenizer = AutoTokenizer.from_pretrained(args.parent/'model', local_files_only=True)
    model = AutoModelForSequenceClassification.from_pretrained(args.parent/'model', local_files_only=True).to(device)
    model.config.local_jev_feature_version = FEATURE_VERSION
    rows = {s:read(args.output/'data'/f'{s}.jsonl') for s in ('train', 'valid')}
    encoded = {s:encode(tokenizer, rr, FEATURE_VERSION) for s, rr in rows.items()}
    labels = torch.tensor([LABELS.index(row['label']) for row in rows['train']])
    optimizer = torch.optim.AdamW(model.parameters(), lr=2e-5, weight_decay=.01)
    steps = args.epochs * ((len(labels)+23)//24)
    scheduler = get_cosine_schedule_with_warmup(optimizer, int(steps*.06), steps)
    initial = evaluate(model, encoded['valid'], rows['valid'], device)
    best = initial['masked_accuracy']; best_epoch = 0
    model.save_pretrained(args.output/'model', safe_serialization=True); tokenizer.save_pretrained(args.output/'model')
    start = time.perf_counter(); epochs = []; stopped = False
    print(json.dumps({'status':'training', 'device':device, 'tokens':{s:v['input_ids'].shape[1] for s,v in encoded.items()}, 'initial_validation':best}), flush=True)
    for epoch in range(args.epochs):
        model.train(); losses = []
        for batch in torch.randperm(len(labels)).split(24):
            if time.perf_counter()-start > args.max_seconds: stopped=True; break
            optimizer.zero_grad(set_to_none=True)
            loss = model(**{k:v[batch].to(device) for k,v in encoded['train'].items()}, labels=labels[batch].to(device)).loss
            loss.backward(); torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step(); scheduler.step(); losses.append(float(loss.detach().cpu()))
        validation = evaluate(model, encoded['valid'], rows['valid'], device)
        record = dict(epoch=epoch+1, loss=float(np.mean(losses)) if losses else None,
                      validation_accuracy=validation['masked_accuracy'], validation_gate_violations=validation['model_gate_violations'],
                      elapsed_seconds=time.perf_counter()-start)
        epochs.append(record); print(json.dumps(record), flush=True)
        (args.output/'training.json').write_text(json.dumps(epochs, indent=2)+'\n')
        if validation['masked_accuracy'] > best:
            best = validation['masked_accuracy']; best_epoch = epoch+1
            model.save_pretrained(args.output/'model', safe_serialization=True)
        if stopped: break
    # Freeze selection before loading any held-out/regression evaluation examples.
    (args.output/'selection.json').write_text(json.dumps({'epoch':best_epoch, 'validation_accuracy':best})+'\n')
    del optimizer, scheduler, model
    results = {}
    for name, path in [('previous', args.parent/'model'), ('continued', args.output/'model')]:
        model = AutoModelForSequenceClassification.from_pretrained(path, local_files_only=True).to(device).eval()
        version = getattr(model.config, 'local_jev_feature_version', None)
        results[name] = {}
        for split in ('valid', 'fresh', 'regression', 'production'):
            rr = read(args.output/'data'/f'{split}.jsonl')
            results[name][split] = evaluate(model, encode(tokenizer, rr, version), rr, device)
        del model
    weights = args.output/'model/model.safetensors'
    report = dict(schema=2, device=device, dataset=manifest, provenance=provenance, epochs=epochs,
                  best_epoch=best_epoch, stopped_by_cap=stopped, elapsed_seconds=time.perf_counter()-start,
                  weights_sha256=hashlib.sha256(weights.read_bytes()).hexdigest(), weight_bytes=weights.stat().st_size,
                  results=results, caveat='Authored synthetic policy agreement, not participant helpfulness; old tests are regression checks.')
    (args.output/'report.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps({name:{s:{k:v for k,v in rr.items() if k not in ('predictions','confusion')} for s,rr in sets.items()} for name,sets in results.items()}, indent=2), flush=True)


if __name__ == '__main__': main()
