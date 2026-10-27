# Local adaptive model — 2026-10-26

## Scope

Update the local MiniLM to the nine-choice Jev adaptive v2 contract for comparison.
Preserve the v4 five-action checkpoint. Headset guidance stays on active Jev.
No thesis synchronization, commits or pushes.

## Implementation

- `adaptive_data.py`: versioned strict request adapter, measured recent-history summaries, synthetic authored labels, immutable dataset generation. Raw state and recorded Jev choices are never training labels. Six device replay probes stay outside training and checkpoint selection.
- `train_adaptive.py`: transfer v4 encoder and corresponding classifier rows to a nine-way head; fine-tune all weights, select on validation only, freeze before test evaluation. Parent weights and provenance hashes retained.
- `model.py` and `serve.py`: saved feature version selects the contract; explicit offered-choice mask, uncalibrated probabilities, separately labeled deterministic observation guards. V4 remains compatible.
- `compare_adaptive.py`: same JSON requests and deadline for both models, bounded 30 cloud calls, cold/warm direct timing and alternating loopback/cloud timing, full per-case evidence; no acoustic/headset-performance claim.
- `shadow.py`: v2 assistance trace pairing with raw/masked/gated choices and probabilities; old contract still supported by old checkpoint, other policies unsupported. Read-only mirror consumption, no guidance authority. Code plus model fingerprint protects run identity.
- Added a production C# request export. All six Python replay payloads exactly match compiled C# `AdaptivePayload` output.

## Verification to date

29 Python tests and production spatial/adaptive contract checks pass. Android Build And Run passed in 38.0 s; wireless install Success and Android launch Status ok. No Unity runtime provider changed.

Training and comparison results will be recorded in `docs/testing/2026-10-26-local-adaptive.md`.

## Correction informed by the first comparison

The first candidate's six device regression cases matched only 8/12 repeated
expectations: it waited on both target-departure snapshots. Jev matched 12/12.
Synthetic departure examples had mostly flat trajectories, so the data did not
teach departure priority across the recent-progress histories present on device.
Preserved that candidate and its complete comparison. Revised synthetic generation
to independently cross departure events with steady, mixed, receding, oscillating,
stalled and insufficient histories. No recorded state or Jev answer was added to
training. Continued the saved nine-class checkpoint for four validation-selected
passes with a faster head learning rate. Device probes are now explicit regression
evidence after this development feedback, not untouched holdouts. Current source
snapshots, package versions and hashes are retained beside the new checkpoint.

## Final receipt

Selected continuation epoch 2 (97.2% validation); fresh synthetic test 539/540 learned, 540/540 guarded. Saved `Policy.predict` agrees with batched evaluation on all 540 examples, with finite normalized probabilities and zero feature-string overlap across the revised splits. Both parent weight hashes remain unchanged and legacy v4 inference succeeds.

Final paired comparison: device regression local 12/12 and Jev 12/12; selected synthetic authored expectations local 18/18 and Jev 10/18. Median HTTP 29.96 ms local CPU loopback versus 181.72 ms cloud, zero failures in 30 attempts per model. These are development expectations and desktop timing, not independent participant validation. Full raw records and initial failed candidate remain preserved outside Git; compact evidence is in the report linked above.

Started the read-only live reader at `~/.local/share/follow-my-voice-models/shadow-adaptive-v2-20261026`. It skips existing archive bytes and compares new v2 requests when Trial Viewer mirrors them. No endpoint or provider in Unity was changed. Stop using the output folder's `stop-request` marker.
