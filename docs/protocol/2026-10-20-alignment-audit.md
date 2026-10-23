# Protocol/runtime alignment — October 20, 2026

The full October 20 handoff supersedes the earlier handoff. **Qualtrics stays on hold.** The later user decision resolves practice: use the first assigned voice (Order A preferred catalog; Order B self-similar). This is an implementation audit, not approval to recruit or a report of participant outcomes.

## Reconciled sources

Read the fresh Overleaf source before editing. Revision `560a0d7` contained the reviewed Section 3 corrections and aggregate propagation; `31c3fe9` adds only the first-block practice voice to four documents. Final revision `f9f49e4` also corrects the schematic scene caption: positions/target identity vary; the implemented object count is fixed. All 19 remote project files match their local counterparts in `docs/protocol/overleaf/`. Original local copies and hashes were retained at `/tmp/protocol-alignment-before-20261020/`. The dedicated Overleaf checkout/index was not reset. No Unity Git commit/push is claimed by this audit.

Claude's Section 3 prose remains, with narrow corrections grounded in code: speech/transition memory has four entries per queue and a 15-second snapshot window; gaze-bearing history has a three-second/384-observation bound and clears on invalidity, source changes or gaps over 100ms. These are different mechanisms. No invented object-entry or blink-preservation thresholds remain. Spatial safeguards are not an absolute guarantee against wrong hints. The Bovo citation resolves through the 62-entry library; acoustics, layout bounds and unique 120Hz observations remain verification work.

## Alignment matrix

| Requirement | Evidence/status | Remaining work |
|---|---|---|
| One practice using first assigned voice | **Implemented:** `ChallengeSet.PracticeTrialCount=1`; practice routing round 0; `VoiceForRound` honors saved order. Schedule first start wall is practice target wall. Study schedule version v6; quick-test v5 origin unchanged. | Wearer verify both orders and practice→measured transition. |
| 40×2; eight trials at each nominal absolute theta | **Implemented:** `ChallengeSet`, seeded schedule and voice-isolation regression checks. | Completed-session pilot; truncation must not be reported as balanced. |
| Final-only questionnaires | **Implemented:** block 40 goes to rest/comfort/tracking checkpoint, no survey gate. Study completion hands off to researcher/external final questionnaires. Development quick tests retain legacy in-headset TLX and are excluded from study analysis. | Qualtrics sync remains on hold; validate external forms/exports when authorized. |
| TLX raw 0–10 vs legacy 0–100 | **Implemented:** explicit range metadata; complete-six native mean, separately named 0–100 rescaling; raw values and missingness preserved. Retrospective block join tested. | Freeze actual export metadata, instrument versions and anchors. Never infer old performance anchors. |
| IMI24 + Guo21 + custom14 | **Protocol proposal:** scoring guide defines subscales/reverse keys; generic CSV joins preserve raw item columns and scope. | Implement/validate automatic IMI/Guo complete-subscale scoring. Do not report a total IMI, Guo21 or custom composite. |
| Exact pre-cue starting theta | **Gap:** logger `signed_theta_deg`/`absolute_theta_deg` are nominal wall geometry (`theta_reference=previous_target_wall_center`); exporter head-bearing geometry is post-reveal. | Freeze origin/cross/target before opening cue, timestamp/frame and signed/absolute exact horizontal angle; degenerate vectors unavailable. Add new fields, retain old meaning. |
| 20 above/20 below cross height | **Gap:** `HeightAssignmentPolicy=unbalanced-floor-scatter-pending-reference-v1`; exported cross-relative height is observed, not a quota guarantee. | Freeze seated-safe ranges/near-zero policy; generate balanced signed schedule, export intended/realized allocation and test both blocks. |
| Active completion | **Implemented:** `StudyTrialClock` runs reveal→correct trigger excluding pauses; completed/incomplete kept distinct. | Verify overlapping pause reasons behave as a union on device, including app focus and researcher/settings pause. |
| 3D target distance | **Implemented exporter:** first tracked head after search onset, with `geometry_reference_search_seconds` and source. | This cannot replace missing pre-cue geometry; confirm tracking validity and coordinate-frame provenance on fresh run. |
| Gaze/controller/selection distinctions | **Implemented:** separate gaze contact, vicinity qualification, controller contact/hold, successful trigger events; instruction count uses actual playback starts. | Validate data in a complete fresh session, including gaps/overlap and headset audio timing. |
| Motion summaries | **Implemented exporter:** adjacent valid motion, tracking/pause gap rejection and quaternion displacement. | Fixed-axis rotation-vector components must not be called Euler yaw/pitch/roll; inspect device coverage. |
| Cloud consent | **Open decision:** current runtime uses ElevenLabs and Jev; old IRB/consent contains offline/local-only language. | Review actual transfers, retention/deletion, access and participant wording before recruitment. Draft document sync is not an IRB amendment/approval. |
| Primary analysis/power | **Protocol proposal:** trial-level log active time with participant clustering, condition, period/order, progression and absolute exact pre-cue theta. | Specify meaningful effect, random effects, theta function and simulation-based N; old four-condition N=48 is superseded. |
| Eye quality/120Hz | **Open validation:** hardware nominal output is not measured distinct delivery. | Native source timestamps/duplicates/gaps, independent spatial validation, drift, calibration acceptance and acoustic/seated pilots. |

Final questionnaires have 65 numeric responses per referenced block (TLX6+IMI24+Guo21+custom14), 130 total. Administer post-exposure SSQ/current-symptom check first, then retrospective Block1 and Block2 sets. Preserve `administration_phase=final` separately from `reference_scope=participant_by_block`, actual voice, timestamp, IDs and instrument version. Four accent items are removed; preferred/perceived gender and voice similarity remain. Practice proficiency, timeout and stopping criteria still need a frozen pilot-informed rule.

## Verification

- Offline voice/schedule suite passes both practice voice orders, exactly one practice, first measured start wall, quick-test reset and 80-trial quotas; voice isolation/content preparation checks pass.
- Shared guidance and gaze-quality shell checks pass.
- Analysis: 38 trial-export tests and 7 TLX scoring/plot tests pass, including mixed declared scales and retrospective block joins.
- Eight relevant standalone/aggregate TeX documents compile with Tectonic. Bibliography-bearing documents emit Tectonic rerun/bbl-consistency notices; resolved Bovo citation/reference is present. Final visual checks cover method text, figures and reference page. No participant outcome or headset accuracy is inferred from these checks.
- Unity RayMenuChecks and the full StudyVerificationChecks suite pass after updating old fixture references to the central state machine and resetting injected-observation caches. The suite also caught obsolete two-practice loops in audio preparation, now fixed in both phrase-library paths. Completion speech no longer directs participants to the old headset TLX form. Android build succeeded in 84.2 seconds (1161.4MB); Wi-Fi streamed install returned Success and launch Status: ok. Package update 2026-10-20 20:58:15; running process28196. This verifies installation/launch only; wearer acceptance remains pending.

## Focused wearer acceptance

1. Assign Order A: exactly one preferred-voice practice, then 40 measured preferred trials. Repeat setup with Order B: exactly one clone-voice practice. Practice does not count in measured totals.
2. Verify the first measured cross uses the practice target wall; inspect saved v6 schedule and nominal angles.
3. At trial40: break/comfort/tracking checkpoint only; continue switches voice without a questionnaire. Active clock remains stopped during the break.
4. At trial80: search ends and researcher/final-questionnaire handoff appears; no legacy workload slider in study mode. Quick tests stay development-only.
5. Verify final export references both actual block voices and final administration; missing responses remain missing. Do not start confirmatory collection until geometry, height, consent and pilot criteria above are resolved.
