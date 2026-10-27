# Desktop participant intake and headset handoff

User-directed implementation, 2026-10-25. Record participant answers only.

## Contract
- Live Trial Viewer owns a new participant session (distinct from a search trial), local interview records, microphone capture, agent prompts, cloning and explicit A/B assignment.
- A = self-similar first; B = neutral first. Both retain the existing block schedule. A separate neutral interviewer voice is never a study condition or a clone sample.
- Answer recordings go to ElevenLabs for transcription/cloning; adaptive prompts use Mistral. Names and interview records stay outside replay exports. Transfer only session ID, participant code, order, neutral choice and clone ID to the headset.
- Headset acknowledges the assignment, asks wearer confirmation, prepares existing study audio, then runs eye alignment and normal centering. No desktop command starts a timed search or replaces an active session.
- Calibration panel/targets use a horizontal facing frame at current eye height, not downward gaze pitch.
- User explicitly requests trusting synthesized audio: bypass transcript, filler and audio-quarantine gates in all modes. Retain transport/decode requirements necessary to play a file, volume normalization and truthful provenance.

## Implementation
1. Session DTO, durable assignment and idempotent headset confirmation through existing ADB command transport.
2. Local intake worker and Live-mode UI: core questions, bounded adaptive follow-ups, answer-only recording, concurrent clone creation, explicit handoff and acknowledgments.
3. Eye-height alignment placement; provider-trusted voice admission; regression updates.
4. Python and Unity checks, desktop build, Android build/install, UI inspection and protocol/progress notes.

## Verification
Exercise persistence, invalid/stale handoffs, assigned order and isolation, answer-only sample selection, clone idempotency, provider failures, default interviewer outside study catalog, pitch-independent calibration, and unverified playable audio. Do not fabricate live participant testing or voice-clone results from synthetic fixtures.

## Research status
Implemented behavior will be labeled separately from the proposed interview protocol. This change does not establish IRB approval for new questions or data handling.
