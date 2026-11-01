# Remove retired study voice provider — November 1, 2026

**Implemented.** Scoped cleanup on `experiment/local-jev-20261025`, following automatic-intake commit `541ad28eac87a1bf4f93374567bd8279b89873e3`. No changes to the automatic capture, ten questions, transcript correction/export, human review or clone/cache gates.

Removed `Provider.followup()` and follow-up state/answer flags from `tools/trial-review/participant_intake.py`, its fake-provider method and obsolete test expectations. The provider already read only the ElevenLabs key; no retired key loader remains. Deleted `scripts/voice-prep/e2e_clone_test.py` and `voxtral_smoke_test.py`; neither has a surviving study-code caller. Existing offline ElevenLabs enrollment tests cover their relevant current-provider contract without live service requests.

Added `scripts/check-study-providers.py` and auto-discovered synthetic tests. The guard fails for retired endpoints, key lookups, clients/imports/dependencies in tracked and untracked non-ignored study source/config. It reports paths/line numbers only, never opens `api_keys.json`, skips binary assets and permits historical provider metadata labels. Guard implementation and its own synthetic test are the two explicit self-exclusions. Before deletion it found the retired scripts; the rewritten intake test failed because the old method still existed. Initial guard testing also exposed SDK-dependency and binary-asset handling gaps, fixed before the final passing runs.

Updated the six requested active documents; the old prototype guide now points to the current v3 flow. Historical prototype evidence remains labelled history. Existing work_log entries and meeting records were not rewritten.

## Network and retained compatibility

Full source-level destination inventory and qualifications: [study-network-hosts.md](../docs/study-network-hosts.md).

- Participant audio/transcription/cloning/synthesis: **api.elevenlabs.io:443** (HTTPS/WSS).
- MiniLM: **127.0.0.1:8766**, Mac loopback service via ADB reverse; checkpoints load locally with no cloud fallback.
- Local transfer/control: **127.0.0.1:5037** ADB server and the selected headset's authorized local endpoint (normally LAN IP:5555; USB alternative).
- Draft survey handoff: **ucf.yul1.qualtrics.com:443**, prepared browser link only; publication remains on hold.
- Retained optional development Jev comparisons: **ai-gateway.vercel.sh:443**, spatial state/guidance text. Non-development desktop study intake assignments lock Local MiniLM.
- Retained optional Codex/Astra evaluation: CLI-managed **chatgpt.com / auth.openai.com**; installed binary also contains **api.openai.com / ab.chatgpt.com**, whose use by this invocation was not established. Explicit evaluation sends selected guidance text/state, without intake audio/transcripts. No live call was made. Therefore only the intake-audio/transcript boundary is confirmed ElevenLabs-only; a broader all-study-text claim would be false.
- Initial model-training download: **huggingface.co** and dependency-managed download redirects, outside runtime inference. No model download here. Vendor/OS telemetry and build/publish infrastructure are outside this application-source audit; it is not packet-capture evidence.

Preserved byte-for-byte: `scripts/verify-study-session.py`, `analysis/test_study_validation.py`, `tools/paper-to-podcast`, `tools/interactive-paper-podcast`. The former two read old `provider="mistral"` labels without network requests; the latter two are separate personal tools left for Pedro's decision. The ignored secret file still has an unused `mistral_key` entry. Its contents were not printed or edited; its byte hash was unchanged across the work.

## Verification and installation

[Machine-readable results](receipts/2026-11-01-remove-mistral/test-summary.json), [review](receipts/2026-11-01-remove-mistral/code-review.md), [installation manifest](receipts/2026-11-01-remove-mistral/installation.json).

- **610 Python tests: 609 pass, one optional statsmodels skip.** Desktop 137, analysis 375, scripts 14, script tests 22 (including three new provider guards), local model 45, native timing 17. Existing `.venv` used for desktop/analysis/script tests, local model environment for model tests. The shared-env print helper has an existing unquoted path-with-spaces failure; no environment/dependency changes were needed.
- **Unity: integration 27 PASS, preparation 5, intake 1, desktop check/build 27.** Stored Unity receipts contain verification lines only; full temporary build logs are named in the JSON receipt.
- Nine offline native/C# scripts pass: native gaze, spatial voice, voice isolation, voice enrollment, headset preparation, eye validation, activation, rotational layout and trial timeout. Fake transports/PCM/runtime fixtures only. No microphone, live ElevenLabs, cloning or participant-data tests.
- `git diff --check` and source provider guard pass. Sequential simplification/review follows agents.md; no independent-peer claim.
- Built `/tmp/FollowMyVoice-ProviderCleanup-20261101.app`; no Current/Previous process was running. Removed old Previous, renamed Current to Previous and copied the verified build as `~/Applications/Follow My Voice Study Intake.app`. All **169 files/symlinks** match the build; Previous exactly matches former Current; exactly those two study bundles remain. Bundled worker matches source and no key file is bundled.
- **APK unchanged:** the only headset C# edit is a documentation comment, with no runtime behavior change. Pedro's explicit conditional APK instruction overrides the default Build And Run helper, which would install/launch unnecessarily. Kept the automatic-intake APK already installed by the preceding session; no headset session was interrupted.

Local commit only; no push, Overleaf publish or docs/thesis edits. CLAIM/DONE posted to the coordination board. New progress entry prepended; inherited progress/readiness edits are retained and excluded from this commit. Vikunja was unavailable (127.0.0.1:3456 refused), so no task changed. Wearer rehearsal and approved consent/protocol/survey/retention decisions remain outside this software verification.
