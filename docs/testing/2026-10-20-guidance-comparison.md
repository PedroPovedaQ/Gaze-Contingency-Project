# Guidance ordering, sequence timing, and highlighted-object reference

**Implemented software changes and offline comparisons, October 20, 2026.** Checkpoint before this work: `7299312`. Earlier comparison: `492be9d`. Source hashes and repeatable commands are in [the paired pilot report](../../analysis/pilots/20261020/README.md).

## Controller ordering

The previous state machine suppressed directions only when gaze was also inside the target's winning vicinity. A valid controller hit outside that vicinity could still allow a return/down cue while the separate one-second pointing hold qualified. Some final playback checks also treated “pointing” as controller **and** nearby gaze, leaving the same gap when a delayed response arrived.

The fix suppresses search directions immediately on fresh controller-on-target evidence, rechecks that evidence before playback, and fades already misleading search speech over 150 ms. It does not grant a pointing confirmation early. Gaze acknowledgment no longer consumes the independent pointing acknowledgment. Selection still requires a correct trigger press.

| Identical input comparison | Earlier | Checkpoint | Fixed |
|---|---:|---:|---:|
| Controller-conflicting action episodes in 20 scripted trajectories | 7 | 7 | 0 |
| Contradictory starts in the synthetic playback adapter | 6 | 9 | 0 |
| Conflicts in 75 frozen recorded-request probes | 4 | 4 | 0 |

The first two rows use simulated trajectories and clocks, not measured headset or acoustic timing. The third counts probes, not participant episodes. The adapter executes production policy classes but does not execute the complete Unity audio lifecycle. These results establish a policy improvement, not a demonstrated change in human behavior.

## The remaining two-second delay

Measuring only request-to-playback missed time spent waiting before a request was emitted. The evaluator now also searches for the first explicit qualified confirmation action in fresh recorded central-state samples. Missing qualification evidence remains unknown.

Run 25 contains 21 confirmation episodes. Eighteen have recorded qualified requests followed by playback: median 60 ms, p95/maximum 86 ms, none over the **1-second engineering review threshold**. That is the narrower request metric.

Only six episodes have sufficient earlier observed qualification evidence: median **560 ms**, p95/maximum **1.957 seconds**, with **two** over one second. Fifteen lack enough evidence for this earlier timing basis. These are software playback timestamps, not sound reaching the participant's ears.

| First observed qualification → software start | Known / total episodes | Median | p95 / maximum | Over 1 s |
|---|---:|---:|---:|---:|
| Gaze confirmation | 5 / 18 | 731 ms | 1,957 ms | 2 |
| Controller confirmation | 1 / 3 | 388 ms | 388 ms | 0 |

| Recorded sequence | Qualification | Existing speech ends | Request | Software playback | Total |
|---|---:|---:|---:|---:|---:|
| Gaze confirmation after incorrect-selection feedback | 51.526 | 53.395 | 53.398 | 53.483 | 1.957 s |
| Gaze confirmation after “Right area. Look higher.” | 113.813 | 114.643 | 114.901 | 114.984 | 1.171 s |

The first waited about 1.869 seconds for “Keep following the guidance and try another object,” then roughly 2 ms before request and 86 ms to software start. The second waited about 830 ms for existing speech, then 258 ms before request and 83 ms to start. Neither episode waited on Jev. Avoid claiming all two-second delays have been removed: finishing a protected sentence, qualification itself, tracking recovery and actual audio readiness can legitimately cost time, but the appropriateness of each wait requires inspection.

**Implemented response:** incorrect-selection feedback is shortened to “Try another object.” Qualified acknowledgment readiness uses the upcoming phrase's 100 ms gap, rather than inheriting the previous ordinary phrase's 250 ms gap. This removes 150 ms of unnecessary configured post-speech waiting; the new phrase's actual prepared-audio duration and end-to-end benefit still need a fresh wearer recording. Neither change claims zero latency.

Run 25's 78 Jev requests include 47 successful responses, three timeouts and 28 cancellations. Cancellations are separate from failures because state changes can deliberately invalidate a request. A separate 12-request Mac-to-Vercel probe returned 12 valid choices, median 163 ms and maximum/p95 498 ms; one missed the Unity catalog's 450 ms deadline. This small host probe does not estimate the headset's durable failure rate.

Recorded Jev calls per Run 25 trial were **10, 23, 15 and 30**; the last trial is unfinished. Three timeouts out of 78 requests is 3.8% for this recording, not a general service reliability estimate. Cancellation rate is 28/78 (35.9%). Existing telemetry cannot completely attribute every interval to network, scheduling, audio readiness and acoustic output, so missing components remain unknown.

Each sampled hard-eligible catalog cell contains only five distinct phrases. Small and full eligible sets are therefore identical. Testing a medium set of 15 requires more valid authored options; geometry was not widened to manufacture a comparison.

## Run 26: “lower” beside a highlighted pill

The screenshot corresponds to approximately study time 14.83 seconds. The target red cube (`r00_o90`) is 5.53 cm above the blue capsule (`r00_o97`). The capsule is the recorded nearest gaze-vicinity winner, not an exact gaze-ray hit.

The runtime and viewer brighten the nearest unambiguous object inside the padded gaze region. Meanwhile, the directional calculation used the raw gaze ray. At request time (about 13.48 s), the target was 18.5° below that ray. At software playback (13.928 s), it was 4.29° below; at the screenshot it was 2.75° below. The raw sign is consistent with “lower,” but the highlighted capsule creates a conflicting visible reference.

This is a visual/spoken-reference mismatch, not evidence that the eye-tracking axis is reversed. The data cannot establish where the participant intended to fixate or validate calibration. Preserve raw gaze and distinguish exact hits from vicinity winners when reviewing this case.

**Implemented response:** a unique highlighted distractor on the same wall can suppress a contradictory vertical cue. The comparison uses target and distractor elevation from the same eye origin, so differing depth cannot reverse the apparent relationship unnoticed. A diagonal retains its valid horizontal component; a purely vertical contradiction stays quiet rather than claiming “right there.” Queued and historical return cues receive the same check. Raw gaze, history and confirmation thresholds remain unchanged; the reference ID, relative elevation/height and suppression reason are recorded separately. This is a conservative conversational policy requiring wearer validation, not a calibration correction.

## Advisory sequence review

The updated Astra rubric receives recent actual speech, state transitions, actions and the nearest available central state within 250 ms before software playback. Local confirmations are now included in a separate review-case file; the counterfactual benchmark retains only exact recorded Jev requests.

A bounded 12-case first pass flagged two proposed return directions during valid controller-on-target evidence. Both requests were cancelled without actual playback; they are not counted as wrong spoken hints. A four-case follow-up included the two delayed local confirmations and two actually played return cues. Astra supported the latter two, judged the 1.957-second wait defensible while feedback was speaking, and marked timing of the other delay unknown with the provided evidence. The deterministic one-second threshold still flags both delays. Interruption appropriateness remains unknown in these cases. These limited advisory reviews are not whole-run human-perceived correctness or an accuracy score.

## Remaining sequence evidence and limits

Run 25 contains 64 spoken lines and three success chimes. Fifty-six spoken starts link to recorded decisions, and the proposed/played text matches for all 56. All 56 have a central-state sample within 250 ms before onset. Nine start with valid controller-on-target evidence; all nine are confirmations or trigger reminders, not directional cues.

There is nevertheless an ongoing-speech overlap: “Go back down and right” starts at 46.816 s, then valid controller-target hits are recorded at 47.258–47.382 s while gaze remains elsewhere. This is an already-playing cue, not a contradictory cue beginning after the hit. The new fade guard addresses that class. The old record does not establish the exact acoustic overlap.

| Requested outcome | Supported result / remaining uncertainty |
|---|---|
| Wrong-direction and stale hints | Request-geometry checks: 24 pass, zero fail, seven unknown. This is not a complete actual-playback accuracy audit; continuous geometry at all spoken instants is not established. |
| Contradictory ordering | No linked directional onset among the nine controller-on-target starts; one observed ongoing direction/controller-entry overlap above. Paired policy tests reproduce and remove the eligibility defect. |
| Repetition or unnecessary speech | Zero exact consecutive identical spoken phrases within each trial, among 64 lines and 41 unique texts. Semantic repetition and unnecessary speech are not ruled out by text inequality. |
| Missed confirmations | Three gaze-confirmation episodes disqualify before playback. They remain unknown/uncompleted, not three confirmed missed-feedback failures. Full recall needs independently reconstructed eligibility across all observations. |
| Excessive silence | Of 49 adjacent speech-end → next-start intervals with same-trial active-search coverage and no sample gap over 250 ms, median silence is 691 ms; three exceed a 3-second screening threshold: 3.098, 3.921 and 3.862 s. Nine other intervals are excluded for missing/inactive coverage. A threshold screen does not prove silence was inappropriate. |
| Interruption appropriateness | No recorded fade-start events in this run; three audio cancellations include setup reset and selection boundaries. Astra marks interruption judgments unknown. New fade behavior needs a wearer test. |

The three screened silent intervals are 62.576–65.675, 84.792–88.714 and 137.532–141.394 seconds. All have valid gaze in their recorded samples. The 3-second silence screen is exploratory, not a perceptual threshold or a rule to fill every pause with speech.

## Live preparation status

A separate small heartbeat reports preparation phase and measured progress before any replay header exists. It does not start a trial or alter the trial clock. The viewer requires changing heartbeat evidence and expires it after five seconds; a stale file cannot claim current loading. Provider-side voice creation has unknown progress rather than a fabricated percentage. Setup errors and app pause are separate states.

## Focused wearer checks

1. Point at the correct target while looking elsewhere, then bring gaze nearby. No contradictory direction should begin during a valid controller hit; no pointing acknowledgment should occur before the hold qualifies.
2. Leave and reacquire the target, including a blink. Looking, pointing and correct trigger selection must remain separate.
3. Recreate Run 26: highlight a distractor just below or above the target and inspect the spoken vertical component, including a queued hint during rapid movement.
4. Select an incorrect object, then immediately find the correct one. Review first observed qualification, prior speech end and actual playback separately.
5. Start quick-test preparation and watch the live viewer before the first round. Check progress, pause, reconnect and setup failure; missing telemetry must not look like current loading.

**Open validation:** post-change wearer sequences, acoustic onset, calibration accuracy, naturalness and behavioral benefit. Astra's sequence ratings are advisory and cannot establish human-perceived correctness. Recorded counterfactual probes do not show how a participant would react to different speech.
