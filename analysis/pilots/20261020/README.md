# Paired guidance pilot — 20 October 2026

**Implemented comparison tooling.** This folder contains synthetic/anonymized evidence only. The original participant recording stays outside Git.

The principal result is a policy invariant, not a wearer or acoustic measurement: valid controller-on-target evidence no longer leaves contradictory search actions eligible. We compiled the actual production C# policy classes from `492be9d`, checkpoint `7299312`, and the fixed working tree against identical inputs. Per-file SHA-256 values identify the fixed source in the JSON report.

| Outcome | Earlier `492be9d` | Checkpoint `7299312` | Fixed |
|---|---:|---:|---:|
| Controller-conflicting eligibility episodes in 20 scripted trajectories | 7 | 7 | 0 |
| Contradictory starts in the synthetic playback adapter | 6 | 9 | 0 |
| Controller conflicts in 75 frozen recorded-request probes | 4 | 4 | 0 |

The third row counts snapshot probes, **not unique participant episodes**. The same 75 request snapshots from run025 were applied to all three variants. Each snapshot was held constant for 300 ms to probe eligibility. This does not reconstruct missing history or measure a participant's hold duration.

The adapter uses 50 ms simulated ticks, fixed 400 ms synthetic speech, actual `GuidanceSpeechPolicy` pacing constants, the production state machine and its final eligibility predicate. It is **not the full Unity `HintGenerator`/`VoiceSynthesizer` pipeline**. Its queued callbacks are discarded at their synthetic ready time; production can cancel earlier when a revision changes. Therefore its wait/late-response timing cannot be presented as measured or predicted headset latency. It does not test physical gaze acquisition, the overlap geometry calculation, or real audio playback. The subsequent visible-highlight reference filter and phrase-aware acknowledgment entry gates are covered by separate runtime regression suites; these trajectory results do not measure their benefit.

## Representative simulated sequence

For `controller_outside_gaze`, the controller reaches the target at 0.60 s while the nearby-gaze condition is absent until 2.00 s:

| Adapter event | Earlier | Checkpoint | Fixed |
|---|---:|---:|---:|
| Gaze confirmation | 0.15 s | 0.15 s | 0.15 s |
| Contradictory return instruction | 1.05 s | 0.85 s | suppressed |
| Further contradictory direction | 1.95 s | 1.75 s | suppressed |
| Controller confirmation | 3.00 s | 2.30 s | 2.05 s |

The fix suppresses contradictory directions immediately but does not confirm the controller until the controller hold is qualified and nearby gaze is valid. Gaze confirmation, pointing confirmation and trigger selection remain separate. This adapter does not simulate a trigger selection; it makes no completion claims.

The `blink_recovery` trajectory demonstrates a separate defect: the earlier/checkpoint policies consume the pointing gate during a gaze confirmation, so no pointing confirmation occurs on recovery. The fixed policy emits the distinct pointing confirmation. The short-hold scenario still produces no pointing confirmation.

In the `audio_hold_target` scenario, protected existing audio lasts until 3.00 s. Qualification occurs at 1.10 s and the adapter starts the confirmation at 3.05 s in all versions: **1.95 s remains**. This is an illustrative finish-phrase delay, not measured headset latency or proof that this interruption policy is appropriate. Actual sequence telemetry and Astra's advisory assessment must be considered separately.

## Real Jev request probe

`jev-eligible-sets.json` records 12 actual requests from the Mac to Vercel using synthetic state and no participant data. All returned a valid offered phrase. Median response time was **163 ms**, p95/maximum **498 ms**; **1/12** exceeded the runtime's 450 ms catalog deadline. No host request failed under the diagnostic 1.5-second timeout. The cold connection is labeled separately. These results are not headset-network measurements and are too small a sample to establish a durable failure rate.

The exact half-open angle/distance constraints yield **five unique eligible phrases per sampled cell**. Thus the requested small set (5) and full eligible set are identical. A medium set of 15 cannot be tested from this catalog without authoring additional valid options. We did not widen geometry, duplicate choices, change participant policies, or infer an API limit. The endpoint's support for larger sets remains untested.

## Reproduce

From the repository root:

```sh
python3 -m unittest discover -s analysis -p 'test_guidance_paired_pilots.py' -v
python3 analysis/guidance_paired_pilots.py \
  --output analysis/pilots/20261020/guidance-paired-pilots.json \
  --network-report analysis/pilots/20261020/jev-eligible-sets.json \
  --recording '/path/to/run025/recording.jsonl'
```

Omit `--recording` to run the 20 scripted trajectories without private source data. The harness compiles the two Git revisions and the current working tree; changing the latter changes the report and its source hashes.

The separate network probe is opt-in, bounded to 12 requests, and reads the key without printing it. It requires a Python environment with `requests` installed; the offline tests and C# replay do not require that package:

```sh
python3 analysis/guidance_jev_set_pilot.py \
  --keys Assets/StreamingAssets/api_keys.json \
  --output /tmp/jev-eligible-sets.json
```

The shared viewer copy is `~/Documents/Trial Review Evaluations/guidance-paired-pilots.json`. The Evaluation tab distinguishes this policy comparison from recorded software playback and advisory Astra judgments.

Open evidence gaps: actual post-fix wearer sequences; acoustic onset; full interruption behavior under real speech durations; repeated or unnecessary wording judged in context; behavioral benefit; medium/full set comparison after additional geometrically valid phrases exist. A replayed counterfactual does not establish how a participant would respond.
