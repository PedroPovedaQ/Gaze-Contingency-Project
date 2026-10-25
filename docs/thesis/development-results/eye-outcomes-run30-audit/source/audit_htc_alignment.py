import sys,json,collections,hashlib
import numpy as np
from pathlib import Path
sys.path.insert(0,'analysis')
from eye_quality import read_recording
from eye_outcomes import vec
p=Path('/Users/pedro.poveda/Documents/Trial Review Live/data/EyeMeasurements/20261023T231821_7a137d30122c4d70a133b5b60e3a6453/gaze_observations.jsonl');rows,_=read_recording(p)
groups=collections.defaultdict(list);lastkey=None;start=0;seen=set()
for r in rows:
 key=(r.get('validationId'),r.get('validationSet'),r.get('targetId')) if r.get('validationTargetPresent') else None
 t=r.get('htcQueryReceiptTime',0)
 if key!=lastkey:start=t
 lastkey=key
 if key is None or not t or t-start<.25 or t in seen:continue
 seen.add(t)
 if not all(r.get(f) for f in ('valid','focused','deviceAvailable','htcGazeAvailable')) or abs(r['receiptTime']-t)>.025:continue
 for side in ('left','right'):
  if not r.get(side+'Valid'):continue
  d=vec(r,side+'WorldDirection')
  if d is None:continue
  target=np.array([r['validationTarget'][a]-r[side+'WorldOrigin'][a] for a in 'xyz'])
  if np.linalg.norm(target)==0:continue
  error=np.degrees(np.arccos(np.clip(np.dot(target,d)/(np.linalg.norm(target)*np.linalg.norm(d)),-1,1)))
  groups[(*key,side)].append(float(error))
out=dict(source_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),method='Per-eye HTC world ray to recorded target; query deduplication; 250 ms settling; valid focused queries within 25 ms; quick alignment targets only, not held-out validation',validated=False,results=[dict(validation_id=k[0],set=k[1],target=k[2],eye=k[3],queries=len(v),median_error_deg=float(np.median(v)),p90_error_deg=float(np.percentile(v,90))) for k,v in groups.items()])
Path('/tmp/eye-outcomes-run30-audited/htc-alignment-audit.json').write_text(json.dumps(out,indent=2))
print(json.dumps(out,indent=2))
