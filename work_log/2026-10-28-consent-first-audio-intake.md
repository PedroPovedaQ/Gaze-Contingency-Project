# Consent-first audio intake — 2026-10-28

## Implemented

Fresh session continuing desktop thread `01a166b4` on Pedro's instruction, with the required initial context and coordination reads. Preserved the coherent inherited working tree at local commit `03c5a14` before new changes. No pushes, Overleaf publication or `docs/thesis/` edits.

See [the operator guide](../docs/guide/10-audio-intake.md) for exact questions, researcher controls, all gates, fallback wording and storage provenance. Flow: reserve P###/preassigned A or B → signed-consent attestation → audition/confirm Selected Voice → River questions and tone → four-item researcher eligibility review → baseline speech, clone and cache → complete verified transfer → wearer session confirmation/audio acceptance → three fit dots/five verification dots/activation/centering → practice → two 40-trial blocks → external final comparison survey.

River (`SAz9YHcvj6GT2YYXdXww`) remains outside the twelve study voices. Clone admission requires 30 s estimated speech and 50 ASR words, excluding empty/clipped answers. These are engineering gates, not validated voice-similarity criteria. The old reading passage is an explicit insufficient-speech fallback. All answer inputs and generated audio manifests are participant/session/consent-bound, hash recorded, resumable and private; the headset gets generated audio and minimal linkage, not raw answers or consent references. Never overwrite an existing source recording.

Study sessions lock Local MiniLM in both blocks. Bundle `20261026-minilm-reassurance-v8`, policy `jev_adaptive_assistance_v4`, authored synthetic policy data 2,240/448/448 train/validation/test. Existing deterministic overrides remain. No model retraining performed; real-device replays are separate evaluation evidence.

The supplied final survey is `SV_3retnpdBMwBx5bw`, `v2026-10-28c DRAFT`, unpublished. Completion emits a unique run handoff containing the eight supplied URL-encoded embedded fields, with A/B block mapping. Trial Viewer retrieves matching participant/session handoffs. Status stays `awaiting_external_survey`, `responsesCollected=false`; development handoffs are labelled.

## Verification and receipts

- Desktop: `python3 -m unittest discover -s tools/trial-review -p 'test_*.py'`: **121 passed**, including mocked prospective consent, nine-question progression, eligibility stops, fallback, clone snapshots, private assignment, participant isolation, exact voice cache keys, local/remote corruption and transfer failure gates. Synthetic fixtures only; no real speaker enrolled.
- Analysis: `uv run --with PyWavelets python -m unittest discover -s analysis -p 'test_*.py'`: **351 passed**. Default interpreter initially lacked PyWavelets; the isolated rerun passed without altering repository dependencies.
- Headset connection harness: eight tests passed. Voice-isolation, headset-preparation, activation and eye-validation shell checks passed. Unity StudyPreparationChecks passed.
- Unity **StudyIntakeChecks passed**, including both condition orders, wrong-participant/hash/template rejection, missing decoded sidecar, incomplete phrase set, legacy assignment rejection, eligibility gate, study MiniLM lock/development provider freedom and survey mapping/escaping. Uses temporary synthetic fixtures, not participant data.
- Desktop Unity build: `TRIAL_REVIEW_BUILD_PASS`, output `~/Applications/Follow My Voice Study Intake.app`; built-in replay verification passes. Inspected the actual `--intake` screen. This verifies rendering; it does not claim a completed microphone/participant interaction.
- Live provider smoke: River metadata retrieved and one generic question synthesized (4.690431 s, 76,111 bytes, SHA256 `6b0b7542742023a7153076ba50f5a785255c2dfe510fbad2dc6f4ae1e21d5202`). **No participant audio uploaded or voice cloned.**
- Initial APK built/installed/launched over Wi-Fi on VIVE Focus Vision, MiniLM reconnect succeeded. Runtime template observed: 566 phrases, 12 catalog voices, River excluded, 1,132 two-voice cache jobs; SHA256 `a497a76e69b14ab848c2ab246915f68448faa496d84c88a10af5c75aa9178317`.
- Final Android: **Build Result Success**, 96.1 s, 1,167.9 MB; wireless install **Success**, launch **Status: ok**, package update `2026-10-28 00:41:52`, live PID 11512. MiniLM service reports ready and reverse `tcp:8766 → tcp:8766` is present. Device is unworn/paused, intake available. Required `./scripts/refocus-unity-and-build-device.sh` invoked; it selected Wi-Fi `192.168.1.208:5555` and dispatched the Unity menu. Earlier menu dispatch failures were resolved through the native Unity menu; no ADB server reset.

## Remaining participant-facing validation

**Open decision / validation:** approved consent wording/version and retention handling; a consented wearer rehearsal covering microphone onset/clarity, ASR review, actual provider clone quality, full 1,132-clip generation/transfer, two-voice acceptance, gaze Retry/Skip and full practice/80-trial completion; and publication/verification of the supplied Qualtrics draft. No wearer was available overnight and no new wearer accuracy, cloning adequacy, questionnaire responses or study-readiness approval is claimed. The headset may pause when unworn; Mac wake and ADB remain necessary for MiniLM.

## Final artifacts and commits

- `03c5a14`: preserved inherited standard gaze/guidance/telemetry/MiniLM work.
- `85bcd02`: consent-first audio intake, cache/transfer, MiniLM study lock and final-survey handoff.
- [Deployment receipt](receipts/2026-10-28-audio-intake/headset-release.json), [filtered Android build log](receipts/2026-10-28-audio-intake/android-build.txt), [desktop test receipt](receipts/2026-10-28-audio-intake/desktop-tests.txt), [generic River synthesis receipt](receipts/2026-10-28-audio-intake/provider-smoke.json).
- APK SHA256 `59585bee44f98e0f56d250b2147fff58cffee8488acc28e3b7179285a270f3b3`; `Builds/CodexAndroid/codex_build.apk` (ignored build artifact). Desktop app: `~/Applications/Follow My Voice Study Intake.app`.
- Build diagnostics are **not warning-free**: VIVE OpenXR package callbacks report `GetOpenXrExtensionInfo` and `ModifyAndroidManifest.AddOpenXRFeatures` NullReferenceExceptions; the build nevertheless succeeds and installs/launches. These package callbacks also occur in prior builds. No new C# compiler error or intake-check failure remains.
- No real participant voice cloned; full-library generation/transfer is tested with fake provider/remote fixtures, exact real runtime template keys and integrity failures. Actual provider smoke covers only the generic intake voice. No participant/ASR/voice-quality validation is inferred from these checks.
