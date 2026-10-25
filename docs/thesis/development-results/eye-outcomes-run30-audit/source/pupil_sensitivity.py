import sys,json
from dataclasses import replace
from pathlib import Path
sys.path.insert(0,'analysis')
from eye_outcomes import *
p=Path('/Users/pedro.poveda/Documents/Trial Review Live/data/EyeMeasurements/20261023T231821_7a137d30122c4d70a133b5b60e3a6453/gaze_observations.jsonl');rows,_=read_recording(p)
output=[]
for name,c in [('strict_current',Config()),('jitter_100pct_only',replace(Config(),pupil_max_p95_relative_jitter=1.)),('gap_50ms_only',replace(Config(),max_gap_s=.05)),('both_experimental',replace(Config(),max_gap_s=.05,pupil_max_p95_relative_jitter=1.))]:
 m,_=analyze(rows,c)
 for x in m:
  output.append(dict(policy=name,trial_id=x['trial_id'],eye=x['eye'],retained_seconds=x['pupil_eligible_seconds'],ipa=x['ipa_candidate_hz'],participant_policy=False))
 print(name, 'seconds left',sum(x['pupil_eligible_seconds'] for x in m if x['eye']=='left'))
Path('/tmp/eye-outcomes-run30-audited/pupil-screen-sensitivity.json').write_text(json.dumps(output,indent=2))
