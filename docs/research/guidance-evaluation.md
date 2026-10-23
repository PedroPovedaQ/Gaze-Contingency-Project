# Offline guidance evaluation

**Implemented — 2026-10-20.** `analysis/guidance_eval.py` exports evidence for reviewing a recorded run. It does not call Jev, an external judge, or a training service. It does not estimate perceived correctness from logs.

```sh
python3 analysis/guidance_eval.py --recording /path/to/recording.jsonl --out /path/to/evaluation
```

Exporter API:

```python
from guidance_eval import evaluate_recording
report = evaluate_recording(recording_path, output_directory)
```

The return value is the same dictionary saved as `guidance-evaluation.json`. Existing output directories are supported. On regeneration, human rating columns are preserved by case ID; the evaluator refuses to discard filled ratings belonging to another source version.

## Three artifacts

| Artifact | Purpose |
|---|---|
| `guidance-evaluation.json` | Retrospective report: recorded request, response, exact-ID playback joins, deterministic checks, evidence sequence numbers and coverage. |
| `guidance-human-ratings.csv` | One row per Jev request or local requested guidance decision, with blank human judgment fields. Suspended samples are not rating decisions. |
| `guidance-benchmark.jsonl` | Frozen Jev request payloads for paired model comparisons. Inputs contain no response, later playback or later user behavior. |

Each report includes the recording SHA-256, recording ID, application/build versions and evaluator version. A case ID combines the recording hash and request sequence. Sequence IDs such as `seq:123` identify evidence in that recording. `sourceTrialId`, `trialId`, round and block remain in the local report for viewer joins.

**Implemented:** request-to-response and request-to-playback measurements subtract replay-relative event times only. Absolute Unity snapshot times are not subtracted from replay times. These are software timestamps, not acoustic onset measurements. Missing events remain null. Duplicate request identities are flagged and excluded from benchmark/timing summaries.

## Confirmation sequences

**Implemented — October 20, 2026:** sequence evaluator `guidance-sequences-v1` follows a qualified confirmation through successive requests, wait responses and exact-ID playback. It measures from the **first recorded qualified request**, not an inferred sensor onset. A later request cannot reset the accumulated delay. Each participating decision receives the same retrospective episode, including evidence references, wait count and outcome. Astra receives that episode even when its requests span different evaluation batches.

Gaze and controller confirmations are separate event types. Gaze confirmation requires the recorded gaze qualification. Controller confirmation requires the controller hold, controller hit and valid gaze in the target vicinity; it does not require an additional gaze hold. A new controller confirmation after a gaze acknowledgment is not automatically redundant. For newer local decisions the recorded central state's qualified action supplies the qualification evidence. Unknown legacy state stays unknown.

Episodes stop at recorded disqualification, invalid gaze samples, sample gaps longer than 0.25 seconds, trial/lifecycle boundaries or matching playback. A missing playback event produces unknown latency, not a slow-failure score. Request snapshots do not prove continuous fixation between observations. The report also supplies actual recent playback from the same trial: up to six starts in the preceding 15 seconds, with event types where recorded.

**Engineering review setting:** confirmation delay above 1.0 second is flagged for review. This is not a validated threshold for human perception or a headset specification. The judge must consider accumulated waits and known blockers instead of accepting a long chain merely because its final request was quick or it stayed under the live deferral limit. Protected speech, tracking loss and requalification remain distinct from avoidable idle waiting.

The Evaluation tab displays sequence timing and wait count separately from individual-request latency. Retrospective context is never added to the request-only model benchmark. Rubric `guidance-review-v2-sequences` invalidates prior cached judgments.

## What the checks establish

- **Choice membership:** whether a returned choice exactly matches an offered choice. An eligible answer is not necessarily a good answer.
- **Direction sign consistency:** for the recognized `speech-navigation-v2` request schema, compare a selected phrase's horizontal metadata with the recorded signed yaw. This requires explicit valid-gaze and available-geometry flags. Zero and exactly 180 degrees remain unscored. This does not establish calibration accuracy, good magnitude wording, correct vertical advice, or relevance after the user moves.
- **Speak prerequisites:** for a `speak` answer with known values, check recorded valid gaze and idle audio. A `wait` answer is not automatically correct. This check does not adjudicate naturalness or an appropriate pause.
- **Recording consistency:** reuse `guidance_audit.py` for the decision/audio contract. This evaluator has no raw-eye input; unavailable exact-eye joins are counted separately rather than labeled sensor failures. Run the existing raw-eye audit separately to verify observation provenance and freshness.

Pass, fail and unknown counts always remain separate. There is no global “Jev accuracy” score. The central state is recorded only when a versioned `guidance-state-v1` object has a nonblank phase; legacy booleans and empty serialization placeholders are not promoted into an invented state-machine phase.

Local decisions without Jev requests appear in the rating sheet and report but are not converted into hypothetical Jev benchmark inputs. Absent playback can reflect a deliberate wait, cancellation, missing recording coverage, or an error; the evaluator does not choose an explanation without evidence.

## Human rating protocol

**Protocol proposal:** review synchronized replay/audio around each decision, including the immediately preceding instruction and target/gaze/controller evidence. Enter a rater ID and separate 1–5 ratings:

- Appropriateness: 1 = misleading or contradicted by the available situation; 3 = defensible but not useful enough; 5 = helpful and appropriate.
- Naturalness: 1 = awkward or disruptive; 3 = understandable; 5 = natural in this interaction.
- Timing: 1 = clearly too early/late or disruptive; 3 = acceptable; 5 = timely.

Leave a dimension blank when the evidence is insufficient. Record confidence, comments and an optional preferred offered choice. These are proposed anchors, not a validated questionnaire. Neither a Jev choice nor a second model's judgment is ground truth. Audio quality requires listening; the text alone cannot establish delivery or a natural interruption. Use independent raters on a shared subset, report agreement and adjudicate disagreements before treating labels as training targets. Store different raters' sheets separately; the generated file has one rating row per case.

## Model comparison and eventual fine-tuning

**Protocol proposal:** replay the exact same eligible candidates and request-time state to each candidate model. Keep model/provider/version, prompt/rubric version, generation settings, latency, failures and cost alongside each result. Randomize or blind model labels during human review. Compare matched decisions, and report participant/session-level uncertainty rather than treating every closely spaced hint as an independent participant.

Benchmark `input.payload` contains only the original request. Responses and later playback are retained in the separate retrospective report for evaluation. Do not merge them back into model input. Outcomes such as later successful selection can inform review, but were unavailable at the original decision time.

**Required before training:** split by participant first and keep every session/trial from that participant in the same partition. The exported participant/session/trial group hashes support this; they do not perform the split. Development, demo and test identities have an unknown participant group and require manual mapping before inclusion. Also deduplicate repeated states or scenario templates across partitions, freeze a held-out evaluation set before tuning, and keep annotation/prompt revisions versioned. Do not put nearby frames from one run in both training and evaluation.

These files contain local research data. Hashing group identifiers is pseudonymization, not anonymization. Recorded payloads may include identifiers or free text, so any external judge must explicitly allowlist and review the fields it sends.

**Open decisions:** annotation ownership, independent-rater sample size, accepted timing tolerances, participant grouping for development runs, model-comparison endpoint/cost policy, and whether the available consent permits secondary model training. No model call, upload, fine-tuning, or scientific validation is performed by this exporter.

## Trial Review and Astra judge

**Implemented — October 20, 2026:** completed run export generates these artifacts automatically. The **Evaluation** tab shows run-level checks, selected-trial decisions, and separate Astra verdicts with rationale and evidence references. Clicking a decision seeks to its request-time scene. Selected/proposed text and recorded playback are shown separately. Old recordings are supported with explicit coverage gaps.

When a legacy schedule/summary is incomplete, the viewer preserves the full CSV export error and generates guidance evidence directly from the recording in a separate folder. This does not reconstruct missing trial performance or subjective data.

The optional automatic Astra stage runs after export using the locally installed Codex CLI, saved ChatGPT authentication, and an explicit `gpt-6-astra` request. The runner prefers the current ChatGPT app's bundled CLI where available, because the older global CLI on this computer was rejected for this model. It never silently falls back to an API key or another model. Authentication, quota, outdated-CLI and model-access failures produce visible statuses.

```sh
python3 analysis/guidance_codex_judge.py \
  /path/to/export/guidance-benchmark.jsonl /path/to/separate/judgments \
  --evaluation /path/to/export/guidance-evaluation.json
```

The Astra stage is **post-session**, not local model inference: sanitized decision evidence is sent through the user's Codex subscription. It uses that account's limits. The live headset loop never waits for it. The viewer's automatic-evaluation toggle controls future runs; an already started bounded job may finish in the background.

Only allowlisted state, candidate semantics, proposed/selected/played phrases, software timing and evidence references are sent. Full recordings, audio, participant names, source paths and credentials are not part of that request. The judge receives retrospective outcomes separately from benchmark inputs. It must return correct/incorrect/unknown for direction, state appropriateness, timing, repetition and interruption; these are model judgments, not measured perception. It must cite supplied evidence references, and local validation rejects missing cases or invented references.

The CLI runs from an isolated temporary working directory with a read-only sandbox and a reduced environment. The instruction to avoid tools and files is part of the rubric; this has not been established as a hard filesystem-read boundary. Treat the judge as an advisory workflow for the authorized development recordings, not a validated isolation mechanism for arbitrary untrusted inputs.

Results and a content-addressed cache live outside the immutable CSV export, under `Documents/Trial Review Evaluations`. Reports retain benchmark/evaluation hashes, requested model and rubric version. Batches are bounded and grouped by trial; retries reuse valid successful batches. A directory lock prevents competing evaluations from overwriting each other. Cache provenance identifies the requested model; it does not establish an immutable provider model snapshot.

Copy the generated human-rating worksheet into a separate review directory before entering ratings; do not edit the checksum-protected export package. No fine-tuning or local-model deployment is included. Human review and a held-out participant/session split remain required before using these judgments as training labels.

**Verified integration:** a real saved request was evaluated successfully with Astra through ChatGPT-authenticated Codex. The initial attempt using the older global CLI failed explicitly. This verifies the transport/schema path, not scientific validity of the judge.

Official references: [Codex non-interactive execution and structured output](https://learn.chatgpt.com/docs/non-interactive-mode), [ChatGPT subscription authentication](https://learn.chatgpt.com/docs/auth).
