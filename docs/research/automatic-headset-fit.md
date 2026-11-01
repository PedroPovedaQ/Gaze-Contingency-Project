# Automatic headset fit metadata

## Implemented

This replaces the October 27 manual-only Reserve decision. Reserve has no IPD fields. After the transferred participant session is confirmed, passive capture runs through voice setup, ending before the existing calibration callback. It does not change calibration, gaze rays, alignment, guidance, eligibility or trial behavior.

Two measurements remain distinct:

| Value | Source and interpretation |
| --- | --- |
| Headset view separation | Distance between Unity OpenXR head-device `CommonUsages.leftEyePosition` and `rightEyePosition`, multiplied by 1000 for mm. If unavailable/implausible, use inverse view-matrix origins from the active `XRDisplaySubsystem`'s first two render parameters, only with stereo rendering and unit camera scale. This describes runtime view geometry. Agreement with the physical lens setting is unverified. `Camera.stereoSeparation` defaults are deliberately not accepted as hardware readings. |
| Wearer eye-origin separation estimate | Distance between valid left/right `XR_HTC_eye_tracker` gaze-pose positions already acquired by `SharedGazeStream`; no additional gaze query or ray correction. This is an unvalidated eye-origin estimate, not clinical binocular distance IPD. The runtime may model these origins; independence from view/lens settings has not been established. |

The installed VIVE OpenXR 2.5.1 package exposes `GetEyeGazeData`, per-eye `isValid` and `gazePose.position` in a common base space. Its eye tracker API does not expose an IPD adjustment-run result. See [HTC's eye-tracker API documentation](https://developer.vive.com/resources/openxr/unity/tutorials/face-data/getting-the-data-of-eye-tracker/). HTC documents automatic lens adjustment in the [Focus Vision settings UI](https://www.vive.com/us/support/focusvision/category_howto/adjusting-the-ipd-on-the-headset.html); that does not establish an application-readable completion flag.

Read-only checks on the connected Focus Vision on November 1 returned no usable IPD/auto-adjustment key in `getprop`, `settings list system`, `secure`, `global`, or `dumpsys display`. A later repeat, including `dumpsys vrmanager`, could not reach the disconnected device. No hidden vendor interface is claimed absent universally. No settings screen was opened and no lens-setting comparison was performed because a rehearsal could be active.

Capture polls at most 20 times/s. Median acceptance requires at least 40 valid acquisition snapshots spanning at least 2 seconds (rolling retention 2.2 s), all within 40–85 mm and with max-minus-min range at most 1 mm. Gaps over 0.2 s, invalid samples or source switches reset the candidate window. Eye validity, fresh HTC delivery under 0.1 s, application focus, valid head pose and user-presence when available gate accepted estimates. View values can still be logged diagnostically when eyes are invalid. These are authored engineering sanity/stability thresholds, not physiological eligibility or validated accuracy limits. Snapshot counts do not establish sensor cadence or statistical independence.

`autoIpdStatus` remains `unavailable`. A stable view median differing from its first stable baseline by at least 0.5 mm produces **change observed**, retaining the largest signed deviation and observation UTC. A further two seconds after a baseline with stable observations and no qualifying change produces **no change observed**. Otherwise the result is **insufficient observation**. Changing API source resets the comparison baseline. Neither change label determines whether automatic adjustment ran: it could precede acquisition, complete at the same setting, occur during missing tracking, or reflect another source of view changes. No adjustment command is sent.

The fit record includes sources, aggregation method, last-window UTC, snapshot count, duration and range; capture UUID/start/end, polling count, change label/size/time and warnings. `GazeData/<code>/fit-setup-<capture UUID>.jsonl` retains the setup trace and `fit-result-<UUID>.json` its result. `participant-fit-v1.json` retains additive fields beside participant data and in each run; `trial_summary.json.participant_fit` and Trial Review exports retain them. Live status sends only fit metadata and identifiers. Desktop updates require a matching UUID and code and do not write a withdrawn intake.

Missing, unstable or implausible automatic readings produce researcher warnings without blocking the session. Earlier accepted readings retain their original timestamps. After setup, missing sources expose optional manual fallback in Trial Review: participant IPD with pupillometer/ruler/participant-report source, and a separate researcher-read lens setting. The headset validates participant ownership and rejects fallback for a source already available automatically. Manual values retain their existing fields and add entry UTC; old records remain unchanged. A manual fallback is one researcher entry, not a sampled median.

Exports retain legacy `participant_ipd_mm`, `participant_ipd_source`, `headset_lens_spacing_mm`. New families `headset_view_separation_*` and `wearer_eye_separation_*` add mm/source/method/UTC/samples/window/range; fit capture/warning and auto-IPD/change fields are separately named. Missing legacy/new values stay blank, never measured zeros.

## Open decision / validation

No wearer measurement, clinical accuracy, lens-readout agreement or auto-IPD detection accuracy has been measured by this change. Deploy only when no rehearsal is active, then compare stable view/eye estimates with the visible lens-setting readout under a labelled development protocol. Repeated fits and an independent IPD instrument are needed before claiming anatomical accuracy. Lens/view agreement alone would not validate the eye-origin estimate.
