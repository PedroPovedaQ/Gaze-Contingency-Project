## Current two-stage provider — October 26, 2026

**Implemented:** `20261026-minilm-reassurance-v8` serves action → phrase decisions. Policy `jev_adaptive_assistance_v4` has seven actions; `observe_2s` and `observe_4s` are no longer offered or accepted. Observation ends after stage one. Speaking actions invoke a separate MiniLM candidate ranker through `questions.assistance_phrase`, on the same service/provider. Unity does not choose the winning phrase.

`behavior.phrase_history` carries actual current-round playback counts and last-start ages, including canonical and rendered text. It survives pause/resume, clears on a new round/participant, and excludes proposals or playback cancelled before onset. Both providers receive it. The ranker uses candidate-specific repeat features; Jev receives the exact counts and an explicit repetition-discouraging instruction. Repetition remains possible when necessary, especially singleton correction menus.

- `phrase_data.py`: strict second-stage contract and versioned state/candidate features; synthetic preference generator.
- `train_phrase.py`: full MiniLM fine-tuning with a scalar scoring head and listwise loss; validation checkpoint selection.
- `phrase_model.py`: learned candidate scores and uncalibrated offered-choice softmax. No runtime teacher or lexical fallback.
- `serve.py`: loads both checkpoints once; health advertises both stages; each response records the actual stage weight hash.
- `headset.py`: pins this bundle and tunnels the Mac CPU service over authorized ADB.

Training still uses **authored synthetic supervision**, not Jev distillation, expert labels, or participant outcomes. The phrase teacher discourages exact repeats, then prefers concision or detail according to the selected action and prior cues. Test agreement establishes imitation only. Live model selection is advisory until Unity rechecks spatial truth, tracking, trial identity, prepared audio and request freshness. No automatic cloud or rules fallback replaces a failed phrase decision.

Current reassurance policy, training iterations and verification receipts: [progress reassurance](../../work_log/2026-10-26-progress-reassurance.md). Pending or interrupted responses to earlier cues are included in synthetic training; they do not automatically suppress reassurance during fresh measured progress.

Earlier experiments below are historical; their action counts, policies, checkpoint names and evaluation results must not be attributed to the current bundle.

# Local Jev policy experiments

## Historical single-stage headset comparison (October 26)

**Implemented:** Local MiniLM is the default for new study and quick-test sessions. Headset Settings has a **Model: Local MiniLM / Jev** button; Jev is opt-in. Adaptive/Baseline remains a separate development control. `python3 tools/local-jev/headset.py` starts/reuses the adaptive-v4 checkpoint and connects headset loopback through USB or Wi-Fi ADB. Keep the Mac awake. See [headset comparison and verification](../../docs/testing/2026-10-26-headset-model-toggle.md) for scope, reconnecting, telemetry and failure behavior. Local mode is now active assistance; the historical shadow workflow below remains read-only.


Fine-tunes a pretrained MiniLM encoder for bounded assistance choices. The new
adaptive checkpoint has nine strategies; the preserved v4 checkpoint has five
legacy navigation actions. These are policy classifiers, not Jev weights or a
general language model. Study sessions and development quick tests default to MiniLM; both can
explicitly select Jev while paused. Both can also be compared on recorded requests.

Latest results: [elapsed-silence fix and MiniLM default](../../work_log/2026-10-26-local-hint-silence.md).
Earlier results: [adaptive v2 comparison](../../docs/testing/2026-10-26-local-adaptive.md).
Earlier results: [five-action continuation](../../docs/testing/2026-10-25-local-jev-continuation.md).


## Adaptive v2 comparison (October 26)

**Implemented:** a separate MiniLM checkpoint now uses the active Jev `assistance`
contract, with nine classes: `observe_1s`, `observe_2s`, `observe_4s`,
`orient_brief`, `orient_explicit`, `refine_brief`, `refine_explicit`, `correct`,
`encourage`. The older v4 checkpoint still accepts only `navigation_action`.
This is a classifier with uncalibrated probabilities, not a general LLM.

`train_adaptive.py` transfers the v4 encoder and corresponding classifier rows,
expands the head to nine outputs and fine-tunes all weights. Supervision is an
explicit authored approximation of Jev v2's instructions: prioritize recent
movement, help unresolved stalls, distinguish historical confirmation from current
target acquisition and correct a verified departure. Jev responses are not labels.
The deterministic reference is also reported; matching these synthetic labels does
not establish an ML advantage over those rules or participant helpfulness.

Input features summarize the latest four seconds (reduction, span, largest gap,
improving fraction), keep older net progress separate, and include current gates,
available capabilities, cue history and previous outcomes. The trend bands match
Unity's 3-degree/four-second trend helper. Additional authored training conventions
include a 75% improving-step sustained-progress band and a 60-degree far band;
these are development policy choices, not validated human thresholds. The text
encoder gets measured summaries, not a computed final strategy, participant identity,
Jev's answer or future behavior. It consumes the same request as Jev but compresses
that request; it is not an architecture-controlled comparison.

Train/validation/test have 2,700/540/540 balanced synthetic rows with distinct seeds
and deduplicated feature strings. Test uses different sample cadences and triple
guard conflicts. This is limited structural holdout, not independent participant
data. The six recorded/synthetic device replay probes are excluded from training
and selection and are regression evidence. Checkpoint selection uses validation
only, freezes before test evaluation and preserves the parent checkpoint. The
first adaptive candidate missed recorded departures despite strong synthetic
departure scores. The revised corpus independently crosses departure events with
all trajectory patterns; the unchanged device probes are regression checks after
that correction. They are not untouched final holdouts.

```sh
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/train_adaptive.py \
  --parent ~/.local/share/follow-my-voice-models/20261025-minilm-action-v4 \
  --output /absolute/path/to/new-adaptive-run
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/compare_adaptive.py \
  --run ~/.local/share/follow-my-voice-models/20261026-minilm-adaptive-v2 \
  --output /absolute/path/to/new-comparison --live-jev
```

The saved v2 candidate continues the preserved adaptive v1 checkpoint for four
passes, using a 0.00002 encoder learning rate and a 0.0002 head learning rate:

```sh
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/train_adaptive.py \
  --parent ~/.local/share/follow-my-voice-models/20261026-minilm-adaptive-v1 \
  --output /absolute/path/to/new-adaptive-continuation \
  --epochs 4 --head-learning-rate 0.0002 --max-seconds 600
```

The comparison uses the exact production C# strategy descriptions and instructions
and identical JSON inputs for both models. It sends at most 30 Jev requests: two
repeats of six device regression probes and the first held-out test example of each
class. Raw, offered-choice-masked and externally guarded decisions are separate.
HTTP timings compare a warmed CPU loopback service with Mac-to-cloud Jev, including
network overhead. They do not measure headset Wi-Fi, FPS or acoustic onset. Loading
and cold/warm direct calls are reported separately. Servers close on completion.

For live comparison, keep Trial Viewer **Live** mirroring recordings and the
headset on **Adaptive**. The observer below handles v2 `assistance` requests;
v1 requests and unrelated question types are counted as unsupported. It never
controls the headset. Do not change the device to Baseline for this comparison.

```sh
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/shadow.py \
  --model ~/.local/share/follow-my-voice-models/20261026-minilm-adaptive-v2/model \
  --source "$HOME/Documents/Trial Review Live/data" \
  --output "$HOME/.local/share/follow-my-voice-models/shadow-adaptive-v2-20261026"
```

`summary.json` reports agreement and latency; `decisions.json` retains individual
choices/probabilities and exact request evidence. Gates returning a one-hot silence
answer are labeled `deterministic_guard`, not model confidence. Model/adapter
fingerprints prevent mixing outputs across implementations. Use a new output path
for old observer folders after this update. Create `stop-request` in the observer's
output folder to stop it. All measurements and model files remain outside Git.

To inspect the loopback endpoint, use `serve.py --model` with the new model path;
`/health` reports `scope: assistance`. The old checkpoint reports
`scope: navigation_action`. Neither supports all Jev question types. Unity's
freshness, identity, geometry, prepared-clip and final playback checks must remain
in the active local-provider integration described above.

## Legacy five-action shadow comparison

`shadow.py` observes the existing Trial Viewer live archive on the Mac. Jev still
controls guidance. The observer has no ADB, cloud client, command writer or audio
output: it runs the saved classifier on the **exact recorded request**, then joins
Jev's response by recording path and request ID. Replay transfer may delay this
comparison; it is an asynchronous shadow of recorded inputs, not a simultaneous
headset inference benchmark.

1. Connect Trial Viewer **Live** so the receiver is mirroring recordings.
2. Run the command below. Existing mirrored bytes are skipped on a new live run;
   new files and appended records are consumed. Newly downloaded archive backlog
   can also arrive after startup, so inspect recording IDs before treating data as
   a new session. Use a separate output plus `--from-start --once` for backfill.
3. This older checkpoint supports archived `navigation_action` requests only.
   The current Adaptive runtime emits `assistance`; use the new checkpoint above
   for new runs. The observer does not switch guidance modes or start trials.
4. Inspect `summary.json` and `decisions.json` in the output folder.

```sh
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/shadow.py \
  --model ~/.local/share/follow-my-voice-models/20261025-minilm-action-v4/model \
  --source "$HOME/Documents/Trial Review Live/data" \
  --output "$HOME/.local/share/follow-my-voice-models/shadow-live-20261025"
```

The default CPU backend avoids competing with the desktop viewer's GPU. The
summary separates raw, offered-action-masked and rule-gated agreement, failures,
pending/orphan responses, discarded requests and observed playback. Decisions
retain the original request for disagreement inspection; no Jev answer or later
trajectory enters inference. **Agreement is not correctness or helpfulness.**
Mac compute time includes tokenization/inference and external gates but excludes
transfer delay; recorded Jev time includes headset/network overhead. First-call
cost is included; these are not paired end-to-end or acoustic timings.

Offsets and decisions commit together in SQLite. Restarting with the same output
resumes; model fingerprint, device or source changes require a new output folder.
Duplicate requests do not rerun inference. Incomplete final lines wait for more
bytes; source truncation stops the observer instead of mixing identities. Local
inference failures are recorded, never substituted into guidance. Output files
are private and must be outside the source archive. No retraining happens here.

Stop with Ctrl-C or create `stop-request` in the output folder. Remove that marker
before restarting. Keep the viewer's Live receiver running for new data. If it
stops or disconnects, shadow results simply stop advancing; they do not certify
that the headset is connected. The runner PID is in the summary.

With the legacy v4 checkpoint, the endpoint supports **only `navigation_action`** with structured
`development-action-v1` state. It rejects the normal joint `navigation_phrase` /
`transmit` request, interruption requests and round summaries. Never substitute
this server for all Jev calls.

## Data and evaluation

- 1,200 balanced synthetic training examples and 240 validation examples use
  cardinal directions. A 320-case test holds out diagonal combinations and
  combined gate failures; it consequently has no `orient` cases.
- Wording is a subset of the existing `DirectionalHintPolicy` library. A test
  compiles the production C# library and checks every generated phrase against
  it. States are simulated and assume audio eligibility; they are not recorded
  participant trajectories or outputs generated by the complete Unity engine.
- Labels express an explicit authored policy. The repeat oracle recognizes only
  the controlled direction vocabulary, including higher/up and lower/down.
  It is not a general semantic-equivalence detector. The input includes a
  three-second silence band, derived from the authored threshold.
- Ten complete production-C# request fixtures from the October 20 development
  pilot are kept out of training/selection. Expected actions are independently
  assigned from case definitions, not copied from stored Jev responses.
- Checkpoint selection uses validation accuracy only. The model is evaluated
  after six epochs on both test sets. Repeated benchmark requests are latency
  samples, not additional independent quality cases.
- Metrics separate raw predictions, offered-action masking and mandatory gates.
  Gating can prevent invalid speech without making the learned model correct.
  Probabilities are uncalibrated. Agreement does not establish helpfulness or
  improve participant task performance.

## Reproduce

Run from the repository root. Weights, datasets and measurements live outside
Git under `~/.local/share/follow-my-voice-models/`.

```sh
uv venv --python /opt/homebrew/bin/python3.11 ~/.local/share/follow-my-voice-models/venv
uv pip install --python ~/.local/share/follow-my-voice-models/venv/bin/python -r tools/local-jev/requirements.txt
~/.local/share/follow-my-voice-models/venv/bin/python -m unittest discover -s tools/local-jev -p 'test_*.py'
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/train.py --output /absolute/path/to/new-run
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/benchmark.py --run /absolute/path/to/new-run --live-jev
```

Training uses MPS when available, otherwise CPU, with fixed seeds and a 20-minute
cap. The base model revision and direct dependency versions are pinned. Accelerator
training need not be bitwise identical across systems. Outputs refuse overwrite.
The first model download requires internet; saved-model inference is local.

## Further fine-tuning

The continuation experiment starts from saved v3 weights rather than reinitializing
the model. It uses 2,400 synthetic training rows and 400 validation rows with all
five actions and both cardinal/diagonal directions. Offered direction-cue wording
families are separated before generation. A fresh 500-case evaluation holds out
additional prepared wording and triple gate failures; it is generated before
training and evaluated only after checkpoint selection is frozen. The old tests
are now explicitly regression evidence. Labels still come from the authored rule
oracle, so fresh wording does not establish independent human agreement.
Validation wording is disjoint from continuation training but was available in
the parent's training pool; only the fresh pool is new across both rounds.

```sh
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/continue_train.py \
  --parent ~/.local/share/follow-my-voice-models/20261025-minilm-action-v3 \
  --output /absolute/path/to/new-continuation
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/compare_local.py \
  --before ~/.local/share/follow-my-voice-models/20261025-minilm-action-v3 \
  --after /absolute/path/to/new-continuation
```

This run combines additional training, broader examples and a shorter input format;
it is not a controlled ablation of extra epochs alone. `compact-v2` inputs retain
each gate individually, normalize direction words and include offered text plus
last actual speech. They do not include the oracle's final action or a computed
gate/repeat decision. The saved model config records its input format; old weights
continue to receive their original representation. The same external gates and
candidate mask still apply. Training uses eight fixed epochs, 0.00002 learning
rate, warmup/cosine decay and validation-only checkpoint selection. It preserves
the parent weights and writes a separate immutable output directory.

`--live-jev` sends at most 20 existing synthetic fixture requests to the current
Jev endpoint, using `jev_key` from the ignored `Assets/StreamingAssets/api_keys.json`.
No key is written to reports. Omit the flag for local/historical comparison only.
The benchmark separates successful latency from failed attempts, preserves each
HTTP result in JSONL, and reports a deterministic authored-rule baseline too.
CPU/MPS direct measurements include tokenization; model loading and first-request
cost are reported separately. Loopback measurements use a warmed persistent server.
Cloud measurements include network overhead and the first request. None measures
headset frame rate, Wi-Fi transport or time to audible speech.

## Inspect the local endpoint

```sh
~/.local/share/follow-my-voice-models/venv/bin/python tools/local-jev/serve.py \
  --model ~/.local/share/follow-my-voice-models/20261025-minilm-action-v4/model \
  --device mps --port 8766
curl http://127.0.0.1:8766/health
```

POST an existing fixture's `payload` to `/typesafe/v1/systemone`. The service binds
only to loopback, limits request bodies and context length, and returns offered
action IDs; it cannot invent audio text or directly play anything. It is an
experimental serial server, not a production deployment. Future integration must
retain Unity's fresh geometry, identity, condition, prepared-audio and playback
revalidation. No server is left running by the benchmark.

Base model: [all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2),
Apache-2.0; GPU backend: [PyTorch MPS](https://docs.pytorch.org/docs/stable/notes/mps.html).
Measurements and the current decision belong in
[`docs/testing/2026-10-25-local-jev.md`](../../docs/testing/2026-10-25-local-jev.md).
