# Local intake transcript data

**Implemented — November 1, 2026.** `audio-intake-v4-20261101` has nine fixed questions. Older sessions retain their own version and question count. The private participant session folder is `~/Documents/Trial Review Intake/<session UUID>/`; P###, not a name, is the participant key. All files below are participant data, excluded from git and thesis outputs.

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
| `questionIndex`, `question` | One-based index and exact question text. Optional fallback follows the nine fixed questions; topic is `reading_fallback`. |
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
# Automatic headset fit and participant code allocation (November 1)

**Implemented:** Reserve no longer collects IPD. `session.json.fit` receives the matching headset's fit record through a fresh, participant-code/session-UUID-bound status update. Original manual `ipdRecorded`, `participantIpdMm`, `ipdSource`, `headsetIpdRecorded`, `headsetIpdMm` remain separate and keep their legacy meanings. No old records are backfilled. `viewSeparation` and `eyeSeparation` each contain `recorded`, `millimetres`, `source`, `method`, `recordedUtc`, `samples`, `windowSeconds`, `rangeMm`; counts are acquisition snapshots, not validated independent sensor samples.

**Implemented:** `fitCaptureId`, `fitStartedUtc`, `fitEndedUtc`, `setupSampleCount`, `fitWarning` describe acquisition. `autoIpdStatus=unavailable`; `separationChange` is `change_observed`, `no_change_observed` or `insufficient_observation`. `separationChangeMm`/`separationChangeUtc` describe the largest signed stable view-median deviation from the first stable baseline, not a device adjustment event. Manual fallback retains the measurement source and adds `manualRecordedUtc`. See [automatic fit](research/automatic-headset-fit.md).

**Implemented:** `~/Documents/Trial Review Intake/participant-codes.json` retains reserved codes (including stopped/withdrawn/deleted-folder reservations) and known development codes. Study suggestions start at P001; new rehearsals start at P900000000001. Both satisfy the existing P-plus-3–12-digits contract. A headset suggestion is already unused and therefore forms a floor, not an extra increment. Local inventory is the boundary: records unknown to this computer/headset cannot be checked. No allocation migrates existing IDs or modifies participant data.

**Implemented v4 additions:** no handedness prompt or category is generated in new transcript JSON/CSV exports. Historical answer records, including handedness, remain unchanged. `cloneAnswerIds`, `cloneInputSha256` and clone timestamp identify the audio snapshot made before eligibility review; later audited transcript/coding corrections do not rewrite that snapshot. `cleanupState`, `cleanupError`, `cloneDeletedUtc` and `clone-deletion.json` record provider cleanup after stopped screening or withdrawal. `cacheState=stopped` cannot be overwritten by a late worker result.

**Implemented monitor record:** `PreparationLogs/session-<desktop session UUID>/screen-transitions.jsonl` stores UTC, desktop session ID, headset process/session ID, participant code, plain-language screen, elapsed seconds since assignment, completed trials and voice block. Heartbeats also expose screen elapsed seconds, gaze status, audio/model faults, app focus/pause and presence availability/value. These are operational telemetry, not validated gaze measures or proof that a participant saw/heard a screen. Older sessions have no inferred transitions.
