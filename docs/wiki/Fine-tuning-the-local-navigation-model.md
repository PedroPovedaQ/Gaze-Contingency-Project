# How we fine-tuned the local navigation model

*Evidence and implementation status: October 25, 2026.*

We fine-tuned a small pretrained transformer to choose navigation actions for **Follow My Voice**, our gaze-contingent mixed-reality search experiment. The aim was to reduce the delay of asking the cloud-hosted Jev service for a decision while preserving the app's geometry, timing and playback rules.

**Implemented:** the latest candidate, v4, reached **98.4% agreement with authored labels on 500 fresh synthetic examples**. Applying mandatory checks outside the model raised final action agreement to **100%** on that set. A small alternating local/cloud benchmark measured **15.27 ms median local HTTP latency versus 196.87 ms for Jev**, about 12.9× faster.

**Open decision:** these results do not establish general Jev equivalence or participant helpfulness. Jev still controls headset guidance. The local candidate is being evaluated through a Mac-based shadow observer; no live participant-quality conclusion has been established.

## What we trained

Although we sometimes call this the “local LLM,” it is more precisely a **five-class transformer action classifier**. It does not generate free-form advice, clone a voice, or produce audio.

We started from [`sentence-transformers/all-MiniLM-L6-v2`](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2), a pretrained sentence encoder, and loaded it with Hugging Face's `AutoModelForSequenceClassification` and a five-label classification head. The base model is published under Apache-2.0; see its [model card](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2).

| Property | Our implementation |
|---|---|
| Pinned base revision | `1110a243fdf4706b3f48f1d95db1a4f5529b4d41` |
| Trainable parameters | 22,715,141 |
| Saved weights | 90,872,572 bytes, approximately 90.9 MB |
| Training method | Full fine-tuning of the encoder and classification head |
| Objective | Supervised action classification using the model's classification loss |
| Training hardware | Mac with PyTorch MPS; scripts fall back to CPU |
| Supported request | `navigation_action`, with `development-action-v1` structured state |

We did not fine-tune Jev's weights, use LoRA, train a generative model from scratch, or use Jev responses as training labels.

## The decision the model makes

Unity computes the geometry and supplies an eligible action menu. The model selects an action ID:

| Action | Meaning |
|---|---|
| `wait` | Stay silent |
| `orient` | Give an eligible orientation cue |
| `refine` | Give a more precise eligible direction cue |
| `correct` | Give an eligible corrective-return cue |
| `encourage` | Use an eligible progress-encouragement cue |

Each speech action points to an existing prepared phrase. The model cannot invent a new phrase or turn an arbitrary string into playable audio.

The supported inference path is:

```text
Immutable runtime state + offered actions
                  |
         Versioned text features
                  |
       MiniLM classifier: 5 scores
                  |
   Restrict selection to offered actions
                  |
   Apply mandatory external silence gates
                  |
         Selected action ID
```

A future runtime replacement would still need Unity's final freshness, geometry, voice/condition identity, cancellation and playback checks. The current shadow observer has no playback authority at all.

### Input features

The feature representation includes gaze and geometry validity, target-wall context, yaw/pitch error, qualified gaze/controller events, guidance phase and permitted action, audio busy/speaking state, elapsed silence, actual recent speech and the offered phrases.

Version v4 uses a compact representation, `compact-v2`. It keeps individual gate values, normalizes the controlled direction vocabulary and includes last actual speech. It does not include the expected label, Jev's answer, a precomputed gate verdict, a precomputed repeat verdict, or later trial outcomes.

The measured training/validation maximum input length decreased from 151 to 130 tokens. Requests beyond our supported 384-token limit are rejected instead of silently truncated. The saved model configuration records its feature version, so older checkpoints continue to receive their original representation.

## How we built the training data

**Implemented:** training data is synthetic and labeled by an explicit policy function in `tools/local-jev/data.py`. No participant recordings, voice samples, gaze logs or provider-answer labels were used for training.

The policy encodes intended behavior such as waiting when a required gate fails, avoiding a recently repeated ordinary direction, preferring an offered progress cue after sufficient silence, and allowing a new corrective-return event. That makes the labels reproducible, but it also limits what the evaluation proves: success means learning this authored policy, not independently discovering the most helpful intervention.

Phrase wording comes from the application's `DirectionalHintPolicy` library. Tests compile the actual C# `AllPhrases()` output and check that the generated phrases belong to it. The simulated states assume local phrase eligibility; they are not full trajectories generated by the Unity engine.

### First retained training run: v3

| Split | Size | Purpose |
|---|---:|---|
| Training | 1,200 | Balanced action labels with cardinal directions |
| Validation | 240 | Select the checkpoint |
| Synthetic test | 320 | Unseen diagonal combinations and combined gate failures |
| Production-shaped test | 10 unique cases | Complete requests from the production C# builder |

The first synthetic test contained no `orient` examples, so it did not cover every action class. The ten C# cases came from the October 20 development work, with expected actions assigned from case definitions rather than copied from Jev responses.

Two preliminary runs were stopped before holdout evaluation to correct phrase provenance. Their artifacts and reasons were retained. The first retained model discussed here is v3.

### Continuation training: v4

We continued the saved v3 weights using **2,400 additional training rows** and **400 validation rows**. Examples covered all five actions, cardinal and diagonal directions, repeats, direction changes, corrective returns, the three-second silence boundary, missing evidence and combinations of failed gates.

A **fresh 500-example test**, balanced at 100 examples per action, used cue wording absent from both training rounds and triple gate-failure combinations absent from the continuation training set.

Important distinctions:

- Continuation training, validation and fresh-test cue-wording families were separated before training.
- Some continuation-validation wording had appeared in the parent's training pool. It is not an entirely unseen validation set across the full training history.
- The old 320 examples and ten C# cases became **regression checks** once their results had been inspected. We do not call them new holdouts for v4.
- Fresh-test labels still came from the same policy oracle. This tests generalization across constructed examples, not an independent human judgment or representative participant distribution.

## Training recipe and checkpoint selection

| Setting | Initial v3 | Continued v4 |
|---|---:|---:|
| Maximum epochs | 6 | 8 additional |
| Batch size | 24 | 24 |
| Optimizer | AdamW | AdamW |
| Learning rate | 0.00004 | 0.00002 |
| Weight decay | 0.01 | 0.01 |
| Gradient clipping | 1.0 | 1.0 |
| Learning-rate schedule | Constant | 6% warmup, then cosine decay |
| Seed | 20261025 | 202610251 |
| Training-loop time cap | 20 minutes | 20 minutes |
| Retained epoch | 4 | 7 |
| Measured training/evaluation run time | About 255 seconds | About 379 seconds |

Both scripts select on **validation masked accuracy**, retaining the first checkpoint in a tie. They evaluate held-out data only after selecting the checkpoint. Fixed seeds improve repeatability; accelerator execution is not guaranteed to be bitwise identical.

For v3, the randomly initialized head started at 20.0% validation agreement. Epoch 4 reached 81.7%; epoch 5 tied and epoch 6 declined, so epoch 4 was retained.

For v4, the starting saved weights with the new feature format scored 75.25% on continuation validation. Epoch 7 reached 96.5%; epoch 8 tied, so epoch 7 was retained. Selection was saved before fresh-test evaluation, and the original v3 weight hash remained unchanged.

The improvement combines additional training, broader examples and a new input representation. We have not isolated how much each change contributed.

## What the quality results mean

We report three distinct measurements:

1. **Raw:** the highest-scoring action across all five labels.
2. **Masked:** the highest-scoring action among those actually offered.
3. **Gated:** the masked choice after external code forces `wait` when a mandatory precondition fails.

The gates cover invalid/missing gaze or geometry evidence, closed guidance permission, speaking/busy playback, qualified target/controller events, and missing local qualification. These checks are ordinary code, not evidence of learned competence.

The comparison below evaluates both saved versions on the same continuation datasets:

| Dataset | Cases | v3 masked | v4 masked | v3 gated | v4 gated |
|---|---:|---:|---:|---:|---:|
| Continuation validation | 400 | 76.0% | 96.5% | 85.5% | 100% |
| Fresh wording and combinations | 500 | 77.4% | 98.4% | 86.2% | 100% |
| Earlier synthetic regression | 320 | 68.4% | 96.3% | 77.8% | 100% |
| C# production-shaped regression | 10 | 70.0% | 100% | 70.0% | 100% |

On the fresh set, v4's raw score was also 98.4%, with no unoffered action predictions. Its eight mistakes proposed speech despite closed or missing-evidence gates. External checks corrected those to `wait`. Removing those checks would restore the errors.

Reloading the saved model and exercising the actual `Policy.predict` path reproduced all 500 final expected actions. The model's reported probabilities are **not calibrated confidence estimates**.

## How fast was it compared with Jev?

The v4 cloud comparison alternated local and Jev requests using the same ten C# payloads twice. Local loopback HTTP was warmed; cloud time included network overhead and the first request. Both arms returned 20 successful responses with no HTTP/parse failures.

| Path | Median | p95 | Agreement with expected actions |
|---|---:|---:|---:|
| Local v4, warmed loopback HTTP on MPS | 15.27 ms | 23.28 ms | 20/20 responses; 10/10 cases |
| Current Jev, cloud HTTP | 196.87 ms | 399.46 ms | 16/20 responses; 8/10 cases |

The median difference is about 12.9×, or 92% lower latency for the local path in this small benchmark. Repeated requests are timing samples, not additional independent quality cases.

A separate paired v3/v4 local-only run measured medians of 7.24 ms and 6.52 ms. That is roughly a 10% improvement under those conditions. **Do not compare 6.52 ms from that run against 196.87 ms from the separate cloud run.**

These measurements do not establish improved headset frame rate, Wi-Fi performance, time to audible speech or participant outcomes. In particular, this work is separate from the earlier voice-preparation frame-stall fix.

### The deterministic baseline still matters

The authored rule implementation also matched all ten C# cases and took about **0.0045 ms median per direct call**, excluding HTTP. Its perfect synthetic-label agreement would be circular because it generates those labels. Nevertheless, the model has not demonstrated an advantage over those rules for this exact policy. Any replacement decision should compare against both Jev and the rule baseline.

## Current integration: shadow observation

**Implemented:** `tools/local-jev/shadow.py` reads the existing Trial Viewer live replay mirror on the Mac. It feeds exact recorded `navigation_action` requests into v4 and joins recorded Jev responses by **recording path plus request ID**. Jev's answer and later trajectory never enter model inference.

The observer cannot start a trial, send an ADB command, synthesize audio or change playback. It writes private local SQLite and JSON evidence outside the source archive. Offsets and decisions commit together, allowing restart and deduplication. It separates unsupported contracts, local failures, Jev timeouts, pending/orphan responses, discarded requests and observed playback from decision agreement.

Only development quick tests with **Settings → DEV guidance: Jev-led** currently emit the supported action contract. Normal joint phrase/transmit requests, interruptions and round summaries are unsupported. This classifier is not a replacement for every Jev call.

Replay transfer makes this an asynchronous shadow of recorded inputs. Local compute time excludes transfer delay, whereas recorded Jev time includes headset/network cost. Shadow agreement is not correctness, and neither timing measures acoustic onset.

The observer passed nine new tests; the complete local-model suite passed 22 tests. An integration smoke test ran the actual saved model on 20 stored development fixture traces and matched their **historical** Jev choices 20/20. That is separate from the fresh cloud benchmark above and is not a live participant result. At initial live activation there were zero newly collected supported decisions.

## Reproducing the work

The commands below run from the repository root. The training and shadow implementation currently lives in the local `experiment/local-jev-20261025` checkout; that branch was not published as part of this article. A fresh clone of `main` may therefore lack these scripts. Model weights and generated datasets are stored outside Git. The paths below are local conventions, not downloadable model artifacts.

Create the environment and run the tests:

```sh
uv venv --python /opt/homebrew/bin/python3.11 \
  "$HOME/.local/share/follow-my-voice-models/venv"
uv pip install \
  --python "$HOME/.local/share/follow-my-voice-models/venv/bin/python" \
  -r tools/local-jev/requirements.txt
"$HOME/.local/share/follow-my-voice-models/venv/bin/python" \
  -m unittest discover -s tools/local-jev -p 'test_*.py'
```

Dependencies are pinned in `requirements.txt`: PyTorch 2.10.0, Transformers 4.57.6, NumPy 2.4.6, Safetensors 0.8.0 and Requests 2.34.2. The first base-model download needs internet; saved-model inference uses local files.

Reproduce the two training stages with **new output directories**:

```sh
"$HOME/.local/share/follow-my-voice-models/venv/bin/python" \
  tools/local-jev/train.py \
  --output "$HOME/.local/share/follow-my-voice-models/reproduction-v3"

"$HOME/.local/share/follow-my-voice-models/venv/bin/python" \
  tools/local-jev/continue_train.py \
  --parent "$HOME/.local/share/follow-my-voice-models/reproduction-v3" \
  --output "$HOME/.local/share/follow-my-voice-models/reproduction-v4"
```

Both stages refuse to overwrite an existing output directory. The retained original runs are `20261025-minilm-action-v3` and `20261025-minilm-action-v4` under the same model-storage root.

Compare saved versions:

```sh
"$HOME/.local/share/follow-my-voice-models/venv/bin/python" \
  tools/local-jev/compare_local.py \
  --before "$HOME/.local/share/follow-my-voice-models/reproduction-v3" \
  --after "$HOME/.local/share/follow-my-voice-models/reproduction-v4"
```

For an optional fresh cloud benchmark, run `benchmark.py --run <run-directory> --live-jev`. This makes at most 20 synthetic fixture requests using the locally configured `jev_key`; credentials must not enter Git or reports.

To observe new mirrored decisions, keep Trial Viewer in Live mode and run:

```sh
"$HOME/.local/share/follow-my-voice-models/venv/bin/python" \
  tools/local-jev/shadow.py \
  --model "$HOME/.local/share/follow-my-voice-models/20261025-minilm-action-v4/model" \
  --source "$HOME/Documents/Trial Review Live/data" \
  --output "$HOME/.local/share/follow-my-voice-models/shadow-live-20261025"
```

The default CPU backend avoids competing with the viewer's GPU. A new live output skips already mirrored bytes; newly arriving archive backlog can still be processed, so inspect recording identities. A separate output with `--from-start --once` processes existing recordings. Read `summary.json` and `decisions.json`; stop with Ctrl-C or an output-folder `stop-request` file, removing that marker before restart.

## Source and evidence map

These are paths in the development checkout. They are listed as paths rather than links to files that have not yet been published to the repository's default branch.

| Path | What it records |
|---|---|
| `tools/local-jev/data.py` | Initial examples, authored labels, features and external gates |
| `tools/local-jev/continuation_data.py` | Continuation splits and compact features |
| `tools/local-jev/train.py` | Initial full fine-tuning and validation selection |
| `tools/local-jev/continue_train.py` | Continued training, provenance and frozen selection |
| `tools/local-jev/model.py` | Saved-model inference and candidate masking |
| `tools/local-jev/benchmark.py` / `compare_local.py` | Timing and decision comparisons |
| `tools/local-jev/shadow.py` | Read-only replay observation |
| `docs/testing/2026-10-25-local-jev.md` | Initial methods, results and limitations |
| `docs/testing/2026-10-25-local-jev-continuation.md` | Continuation methods and results |
| Corresponding `*-results.json` files | Portable numerical results, hashes and paired observations |
| `docs/testing/2026-10-25-local-jev-shadow-smoke.json` | Stored-fixture observer integration result |

Run directories retain datasets, model/tokenizer files, training histories and reports. The continuation run also records `provenance.json`, `selection.json`, source hashes and the parent weight hash.

## What remains before replacing Jev

**Open decision:** promotion to active guidance requires independent labels for real production sequences and live shadow evidence across repeats, corrections, cue starvation, missing/noisy evidence, cancellation and condition changes. Splits should keep entire sessions or scenario families apart, rather than distributing neighboring moments across training and test.

Compare the candidate against Jev **and** the deterministic baseline, retaining mandatory gates and Unity's final playback checks. Evaluate decision usefulness and end-to-end response timing separately from label agreement. Participant-facing protocol changes remain a **Protocol proposal**, subject to the study's approval process; this development experiment is not an assertion of research approval.
