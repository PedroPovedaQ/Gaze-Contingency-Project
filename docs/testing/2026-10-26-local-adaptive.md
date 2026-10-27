# Local MiniLM adaptive v2 comparison — October 26, 2026

**Implemented:** a separate local MiniLM classifier now accepts the same nine-choice
`assistance` contract as active Jev v2. It supports observation intervals, brief or
explicit orientation/refinement, verified-departure correction and encouragement.
The older five-action v4 model is preserved. The headset still uses Jev; the local
model is available for identical-request comparisons and read-only live observation.

## What changed

The local model cannot acquire this behavior by changing a system prompt: it is an
encoder classifier. We transferred its v4 weights to a nine-output head and
fine-tuned all weights. It now receives measured recent movement, current target
state separate from historical acknowledgment, prior cue responses, available
strategies and previous trial outcomes. It returns an offered strategy and
uncalibrated softmax probabilities. Unity retains geometry, phrase selection,
identity, freshness and playback authority.

The first adaptive candidate exposed a real generalization gap: despite 85.6%
synthetic test agreement, it waited on both recorded post-confirmation departures.
Its synthetic departure examples mostly had flat movement. We retained that model
and its failed comparison, then independently crossed departure events with
improving, mixed, receding, oscillating, stalled and insufficient trajectories.
The continuation learned correction priority even when the longer trajectory
still showed progress. No rule forcing `correct` was added to inference.

## Training and evidence limits

- Preserved parent chain: five-action v4 → adaptive v1 → adaptive v2. Both parent
  weight hashes were checked after training. All files remain under
  `~/.local/share/follow-my-voice-models/`; no weights or training datasets were uploaded. Comparison requests were sent to Jev.
- Initial adaptive run: eight passes, full fine-tuning at 0.00002, 2,700 training
  and 540 validation rows. It introduced the nine-way head.
- Revised run: four passes from adaptive v1, 0.00002 encoder and 0.0002 head learning
  rates, AdamW, batch 24, 6% warmup/cosine decay, gradient clipping and fixed seeds.
  Selected epoch **2** on validation; epochs 3 and 4 tied and did not replace it.
- Revised train/validation/test: 2,700/540/540 balanced synthetic rows, separate
  seeds, deduplicated feature strings. Test uses different sampling cadences and
  triple guard conflicts. Those families were already inspected during the first
  experiment; this is fresh synthetic-example evaluation, not an independent
  out-of-distribution or participant holdout.
- Labels come from an explicit authored policy approximation, not human ratings or
  Jev outputs. The deterministic reference also passes all six replay expectations;
  its synthetic label accuracy is tautological. No ML advantage over those rules
  has been established.
- The six device regression probes include five recorded snapshots and one
  synthetic progress control. They stayed out of training rows and checkpoint
  selection, but their failures informed corpus design. They are **regression
  evidence**, not untouched final validation.
- The model compresses the same request that Jev reads. Features use four-second
  trend bands, span/gaps, improving fraction, separate older progress, current
  flags and bounded history. Authored 3-degree trend, 75% sustained-progress and
  60-degree far bands are development conventions, not validated human thresholds.
  This is not a controlled architecture-only comparison.

## Results

| Evaluation | Learned/offered-choice masked | After external observation checks |
|---|---:|---:|
| Validation, 540 examples | 97.2% | 100% |
| Fresh synthetic examples, 540 | 99.8% (539/540) | 100% |

Raw and offered-choice-masked accuracy were identical. The one fresh-test mismatch
selected a longer observation interval; the external guard returned `observe_1s`.
Validation still included **14 speech choices under closed guards**. Those checks
remain necessary; high synthetic accuracy does not establish a safe standalone
replacement for Unity's checks.

| Paired comparison | Local MiniLM | Jev v2 |
|---|---:|---:|
| Six device regression cases, each repeated twice | 12/12 expected | 12/12 expected |
| Nine synthetic cases, each repeated twice | 18/18 authored expectations | 10/18 authored expectations |
| Median HTTP response | 30.0 ms | 181.7 ms |
| p95 HTTP response | 43.9 ms | 376.0 ms |
| HTTP/deadline failures | 0/30 | 0/30 |

Both corrected the recorded departures and observed during the healthy-progress
control. The local model's synthetic advantage measures fit to its authored
supervision; Jev's differing assistance choices are not independently established
participant-facing errors. Thirty requests per model represent **15 unique cases**,
not thirty participants or thirty independent quality examples.

The paired test alternated request order after training completed, with identical
JSON and a 1.5-second decision budget. All six replay payloads were checked against
compiled production C# output. Local timings include CPU inference and warmed
loopback HTTP; Jev includes Mac-to-cloud network overhead and its cold first
request. About sixfold lower median time here is **not** a measured headset FPS,
Wi-Fi or acoustic-onset improvement. Cold loading and warm direct calls are
separately retained in the JSON results. Benchmark servers shut down afterward.

## Using the comparison

Leave the headset on **Adaptive**, using either voice. The observer described in
[the local model README](../../tools/local-jev/README.md#adaptive-v2-comparison-october-26)
compares MiniLM with the exact Jev v2 requests mirrored by Trial Viewer Live. It
cannot speak, change settings or start trials. It records raw/masked/guarded
choices and probabilities separately. `deterministic_guard` results are not model
confidence. The older checkpoint continues to support its legacy contract only.

The new model is at:
`~/.local/share/follow-my-voice-models/20261026-minilm-adaptive-v2/model`.

[Full results and provenance](2026-10-26-local-adaptive-results.json) include hashes,
training settings, confusion counts, per-case decisions/probabilities and timing.
Full source snapshots, datasets and per-example predictions remain beside the
saved checkpoint. [Implementation notes](../../work_log/2026-10-26-local-adaptive-model.md)
record the initial failure and correction.

**Verification:** the saved `Policy.predict` matched batched evaluation on all 540
fresh synthetic cases; probabilities were finite/normalized, both parent weights
were unchanged and legacy v4 inference still worked. 30 Python tests, production
C# spatial/adaptive checks and exact
payload parity checks pass. Android Build And Run passed in 38.0 seconds, installed
and launched over Wi-Fi. No local-model provider was installed into the headset.
**Open decision:** independent sequence labels and wearer trials are needed before
choosing a local policy for active study guidance.
