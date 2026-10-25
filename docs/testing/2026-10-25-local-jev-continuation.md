# Further local Jev fine-tuning — 2026-10-25

**Implemented experiment; not deployed.** Continued training improved offered-action agreement on a fresh 500-case synthetic policy test from **77.4% to 98.4%**. With the existing external gates, the saved-model inference path matched all 500 expected actions. The previous ten production-shaped development cases improved from 7/10 to 10/10. These are authored-policy tests, not participant helpfulness or study-outcome measurements.

## What changed

Continued the saved v3 MiniLM weights for eight additional epochs on 2,400 new synthetic training rows and 400 validation rows. All five actions, cardinal/diagonal directions, repeated and changed directions, corrective returns, threshold-adjacent silence, missing evidence and single/double gate failures are represented. Data still uses prepared phrases from the compiled C# `DirectionalHintPolicy.AllPhrases()` library. No participant data, voice samples or Jev-generated labels were used.

A compact, versioned representation keeps each gate individually, normalizes the controlled direction vocabulary and includes last actual speech plus offered phrases. It never includes the oracle's output, a precomputed gate decision or a precomputed repeat decision. The training/validation maximum input length decreased from 151 to 130 tokens. Old model configs retain the old input function, so comparisons load each checkpoint with its intended representation.

This changes input engineering, training examples and training duration together. It does not isolate the causal benefit of extra epochs alone. The weights remain the same 22.7M-parameter architecture, approximately 90.9 MB; the parent weight hash was verified unchanged.

## Selection and test isolation

- Fixed seed, AdamW learning rate 0.00002, batch 24, gradient clipping, 6% warmup and cosine decay, eight-epoch/20-minute bounds. Completed training and comparative evaluation in 379 seconds on Mac MPS.
- Initial saved weights with the new representation scored 75.25% on the new validation set. Epoch 7 reached 96.5%; epoch 8 tied, so the earlier checkpoint was retained. Selection was written before fresh evaluation began.
- Offered direction-cue wording families are disjoint between continuation training, validation and fresh evaluation. Validation wording was available in the parent's training pool; it is not claimed to be unseen across all training history.
- The fresh set contains 500 balanced examples, 100 per action, with cue wording absent from both training rounds and triple gate-failure combinations absent from continuation training. Labels come from the same authored policy oracle; this is structural generalization, not independent human validation or a representative field distribution.
- The old 320-case synthetic test and ten production-C# cases are explicitly regression checks. They were previously inspected and no longer serve as new holdouts. No fresh-test result was used to retrain or select another checkpoint.

## Decision agreement

Both versions were evaluated on the identical examples. “Masked” restricts outputs to offered actions; “gated” additionally applies the unchanged mandatory silence checks outside the network.

| Dataset | n | Previous masked | Continued masked | Previous gated | Continued gated |
|---|---:|---:|---:|---:|---:|
| Validation | 400 | 76.0% | 96.5% | 85.5% | 100% |
| Fresh wording and combinations | 500 | 77.4% | 98.4% | 86.2% | 100% |
| Earlier synthetic regression | 320 | 68.4% | 96.3% | 77.8% | 100% |
| Production-shaped regression | 10 | 70.0% | 100% | 70.0% | 100% |

Fresh raw model accuracy was 98.4% (previous 74.8%). The new model proposed no unoffered actions. Its eight fresh mistakes were all speech actions under closed/missing-evidence gates; the external gate returned `wait`. The model has therefore **not** learned perfect gate behavior, and removing those checks would reintroduce errors.

A separate verification reloaded the saved model and called the actual `Policy.predict` path on all 500 fresh examples. It obtained 500/500 expected final actions with gates enabled and zero disagreements with batched evaluation. The model's probabilities remain uncalibrated.

## Latency

The paired local comparison alternated before/after order over the same ten regression payloads, five repeats each, using warmed loopback HTTP services on MPS. All 100 requests succeeded. Repetitions are timing samples, not additional independent quality cases.

| Paired local model | Median | p95 | Expected responses |
|---|---:|---:|---:|
| Previous v3 | 7.24 ms | 9.42 ms | 35/50 |
| Continued v4 | 6.52 ms | 7.90 ms | 50/50 |

That is about 10% lower median latency in this paired run. Previous separately measured 19 ms and current paired 6.52 ms are different benchmark conditions and should not be treated as a controlled threefold model improvement.

A separate alternating local/cloud comparison used the same ten payloads twice. The warmed new local endpoint measured median **15.27 ms**, p95 **23.28 ms**; current Jev measured median **196.87 ms**, p95 **399.46 ms**. Both had zero HTTP/parse failures; expected-response agreement was local 20/20 versus Jev 16/20. Cloud timing includes network overhead and the first request. Do not compare the fastest 6.52 ms local-only result directly against the separate cloud run.

Direct-call profiling recorded CPU median 11.46 ms and MPS median 5.76 ms. Loading/first-request costs were CPU 89/32 ms and MPS 137/243 ms. Profiling launched during the tail of final offline evaluation, so initialization/direct-call costs are diagnostic, not isolated comparative evidence. The paired local comparison ran afterward with training/evaluation finished. Mac process load and GPU scheduling are uncontrolled; these are small development measurements, not headset FPS, Wi-Fi or acoustic-onset measurements.

The authored-rule baseline again matched all ten production cases, at 0.0045 ms median direct-call cost excluding HTTP. ML still has no demonstrated advantage over those rules for this exact authored policy. Its perfect synthetic label agreement would be tautological because it supplies the labels.

## Current decision

**Open decision:** the learned candidate is substantially stronger and remains fast enough for further development testing. Keep it isolated from participant-facing Unity until independent production-sequence labels and live shadow tests establish useful behavior. Preserve the mandatory gates, offered-action mask, and Unity's final freshness, identity, geometry, prepared-audio, cancellation and playback checks.

Only `navigation_action` is supported. Normal joint phrase/transmit requests, interruptions and round summaries remain outside scope. Nothing was changed in Unity or deployed to the headset; the previous model remains available.

## Reproduce and inspect

- [`tools/local-jev/README.md`](../../tools/local-jev/README.md): continuation training, current server and comparison commands.
- [`2026-10-25-local-jev-continuation-results.json`](2026-10-25-local-jev-continuation-results.json): metrics, paired observations, remaining mistakes, selections, configuration and hashes.
- Model/data/full predictions: `/Users/pedro.poveda/.local/share/follow-my-voice-models/20261025-minilm-action-v4/`.
- Logs: `/tmp/local-jev-training-v4.log`, `/tmp/local-jev-benchmark-v4.log`, `/tmp/local-jev-paired-v4.log`.
- Thirteen tests passed, including old-format compatibility, split isolation, all-action coverage, actual prepared-phrase membership and endpoint failure handling. Independent review confirmed selection/feature-version handling and identified the validation-wording nuance documented above.
- All benchmark servers shut down. No credentials are included in results; no external training/upload occurred.
