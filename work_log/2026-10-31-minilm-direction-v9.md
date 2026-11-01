# Direction-aware MiniLM candidate — 2026-10-31

## Implemented input contract

Both action and phrase requests carry `state.direction` with schema `adaptive-direction-v1`:

- `needed`: `left`, `right`, `up`, `down`, `up left`, `up right`, `down left`, `down right`, `aligned`, or `unknown`. This maps the actual Unity direction, including its committed long-turn choice and existing within-wall hysteresis. Python does not recompute it from target coordinates.
- `moving`: a direction followed by `toward`, `away`, or `across`, or `still` / `unknown`. Unity uses net gaze azimuth/elevation changes over its latest two-second spatial history, unwraps azimuth steps, and compares the motion vector with the current shortest signed target-error vector. This separates following a committed long-way cue from moving toward the shortest target bearing. No target identity is exposed by these fields.
- `started`: `same plane`, `small turn left/right`, `medium turn left/right`, `large turn left/right`, `behind`, or `unknown`. The source is the same trial's **realized** signed pre-cue theta snapshot, relative to the readiness cross. Missing snapshots (including practice) remain unknown; the assigned schedule is never substituted.

Movement needs at least two samples spanning 0.5 seconds, a newest sample no older than 0.75 seconds, and no gap exceeding 1.25 seconds. Tracking invalidity clears the history. Net movement below one degree is `still`; each axis below one degree is omitted. Normalized projection greater than 0.25 is `toward`, less than -0.25 is `away`, otherwise `across`. These are **engineering bands**, not validated fixation/saccade or sensor-accuracy thresholds. Start bands: absolute turn below 22.5 degrees is same plane; up to 67.5 small, up to 112.5 medium, below 157.5 large, otherwise behind. Neither new encoder input contains raw angles for these fields.

Action features: `adaptive-v5-direction-v1`. Phrase features: `adaptive-phrase-ranker-v3-direction`. Candidate authored policy: `jev_adaptive_assistance_v5`; `reference(..., policy=...)` can also reproduce v4 rules. Older action features `adaptive-v4-progress-v3` and `adaptive-v2-measured-v1`, phrase features `adaptive-phrase-ranker-v2`, and policy v4 remain readable. Missing direction objects become three unknowns. V8's 448 frozen action feature strings and all 160 phrase groups were checked byte-for-byte against its saved adapter.

The new context is allowlisted. Participant/target IDs, target shape/color, Jev replies, labels and future observations never enter the encoder. Existing non-direction features and actual past speech history remain. New state words are also available to the existing request/recording mechanism without rewriting older recordings.

## Authored policy changes

1. Existing hard gates and verified-departure correction remain first and unchanged.
2. If gaze follows Unity's needed direction while shortest-angle distance increases, and the trial started with a large turn on that side or behind, while outside the target wall, briefly observe during the existing `recent` silence band (less than four seconds). This recognizes a possible committed long-way turn. It does **not** manufacture progress, encouragement availability, a new direction, or an indefinite silence exemption.
3. If gaze moves opposite a needed axis and away from the target, prefer the offered explicit orient/refine action, falling back to its brief action or observation. This supplies clearer guidance for movement away from a correct cue. It does not invent a verified-departure correction.
4. Otherwise retain the v4 reference policy. Start position alone never changes an answer. Unknown direction evidence does not invent a path.

Phrase preferences remain unchanged: discourage exact repetitions, then prefer concision/detail according to the selected action and prior interventions. All candidate phrases are already spatially truthful; no new lexical direction selector is introduced. Gates, offered menus, arrival overrides, actual-playback phrase history, cancellation/freshness/identity checks, and final playback revalidation are unchanged.

## Training and evaluation design

Authored synthetic supervision only. New action data contain 2,240 training, 448 validation and 448 test states, balanced across seven actions, with fresh final seeds 261031111/261031112/261031113, feature deduplication, different test cadences and triple gate conflicts. The final generator computes motion from the same two-second window as Unity and includes recent reversals after earlier improvement. Corpus counts are included in the final receipts. Synthetic direction varies independently of target identity and irrelevant metadata; cue direction, motion relation and start bands distinguish cases v8's scalar features collapse.

New phrase data contain 768/160/160 training/validation/test states. The production C# menu exporter's explicit `needed` field determines its synthetic needed field; unsupported motion evidence remains unknown. Phrase repetition and preference rules are unchanged. V9 continues v8's action encoder/head and scalar phrase ranker, separately, selecting checkpoints using validation only. The test sets and fixed direction probes do not select checkpoints. No retraining after inspecting comparisons is planned.

Paired evaluation applies **both** versions to v8's frozen tests and v9's new tests, the existing 14 regression cases, five already-selected actual requests from DEV_NEUTRAL runs 33/34, and three fixed long-way/wrong-way pairs whose v8 feature strings are identical. Original recordings are opened read-only and hash/size/mtime checked before and after. Historical request adapters remove retired wait options and upgrade the contract identifier, without deriving new measurements or borrowing future samples. Legacy recordings lack these direction fields and remain unknown. Their outcomes cannot establish performance with newly captured direction data.

The first action run is preserved as `20261031-minilm-direction-v9-superseded-geometry`: source review found impossible long-way examples below Unity's 150-degree route-retention boundary. The corrected final corpus uses 168–178-degree current yaw with continuous receding history. The first trainer had already printed its test aggregate before this source correction. Its predictions did not choose the correction, but these are development evaluations after iteration, not an untouched final holdout or independent human assessment. An early phrase run was interrupted after finding that lexical parsing could mistake “Right area” for rightward movement; the final exporter provides Unity's explicit direction. A second phrase attempt was interrupted for concurrent MPS memory contention before completing an epoch; the final run uses the same corrected corpus and seed, sequentially. A subsequent source review found the action generator was using the four-second trend for a two-second motion field. That action bundle is preserved as `20261031-minilm-direction-v9-superseded-window`; final action training uses the corrected two-second calculation, an added earlier-improvement/recent-reversal family and fresh seeds. The phrase corpus overrides its simulated motion from the selected menu (unknown except offered encouragement), so its independently trained corpus is retained with its own saved source snapshot and hashes. All superseded/partial directories remain separate; none is selected for adoption.

Agreement means imitation of authored preferences, not improved guidance, participant benefit or validation of the thresholds. Latency is Mac CPU decision compute; it excludes headset transport and acoustic onset.

## Adoption checks fixed before paired evaluation

Retain v8 unless v9 preserves every accepted existing/development regression case, does not lower aggregate masked/gated agreement on the frozen v8 action test or preferred-choice agreement/no-repeat counts on the frozen v8 phrase test, has no gate/menu/override regression, and stays in the same latency range. Also report raw closed-gate advice separately; any increase is treated conservatively as a reason not to adopt. For timing, “same range” means median no more than 1.25× v8 and p95 no more than 1.5× v8 for direct action calls and complete loopback two-stage decisions. These are engineering adoption tolerances, not participant-effect thresholds. No retraining or checkpoint reselection follows the paired comparison.

## Frozen comparison and adoption result

**Implemented: adopt v9.** Every predeclared adoption check passed. The default bundle is `20261031-minilm-direction-v9`, policy `jev_adaptive_assistance_v5`. V8 remains readable and its weights are preserved. The default change does not assert better participant guidance.

Action agreement (raw = offered-menu-masked in these evaluations; final column applies the unchanged gates):

| Test set | v8 raw / masked | v8 after gates | v9 raw / masked | v9 after gates |
|---|---:|---:|---:|---:|
| v8_test | 424/448 (94.64%) | 426/448 (95.09%) | 431/448 (96.21%) | 433/448 (96.65%) |
| v9_test | 391/448 (87.28%) | 393/448 (87.72%) | 433/448 (96.65%) | 435/448 (97.10%) |
| existing_regression | 13/14 (92.86%) | 14/14 (100.00%) | 13/14 (92.86%) | 14/14 (100.00%) |
| real_development | 5/5 (100.00%) | 5/5 (100.00%) | 5/5 (100.00%) | 5/5 (100.00%) |
| direction_pairs | 3/6 (50.00%) | 3/6 (50.00%) | 3/6 (50.00%) | 3/6 (50.00%) |

Phrase preferred-choice agreement before/after gates is identical on both open-gate test sets. The singleton/arrival suite separately exercises the verified playback override; it tests phrase preference as well as eligibility.

| Phrase test | v8 before / after | v9 before / after | v8 no-repeat | v9 no-repeat |
|---|---:|---:|---:|---:|
| v8_test | 152/160 (95.00%) | 156/160 (97.50%) | 88/88 | 88/88 |
| v9_test | 148/160 (92.50%) | 153/160 (95.62%) | 78/78 | 78/78 |
| singleton_and_arrival_regression | 1/2 (50.00%) | 2/2 (100.00%) | 0/0 | 0/0 |

No-repeat 0/0 means no alternative unused wording opportunity, not a failure. The default phrase teacher remains unchanged.

The two development recordings contain 28 and 32 action requests respectively, and no `assistance_phrase` requests. The five previously selected regression requests remain the real-data test subset; no new cases were selected from model outcomes. Real-recording phrase agreement is therefore **unavailable**, not 100%. Phrase-stage evidence comes from the two frozen synthetic sets and dedicated singleton/arrival cases.

Both versions produced two speaking recommendations behind closed gates on each 448-case action test; the unchanged gates blocked every one. In the 14-case suite each had one such raw recommendation and 14/14 after gates. Both passed all nine individual action/phrase gate probes. No unoffered action/phrase was returned, and the existing arrival, cancellation, freshness, identity and playback tests passed.

### Action results by class

Correct/total before gates → after gates; each class has 64 cases in each synthetic test. Precision, recall and F1 are in the JSON receipt.

| Class | v8 on v8 test | v9 on v8 test | v8 on v9 test | v9 on v9 test |
|---|---:|---:|---:|---:|
| observe_1s | 62/64 → 64/64 | 62/64 → 64/64 | 61/64 → 63/64 | 61/64 → 63/64 |
| orient_brief | 62/64 → 62/64 | 62/64 → 62/64 | 54/64 → 54/64 | 60/64 → 60/64 |
| orient_explicit | 63/64 → 63/64 | 60/64 → 60/64 | 51/64 → 51/64 | 62/64 → 62/64 |
| refine_brief | 64/64 → 64/64 | 61/64 → 61/64 | 57/64 → 57/64 | 61/64 → 61/64 |
| refine_explicit | 63/64 → 63/64 | 58/64 → 58/64 | 51/64 → 51/64 | 61/64 → 61/64 |
| correct | 46/64 → 46/64 | 64/64 → 64/64 | 53/64 → 53/64 | 64/64 → 64/64 |
| encourage | 64/64 → 64/64 | 64/64 → 64/64 | 64/64 → 64/64 | 64/64 → 64/64 |

Phrase per-class preferred agreement and no-repeat counts for **both versions on both sets**, including the singleton correction case, are in [per-class.csv](receipts/2026-10-31-minilm-direction-v9/per-class.csv). A class with zero cases is explicitly untested in that set.

### Concrete differences and a remaining failure

- New-test `test-127`: needed right, moving left away, started same plane. V8 chose `encourage`; v9 chose the authored `orient_explicit` answer.
- New-test `test-357`: needed left, moving right away, started behind. V8 chose `observe_1s`; v9 chose the authored `orient_explicit` answer.
- New-test `test-692`: needed down right, moving still, started small turn left. V8 correctly chose `refine_brief`; v9 incorrectly chose `refine_explicit`. Aggregate gains do not eliminate individual regressions.
- **Long-way failure:** all three fixed pairs use a 174° rear-seam state. For each pair, only motion relative to Unity’s needed direction changes; v8’s feature text is byte-identical. Both models chose `orient_explicit` in all six cases. Both were right for the three opposed-motion cases and wrong for the three along-cue cases where the authored v5 answer is brief observation. V9 has not reliably learned this relation. No post-comparison retraining or test-based selection was performed.

### Mac latency

Apple M4, 32 GB RAM, macOS 27.2, CPU inference with four Torch threads. Models loaded once, warm-up excluded; training had ended. Both versions received the same first 12 eligible frozen v8 phrase-test states, repeated three times. Only actually speaking decisions count as complete two-stage samples. v8 skipped stage two six times; v9 skipped it nine times. The sample counts differ and these are engineering timing samples, not independent quality observations.

| Measurement | v8 median / p95 | v9 median / p95 | Samples v8 / v9 |
|---|---:|---:|---:|
| Direct action | 6.81 / 7.30 ms | 7.06 / 7.38 ms | 36 / 36 |
| Direct action + phrase | 26.81 / 32.75 ms | 28.36 / 34.83 ms | 30 / 27 |
| Complete action + phrase over loopback HTTP | 28.46 / 34.23 ms | 33.84 / 40.99 ms | 30 / 27 |

V9’s HTTP two-stage median is 18.9% higher on this workload, within the predeclared 25% median tolerance; direct median is 3.8% higher. Full min/max, phase timings and gate results are in the receipt. These timings exclude headset Wi-Fi/ADB, Unity main-thread scheduling and acoustic onset. The historical v8 84–87 ms two-stage and 12–18 ms direct figures used a different probe/workload; do not present the new numbers as a speedup over those historical measurements.

### Thesis replacements

- **3.1.4.2:** describe candidate/default v9 `state.direction.schema=adaptive-direction-v1`, `needed`, `moving`, `started` with the above causal sources and unknown handling. The directions are Unity’s words, not model-computed geometry. Keep target identities/shape/color, labels, Jev answers and future behavior out of the encoder.
- **3.1.4.4:** default model name becomes `20261031-minilm-direction-v9`. For the fresh benchmark, report **7.06 ms direct median / 7.38 ms p95 (36 calls)** and **33.84 ms complete loopback two-stage median / 40.99 ms p95 (27 speaking decisions)**. Label the measurement scope and retain 84–87/12–18 only as historical v8 probes, not current v9 values.
- **3.1.4.7:** action train/validation/test remain **2,240/448/448** (320/64/64 per action). Replace current action test **94.6% → 96.65% (433/448)** and after-gates **95.1% → 97.10% (435/448)**. Phrase data are **768/160/160**; current phrase agreement **95% → 95.625% (153/160)** and no-repeat **88/88 → 78/78 on the new phrase test**. Also show the paired old-test results: v9 **431/448 before, 433/448 after**, phrase **156/160**, no-repeat **88/88**. The differing no-repeat denominators reflect test opportunities, not reduced coverage of an unchanged set.
- Candidate policy is **v5**, action features **adaptive-v5-direction-v1**, phrase features **adaptive-phrase-ranker-v3-direction**. Action checkpoint selected epoch **6/6**, validation **418/448 before gates, 441/448 after**; phrase selected first best epoch **3/4**, validation **155/160**, no-repeat **103/103**. Action training **265.0 s**, phrase **413.6 s**. Preserve the authored-label and iterative-development limitations, including the unresolved **3/6** directional probe result.

Full metrics, per-class results, hashes and read-only source receipts: [comparison-summary.json](receipts/2026-10-31-minilm-direction-v9/comparison-summary.json). Detailed machine-local outputs and all weights remain outside Git under the named model bundle.

## Verification and delivery — November 1, 2026

All **632** active Python tests ran: **631 passed, one optional statsmodels test skipped**. Suites: analysis 370, desktop 128, scripts 19, analysis/tests 17, paper-to-podcast 18, interactive-paper-podcast 21, sync utility 14, local-jev 45. The sync tests use temporary repositories; no Overleaf publication occurred. Live provider/microphone smoke scripts and archived thesis copies were excluded. Offline C# guidance, voice isolation, spatial voice, activation, validation, gaze quality, headset preparation, native gaze sanitizers, rotational layout, trial timeout and mocked enrollment checks passed. The adopted policy passed **27 Unity integration checks**. Details: [verification.json](receipts/2026-10-31-minilm-direction-v9/verification.json).

The desktop build passed **25 checks**. The current app was stopped if running, old Previous removed, current renamed to `Follow My Voice Study Intake Previous.app`, and the verified build copied to `Follow My Voice Study Intake.app`. All **169 files/symlinks** match the build; Previous matches the former current. No new named bundle was added to `~/Applications`. [Install receipt](receipts/2026-10-31-minilm-direction-v9/desktop-install.json).

The required Unity refocus/menu helper found the wireless headset but macOS activation failed with **-609**. Batch Build And Run was used instead. Final Android deployment evidence follows below.

[Completed review](../docs/reviews/2026-11-01-minilm-direction-v9/report.md): no remaining actionable code findings. Review and simplification ran sequentially in the main thread per repository instructions; no independent-peer coverage is claimed. Inherited `progress.md` edits remain outside this commit, with this session's log prepended. Model weights and raw recordings remain outside Git; neither development source changed. No push, Overleaf publication, `docs/thesis` edits, microphone, ElevenLabs or cloning activity.

Android built successfully in **76.1 seconds**, installed and launched (`Status: ok`) on wireless **VIVE Focus Vision 192.168.1.208:5555**. Installed APK SHA-256 matches the build: `ed39726c99ece50b47257a6788586516ef4011df739586849aa85ce1bf0c6a07`. Both service stages report v9 and their checkpoint hashes match the frozen comparison. Unity's shutdown stopped its ADB server; the remembered wireless endpoint and model reverse tunnel were restored afterward without an explicit server reset. Keep the Mac awake and ADB connected for local-model sessions. No wearer walkthrough is claimed. [Android receipt](receipts/2026-10-31-minilm-direction-v9/android-install.json), [model readiness](receipts/2026-10-31-minilm-direction-v9/model-ready.json).

The VIVE build hook removed three preloaded-asset entries from project settings; those generated changes were restored to the clean starting settings. Existing package/build-hook warnings did not prevent the successful build. Vikunja was unavailable at the start (localhost:3456 refused); no task-board mutation was made there.
