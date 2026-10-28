# Eye validation exercises — 2026-10-27

## Implemented

Isolated Trial Viewer-controlled validation with spatial/drift dots, fixation and saccade targets, blink/head-turn/pursuit/software-gray/gaze-angle controls, counterbalanced arithmetic and difficulty responses, and sandbox selection. Session-bound commands, revision checks, participant/IPD provenance, raw gaze and controller samples, stimulus/event markers, periodic manifests, interruption/error receipts, and separate analysis exports. Completion never sets physiological validation true.

Controller follow-up: the first hardware pilot exposed that study setup had not yet created ControllerRaySelector. Validation now creates a temporary selector when needed, releases only what it owns, and samples after the selector's late update while retaining early stimulus updates before gaze sampling.

## Checks and deployment

- Earlier full suites: 347 analysis tests and 102 viewer Python tests passed.
- Managed schedule/counterbalance/stale-command checks, five bridge validation tests and three analysis validation tests passed, including after the controller follow-up.
- Headset preparation checks passed.
- Initial runtime Android build succeeded (87.2 seconds), installed and launched on FA5AR3N00385. Controller follow-up build succeeded (168.8 seconds), installed over Wi-Fi with Success and launched with Status: ok. New process 20288; headset remains paused before a fresh setup heartbeat.
- Desktop v4 built and opened: `/Users/pedro.poveda/Documents/Trial Review Builds/Trial Review - Validation Exercises 20261027 v4.app`.
- Sequential manual review fixed terminal error preservation, participant-change IPD carryover, modal background input, live-controls clipping, and pre-study controller recording. No independent reviewer or full formal CE review is claimed.

## Hardware pilot evidence

Both USB and Wi-Fi (192.168.1.208:5555) connected. Development participant P999999; IPD left unknown. Drift command acknowledged. Validation ID `1a3b60dc7816417e92434e3125c5ee55` remained at instruction stage, then ended `interrupted: Headset paused`. All three recording files pulled; manifest retained 2,051 gaze rows and raw-source references. No stimulus phase completed.

Local copy and audited report preserved at `/Users/pedro.poveda/Library/Application Support/Follow My Voice/Validation Pilot 20261027/audits/first-interrupted-pilot/` (also `/tmp/eye-validation-pilot-20261027/`). Report preserves interrupted status and `measurements_validated: false`. Headset mirror showed setup, so actual instruction visibility and Begin interaction still need wearer confirmation. Do not describe this as a successful physiological validation.

Live output: `/Users/pedro.poveda/Library/Application Support/Follow My Voice/Validation Pilot 20261027`. The prior Documents/Trial Review Live location stalled on a stop-request filesystem operation; old archive preserved. New local folder is connected and catching up historical recordings.

## Open physical checks

1. Confirm instruction panel appears and Begin responds.
2. Complete drift check and verify tracking validity, target markers, current-frame controller records, and synchronized local files.
3. Run spatial, saccade, control, arithmetic and search exercises with the wearer; inspect traces and independent labels before any measurement acceptance claim.
4. Native gaze/pupil capture timestamps currently report unavailable on this hardware sample; do not infer capture rate from render rows.

No commit, push, manuscript changes or Overleaf synchronization.

## Completed wearer drift pilot

Session `85bb47a4d353414d9e10a4de6a54ca6c`, participant code P032, completed all five 2.5-second drift targets. Begin event source was `headset_button`, confirming in-headset interaction. Captured 3,102 running-stage gaze rows flagged valid and 2,075 controller rows across the whole session (1,695 available/tracked). These are recorded rows, not unique sensor samples or an inferred hardware sampling rate. Mean used-ray target errors by position were 4.24°, 7.01°, 5.77°, 5.06°, 6.37°; include transition/settling effects and must not be treated as calibrated fixation accuracy. Native gaze/pupil timestamp availability remains false.

Files and hash-based audit preserved under `~/Library/Application Support/Follow My Voice/Validation Pilot 20261027/audits/`, folders `validation_20261027T131111_85bb47a4d353414d9e10a4de6a54ca6c` and `drift-85bb47-report`. Report status complete, measurements_validated false. Next: headset calibration/fit, repeated spatial check, then saccade/control/arithmetic exercises and independent analysis.
