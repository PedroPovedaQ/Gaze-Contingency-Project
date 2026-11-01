# Study network hosts — November 1, 2026

**Implemented; source audit.** Scope: the desktop Study Intake/Trial Review app, its Python workers, the Focus Vision study app and the Mac MiniLM service. This inventories application destinations and explicit optional tooling; it is not a packet capture of Unity, macOS, Android or vendor SDK telemetry. No live provider request, microphone capture or participant-data test was used for this audit.

## Participant flow

| Host / transport | Caller and content | Source |
| --- | --- | --- |
| `api.elevenlabs.io:443` (HTTPS; optional speech WebSocket uses WSS on the same host) | Intake answer audio for `scribe_v2` transcription and instant voice cloning; fixed prompt/study phrase text for speech synthesis; optional synthesized-audio verification. Selected Voice preview and clone cache preparation use this same host. | `tools/trial-review/participant_intake.py`, `neutral_audio.py`, `Assets/ElevenLabsVoiceClient.cs`, `VoiceSynthesizer.cs` / `.Downloads.cs`, `VoiceAudioVerification.cs`, `SpatialVoice/ElevenLabsPcmClient.cs` |
| `127.0.0.1:8766` (HTTP) | Headset guidance state/candidate text and health checks, tunneled by ADB reverse to the Mac. Local MiniLM binds loopback and loads both model stages with `local_files_only=True`; no cloud fallback. | `Assets/SpatialVoice/JevDecisionClient.cs`, `tools/local-jev/{headset,serve,model,phrase_model}.py` |
| `127.0.0.1:5037` (ADB server), selected headset LAN address on port `5555` or its authorized wireless-debugging endpoint; USB alternative | Local assignment/cache transfer, controls, recordings/replay and the reverse model tunnel. The headset address is configured/discovered, not a fixed Internet host. | `scripts/headset-adb.py`, `tools/trial-review/live_bridge.py`, `tools/local-jev/headset.py` |
| `ucf.yul1.qualtrics.com:443` (prepared browser link only) | Draft final-survey handoff with coded session/order metadata. The application constructs a URL; this is not an automatic upload of intake audio/transcripts. Survey publication remains on hold. | `Assets/TrialDataLogger.cs`, `docs/guide/10-audio-intake.md` |

**Confirmed for intake audio and transcripts:** the only cloud recipient in the implemented intake/voice pipeline is ElevenLabs. Full answers/transcripts/corrections remain in private local session files and local exports; the headset receives the validated assignment and prepared audio cache without interview text. New follow-up methods/options, retired voice-prep scripts and their credential lookups have been removed. An unused `mistral_key` entry still exists in the ignored secret file; its contents were not printed or edited.

**Implemented model boundary:** non-development desktop intake assignments set `SessionConfig.StudyModelLocked`; `TrySetLocalAdaptiveModel(false)` rejects changing those sessions to cloud Jev. MiniLM inference stays on the Mac and the local ADB transport. This is a source-level routing guarantee, not a claim about provider retention or all operating-system network activity.

## Explicit optional paths outside the participant intake flow

| Host | Caller and transmitted content | Status |
| --- | --- | --- |
| `ai-gateway.vercel.sh:443` | Development Jev choice requests containing spatial state, offered phrases and speech history; explicit comparison/pilot scripts in `tools/local-jev/` and `analysis/` use the same endpoint. | Retained development path; unavailable as a selection for non-development desktop study assignments. Does not read intake answers/transcripts. |
| `chatgpt.com:443`, with `auth.openai.com:443` for authentication | Optional Trial Review **Astra** evaluation invokes the installed Codex CLI using ChatGPT login. It sends allowlisted guidance-evaluation text/state. CLI owns its network/authentication implementation; these service domains are present in the installed binary, not endpoint literals in the study source. The CLI binary also contains `api.openai.com` and `ab.chatgpt.com`; their use in this particular invocation was not exercised or established by the source audit. | Explicit operator action; no intake audio/transcript input. This means an unqualified statement that **all study-related text** goes only to ElevenLabs would be false. |
| `huggingface.co:443` and provider-selected download hosts | Initial `tools/local-jev/train.py` fetches the pinned pretrained MiniLM/tokenizer through Transformers. Package/model download redirects are dependency-managed, not an enumerated application endpoint. | Setup/training only; authored synthetic supervision. Not used by the running local inference service, which requires local checkpoints. |

No optional cloud comparisons, Codex evaluations or model downloads were invoked for this verification. Publishing/build infrastructure (GitHub/package registries, Unity licensing and `git.overleaf.com` in the separate thesis-sync script) is not a participant runtime destination. No publish/push was performed.

**Out of study scope; unchanged:** `tools/paper-to-podcast` and `tools/interactive-paper-podcast` retain their personal-tool integrations. Pedro can decide their future separately. They are not bundled by the desktop study build or called by the intake worker.

## Regression check

Run `python3 scripts/check-study-providers.py` or the auto-discovered `scripts/tests/test_study_providers.py` suite. The check scans tracked and untracked non-ignored study code/config in `Assets/`, `tools/trial-review/`, `tools/local-jev/`, `scripts/` and `analysis/`, reports locations only, and never opens `api_keys.json`. Synthetic tests exercise forbidden endpoints, credential names, clients and dependencies in every root. Old `provider="mistral"` metadata readers in `scripts/verify-study-session.py` and `analysis/test_study_validation.py` remain valid; they make no network request.
