# Participant IPD recording

## Implemented

In Trial Viewer → live participant interview, enter **binocular distance IPD (mm)** and select **Pupillometer**, **Ruler**, or **Participant report** before Begin interview. An optional separate field records the headset's lens-spacing setting, manually read by the researcher. Blank IPD with Not recorded is an explicit missing measurement; neither field is estimated from eye tracking. Values are fixed when the interview is created; check them first. Existing interviews and direct headset-only/quick-test sessions without this intake remain not recorded.

The local intake `session.json` retains a `fit` object. The coded participant assignment transfers it to the headset without transferring interview answers. `SessionConfig` copies it, clears it on participant changes, and writes `participant-fit-v1.json` beside participant data and each run. `trial_summary.json` contains `participant_fit`. Assignment persistence on the desktop and headset retains metadata across a pending-assignment restart.

The flat analysis CSV adds `participant_ipd_mm`, `participant_ipd_source`, and `headset_lens_spacing_mm`. Trial Viewer's joined Final data panel displays these columns and its final-summary export retains them. Missing values export as blank, with participant source `not_recorded`; raw Unity JSON uses availability flags with inactive numeric zero fields. Consumers must respect those flags. Legacy data is not backfilled.

The greater-than-zero/at-most-100-mm check is input/unit sanity, not a physiological eligibility rule. Measurement accuracy depends on the recorded method. This is session metadata, not an additional eye-tracking measurement. Changes in headset fit after intake and recalibration are not inferred by these fields.

## Verification

- Intake/receiver and other viewer Python checks: 97 passed.
- Flat CSV tests: 46 passed, including recorded/legacy IPD and invalid numeric input.
- Full analysis suite: 344 passed.
- Managed voice/session checks passed: assignment copy, run persistence, reset and invalid input.
- Managed headset preparation checks passed.
- Device build-and-run helper attempted; no online Focus Vision was available. APK built successfully in batch mode (the interactive editor was closed), with no install or launch on hardware. APK: `Builds/CodexAndroid/codex_build.apk`; log: `/tmp/ipd-android-build.log`. Unity was reopened afterward.
- Trial Viewer built successfully: `/Users/pedro.poveda/Documents/Trial Review Builds/Trial Review - Participant IPD 20261027.app`. Build log: `/tmp/trial-review-ipd-20261027/build.log`.

No commit or push. No wearer validation or automatic IPD measurement claimed.
