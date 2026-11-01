# Automatic fit metadata and advancing participant codes

## Implemented

Local work on `experiment/local-jev-20261025`, following `82231bf`. This reverses the October 27 manual-only IPD decision. Reserve no longer asks for IPD. After accepting the desktop assignment, the headset passively captures fit metadata while worn with fresh valid eye tracking during voice setup, ending before the existing calibration callback. No gaze ray, calibration, guidance, trial or eligibility behavior changes.

The first source is Unity OpenXR head-device left/right eye positions. If unavailable or implausible, the active XR display's stereo inverse-view origins are used with unit camera scale. Their distance is **headset view separation**, not a verified physical lens readout. A separate **wearer eye-origin separation estimate** uses the existing HTC per-eye gaze origins in the common base space. It is not validated clinical IPD; the runtime may model the origins, and independence from lens settings is unknown. The code does not accept Camera.stereoSeparation defaults as device measurements.

Both values retain source, method, UTC, number of acquisition snapshots, duration and range. At most 20 snapshots/s are acquired. A median requires at least 40 valid snapshots spanning two seconds, 40–85 mm values and a range no greater than 1 mm; gaps over 0.2 s and source changes reset the candidate window. These are authored engineering settings, not validated accuracy or physiological limits. Snapshots need not be statistically independent. Missing, unstable and implausible values warn without blocking.

The fit record, setup JSONL trace, per-capture result, run sidecar, trial summary, live status, matching desktop intake and additive CSV columns preserve the metadata. Existing manual fields remain unchanged. Optional manual fallback appears after setup only for unavailable automatic sources, records its honest source and latest manual-entry UTC, and is one researcher entry rather than a median. Session ownership and available automatic sources are checked on both sides.

The VIVE OpenXR 2.5.1 eye-tracker API exposes per-eye validity and gaze-pose origins but no adjustment-run result was found. Initial read-only `getprop`, all three `settings list` namespaces and `dumpsys display` completed on the Focus Vision without a usable IPD/status key. A later repeat, including vrmanager, could not reach the disconnected device; no reconnect or setting change was attempted. See [probe receipt](receipts/2026-11-01-automatic-fit/device-api-probe.json), [HTC eye-tracker API](https://developer.vive.com/resources/openxr/unity/tutorials/face-data/getting-the-data-of-eye-tracker/) and [HTC's lens-adjustment UI documentation](https://www.vive.com/us/support/focusvision/category_howto/adjusting-the-ipd-on-the-headset.html). This search does not prove that no vendor interface exists.

`autoIpdStatus` remains unavailable. A stable view median changing at least 0.5 mm from its initial stable baseline records **change observed**, signed magnitude, source and UTC. Two further seconds of stable observations without a qualifying change yield **no change observed**; otherwise observation is insufficient. Source switches establish a new baseline. Neither label proves that automatic adjustment ran or did not run. Adjustment could precede acquisition or finish at the same value. No automatic adjustment command is sent.

Reserve now calculates the next study P### from session folders, persistent reservation ledger, catalog/history, local archive metadata and headset knowledge. The headset's next-unused suggestion is a floor; its active code is treated as used. Reservations are checked again under the existing file lock; stopped/withdrawn/deleted session codes remain reserved. Numeric aliases count as duplicates. Refresh runs while Reserve is open, on study/rehearsal toggle and after Finish Here/create another session, preserving deliberate field edits and displaying duplicate warnings. Unreadable inventories fail visibly rather than silently reverting to P001.

Development rehearsals use the separate, visibly labelled sequence beginning **P900000000001**, accepted by the existing 3–12 digit validators. They do not advance study numbering. Existing low-number development records remain readable; explicit study use takes precedence over conflicting older development metadata. Actual headset assignment checks cover the 12-digit code.

## Verification and artifacts

- **622 Python tests: 621 passed, one optional statsmodels skip**: desktop 148, analysis 376, scripts 14, script tests 22, local model 45, native timing 17.
- Actual Unity: study integration 27 PASS markers, preparation 5, intake/fit 2; desktop verification/build 27. Actual Unity empty and populated fit JSON round-trip through Python validation.
- Nine offline C#/native scripts pass: voice isolation, headset preparation, activation exercise, eye validation, rotational layout, spatial voice, trial timeout, voice enrollment and native gaze (including eight sanitized C++ suites).
- Synthetic UI evidence: 56 states each at 1440×900 and 1280×800, including study/rehearsal Reserve, duplicate code, automatic values, observed/no observed change, missing values and fallback. Captures contain no participant evidence. Paths/hashes are in [screenshots.json](receipts/2026-11-01-automatic-fit/screenshots.json).
- Desktop output: `/tmp/FollowMyVoice-AutoFit-Release-20261101.app`. Android output: `Builds/CodexAndroid/codex_build.apk`. Build and artifact receipts are in [the receipt directory](receipts/2026-11-01-automatic-fit).
- [Sequential review receipt](receipts/2026-11-01-automatic-fit/code-review.md). No independent-peer review claimed.

**Installation is waiting.** The existing Current desktop app remained open (PID 5228); rehearsal inactivity was not confirmed. No quit/replacement/relaunch of that app, APK installation/launch or headset state-changing command ran. Separate capture-only processes used synthetic data and bypass microphone, jobs, live polling and provider calls. No microphone, ElevenLabs, cloning or participant-data edits/tests were performed. No push, Overleaf publication or docs/thesis edit. The build-and-run default was overridden by Pedro's explicit rehearsal constraint. Existing mixed `progress.md` and inherited readiness receipts remain outside this commit; this session's progress entry is prepended. Vikunja was unavailable (localhost:3456 refused).

## Open decisions and thesis handoff

1. Replace manual IPD in Reserve and the October 27 assertion that IPD is never read with passive pre-calibration headset collection; retain historical data descriptions as history.
2. Describe view geometry and the separate HTC eye-origin estimate with their exact sources and limitations. Do not call either validated anatomical IPD or assert agreement with the physical lens setting. No real-device values or lens readout comparison were obtained for this implementation.
3. State the median/freshness/stability settings, provenance and storage/export fields as authored engineering choices, with manual fallback and nonblocking missing-value warnings.
4. Replace any assertion that auto-IPD completion is detected with unavailable status and observed-change/no-change/insufficient-observation semantics, including magnitude/time/source and their limits.
5. Update the procedure and intake diagrams for automatically advancing editable study codes, persistent duplicate protection, Finish Here refresh and the separate P900000000001+ rehearsal namespace.
6. Cite this local commit and the synthetic screenshots as software evidence only. No wearer, lens agreement, clinical accuracy or adjustment-detection accuracy claim is justified. The 96-trial task, calibration/guidance and analysis results are unchanged.
7. Installation and a labelled development hardware check remain pending until no rehearsal is active. Compare view/eye values with the visible lens readout; anatomical accuracy would additionally require repeated fits and an independent instrument. Lens agreement alone would not validate the eye-origin estimate.

Authoritative detail: [automatic-headset-fit.md](../docs/research/automatic-headset-fit.md). The coordination DONE post records the resulting commit and final build status.
