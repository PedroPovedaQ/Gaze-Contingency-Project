# Local intake transcript data

**Implemented — November 1, 2026.** `audio-intake-v3-20261101` has ten fixed questions. Older sessions retain their own version and question count. The private participant session folder is `~/Documents/Trial Review Intake/<session UUID>/`; P###, not a name, is the participant key. All files below are participant data, excluded from git and thesis outputs.

| File | Contents |
| --- | --- |
| `session.json` | Protocol, participant/session identity, consent reference, phase, capture settings, answers, eligibility review and clone/cache provenance. |
| `intake_transcripts.json` | `intake-transcripts-v1` export: participantCode, sessionId, protocol, withdrawnUtc and full answer records including machine/researcher history. |
| `intake_transcripts.csv` | One row per answer or optional fallback take; full current and original machine text, provenance, status, coding and correction flag. |
| `answer-<id>.json` | Same answer record persisted individually. |
| `answer-<capture ID>.wav` | Original mono 24 kHz 16-bit capture. Optional `.source.wav` preserves multichannel input before downmix. |
| `*.wav.capture.json` | Consent/session binding, zero-based capture questionIndex, exact prompt, authorization timestamp, cue and silence settings. |
| `*.wav.end.json` | Automatic stop reason (`sustained_silence`, `no_speech`, `maximum_duration`), detected-speech flag, analyzed duration, silence window and repeat index. |

| Answer field | Meaning |
| --- | --- |
| `participantCode`, `protocol`, `id`, `topic` | Coded participant, immutable protocol version, unique answer ID, fixed question topic. |
| `questionIndex`, `question` | One-based index and exact question text. Optional fallback follows the ten fixed questions; topic is `reading_fallback`. |
| `audio`, `audioSha256` | Audio filename relative to the linked private intake folder and original WAV hash; blank for an unanswered question. |
| `transcript` | Full current text, with no silent redaction or narrative interpretation. |
| `machineTranscript` | Original successful machine text, retained after correction. Empty when no successful ASR exists. |
| `transcriptionProvider`, `transcriptionModel` | ElevenLabs / `scribe_v2`; blank for unanswered questions. |
| `recordedUtc` | Unix UTC seconds when the answer/transcription result was saved. Capture authorization time is in the linked capture receipt. |
| `transcriptionStatus`, `transcriptionError` | `complete`, `failed`, or `unanswered`, plus failure information. A manual correction does not erase the recorded ASR failure. |
| `researcherCorrected` | Whether a researcher saved text/coding; false until explicitly saved. |
| `possibleIdentifiers` | Local conservative name/identifier heuristic. Review flag only: false is not proof of de-identification. |
| `structuredValue` | Eligibility remains unreviewed until its separate review receipt. Background defaults to `unclear`; researcher categories are listed in the intake guide. Narrative content has no additional inferred coding. |
| `skipped` | Unanswered due to explicit Skip or exhausted no-speech retry. Either prevents an eligibility item from passing. |
| `quality` | Duration, DC-removed energy speech estimate, word count, clipping and clone usability; unchanged 30 s / 50 word admission gates. |
| `transcriptionAttempts` | JSON-only history: each machine result/error, provider/model and timestamp, including retries. |
| `corrections` | JSON-only history: previous/new text, researcher initials, timestamp and chosen background category. |
| `captureAttempts` | JSON-only references to all takes for the question, including silent attempts and their ending receipts. |

**Implemented export linkage:** a run's `desktop-session-id.txt` must resolve to the same session UUID and P### before the desktop run exporter includes the two transcript files. Missing local intake is reported as unavailable. Updated transcripts produce a new export; prior exports are preserved. Audio references remain relative to the private intake folder; raw audio is not copied to the headset or run export.

**Implemented withdrawal:** stop capture immediately, persist withdrawal, block further capture/transcription/correction/clone/cache/handoff and new run exports. Preserve an already in-flight provider result locally without restarting the interview. Existing audio, transcripts and prior exports follow the approved data-handling process; software does not silently delete them or claim to recall provider uploads.

**Open decision:** approved retention/deletion procedure, wearer validation of silence settings and completeness of name review. Synthetic tests establish software behavior only.
