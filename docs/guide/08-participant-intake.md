# Participant intake from Trial Viewer

**Implemented (2026-10-25):** Live mode → New participant session opens the desktop interview. This creates a participant session; it does not start a timed search trial.

1. Enter an unused coded participant ID. A connected headset suggests its next available code. Choose **A: self-similar first** or **B: neutral first**, and the neutral study voice.
2. Begin the interview. **River** reads the prompts. River is outside the study's twelve-voice catalog and is never assigned as the experimental neutral voice.
3. Confirm the participant's agreement to recording/cloning, select the microphone, and press **Record participant answer** only while the participant speaks. Prompts and replay audio stop before recording starts. Press **Stop and submit answer**. The microphone never starts automatically. Each recording is bounded to two minutes; use headphones to avoid acoustic spill from other applications.
4. The core prompts cover preferred name/nickname (optional), VR experience, eye-tracking/voice-assistant experience, and optional comfort/accessibility information. Mistral can ask one follow-up per experience topic. Questions, transcripts, skipped answers and audio references are saved locally.
5. Cloning starts in a separate process after 30 seconds of submitted answer audio, while the remaining interview continues. It uses a snapshot of submitted answers at that point; later answers do not silently retrain the clone. For shorter interviews, use **Prepare voice from recorded answers**. Provider requirements still apply.
6. Once the interview and clone are complete, send the session to the headset. The wearer confirms the code and order, listens to the prepared study samples, then follows eye alignment and the existing experiment flow. Assignment cannot replace an active trial.

**Implemented: storage and ownership.** `~/Documents/Trial Review Intake/<session UUID>/` holds the private desktop interview. The directory is owner-only; API keys remain outside the app at `~/.config/follow-my-voice/api_keys.json`. The worker reads `elevenlabs_key` and `mistral_key` from this existing local configuration. No credentials are packaged by the desktop build.

Answer audio is sent to ElevenLabs for transcription and cloning. Only VR and voice-assistant experience answers are sent to Mistral for optional follow-up generation. Preferred name and comfort answers are not sent to Mistral. These files are separate from replay exports. The headset receives only schema, session UUID, participant code, A/B assignment, neutral voice selection and cloned voice ID. `desktop-session-id.txt` and session metadata link headset runs to the desktop session without copying interview content.

**Implemented: failure recovery.** Closing the panel leaves workers running. Reopening the panel resumes the saved session. A failed spoken prompt can be retried; retrying a saved answer cannot count it twice. An uncertain clone response is never automatically retried because the provider may already have created the voice. The researcher can explicitly retry; the button warns that a duplicate provider voice may result. Local recordings remain intact. The headset preserves existing participant assignments and rejects conflicting participant/order data.

**Implemented: provider-trusted study audio.** `SessionConfig.ValidateVoiceTranscript` defaults to false for study and development runs. Transcript matching, filler/quarantine admission and loudness rejection are bypassed. Transport, decode and sample-buffer errors can still fail because an invalid file cannot play. Volume normalization and provenance remain, with `provider-trusted-v1` recorded instead of a fabricated verification pass. Interview transcription is for interpreting answers; it is not synthesized-audio validation.

**Implemented: eye alignment.** The setup panel and calibration targets use horizontal head orientation at current eye height, so looking down when setup opens no longer places the task on the floor. The targets remain fixed in the room during measurement.

**Protocol proposal:** Use this interview in the participant procedure after the study's consent process. The implemented question wording, provider processing and storage do not establish ethics approval.

**Open decision:** Confirm the final demographic coding, retention/deletion schedule and whether these interview questions/data handling require an approved protocol amendment before collecting study participant data.

## Verification and development

- `python3 -m unittest discover -s tools/trial-review -p 'test_*.py'`
- `./scripts/test-voice-isolation.sh`
- `bash scripts/test-headset-preparation.sh`
- Unity: Tools → Codex → Check Entry Eye Alignment
- Desktop: `python3 tools/trial-review/build.py --project /tmp/trial-review-intake-20261025 --output /absolute/new-name.app`
- Headset: `./scripts/refocus-unity-and-build-device.sh`; if offline, Tools → Codex → Build Android APK.

The worker's `--request` JSON interface supports `create`, `prompt`, `answer`, `clone` and explicit `retry_clone`, so UI and automation use the same state transitions. Desktop state belongs to the Python worker; the headset owns timed experiment execution. Tests use synthetic answer WAVs and fake providers; live microphone/clone quality and wearer confirmation still require an end-to-end device rehearsal.
