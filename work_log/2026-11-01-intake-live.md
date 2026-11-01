# Synthetic live intake and desktop restart validation

Starting revision: `72c6d97`, branch `experiment/local-jev-20261025`.
This follow-up uses Pedro's explicit authorization for ElevenLabs synthetic audio;
the previous brief's blanket prohibition is superseded.

## Implemented

Harness-only `--live-elevenlabs --development-session` mode retains the offline
default. It restricts provider mutations to isolated development sessions, reads
voices/subscription first, requires a free slot and 30,000 remaining characters,
records one `DEV-E2E-` clone attempt and its exact returned ID, limits the fake
headset template to 20 clips, and deletes/verifies only its own clone in `finally`.
The account credit field is `subscription.character_count`; requested TTS characters
are also counted separately. Account deltas may include concurrent activity.

The live worker path includes real audition/prompt/tone/clone-clip playback via
`afplay`, Scribe v2, C# WAV activity detection, clone/cache before review,
correction-with-review, fake transfer/assignment, consent/session/transcript/export
checks and next development reservation. Negative screening replays a saved
pre-review checkpoint against the same clone and copied cache, with zero additional
billable POST calls. These are implemented harness capabilities, not evidence that
the live scenarios ran. The harness does not execute the desktop's GUI controller.

Synthetic capture receipts now explicitly record `none_development_wav_input` and
the WAV source hash instead of inheriting `operator_selected` microphone provenance.
All new tools remain excluded from `prepare.py` packaging. No shipped worker, Unity
runtime, API-key file, app installation, or APK was changed.

## Executed results

| Scenario | Actual result |
|---|---|
| A: Live preflight | Executed. GET voices succeeded: 28 listed voices, seven non-premade. GET subscription failed HTTP 401: configured key lacks `user_read`. |
| A: Live happy path | NOT RUN beyond preflight. Cannot verify free slot or 30,000 remaining characters; hard stop enforced. |
| A: Live negative path | NOT RUN. No live clone exists. |
| B: Desktop restart during answer | NOT RUN. Final build cannot safely isolate intake from the open app or replace microphone/provider/headset boundaries. |
| B: Desktop restart during cache | NOT RUN for the same reason. |
| Full offline harness | PASS: happy path, insufficient speech/fallback, negative screening, withdrawal, failures/retry/reload; zero fake clones remain. |
| Live-mode orchestration with fakes | PASS: real intake/audio workers, Piper WAVs/C# detector, one fake clone, positive correction/review/handoff/export and same-clone negative checkpoint; no network or OS playback in this test. |
| Desktop Python suite | PASS: 168 tests, including 11 live boundary tests and one fake live-flow integration test. Log: `receipts/2026-11-01-intake-live/desktop-tests.log`. |

The live command's complete request journal is
`receipts/2026-11-01-intake-live/live/live-results.json`. Initial diagnostic preflight
also made only GET requests. No audio POST, cloning POST or DELETE was sent.
**Real clones: 0; clone IDs: none; deletion verification: not applicable. Requested
TTS characters: 0; billable operations: 0.** Actual subscription characters/credits
before and after, quota limit, voice-slot limit and free-slot count are unavailable
because `user_read` is missing. Do not describe an unmeasured account delta as zero.
**New River clips: none. Reused River clips in live mode: none. Q4/Q8 remain ungenerated.**

## Desktop isolation evidence

The retained final build at `/tmp/FollowMyVoice-IntakeV4-Monitor-20261101.app` has
byte-identical packaged intake/audio/neutral/code/bridge workers to this checkout.
Its executable SHA256 is
`ef5b6260a69655013d3ae5559353d0e40806e16d97a142f0fa43e466a27fd86d`.
This is an existing artifact, not a rebuild.

`OpenIntake` restores `ParticipantIntakeFolder` from shared PlayerPrefs and starts
`ConnectLive`. `StartAnswer` requests microphone permission and uses `Microphone.Start`.
`--live-output` isolates bridge output only; it does not isolate preferences/catalog,
intake keys or session restoration. Capture mode bypasses the workers, so a
capture-only process cannot prove restart recovery. The final build has no fake
provider/headset or WAV-capture entry point. Launching it for this test could restore
the open app's session or touch its headset connection. No test desktop process was
launched, and no current-app preferences or participant files were inspected.

Detailed assessment: `receipts/2026-11-01-intake-live/desktop-restart-assessment.json`.
PID 5228 was confirmed alive before and after validation. No quit, replacement,
relaunch, headset command, APK install, microphone access, or real participant data.
Temporary development folders were removed. Receipts retain only synthetic metadata.

## Fixes and remaining work

No shipped-code bug was demonstrated by the blocked live/restart scenarios. The
harness provenance issue above was fixed. While implementing the test itself, a
subprocess mock initially recursed; its original function is now captured before
patching, and the fake live flow passes. Preflight/cleanup receipts preserve HTTP
failure status and cleanup outcomes; unknown clone outcomes cannot auto-retry.

A person must enable `user_read` on the existing ElevenLabs key (without sharing it
in chat), then resume the authorized run with a fresh receipt directory after
confirming prior clone count remains zero. Desktop restart validation needs a safe
isolated test entry point/build or a separately isolated user environment; neither
restart is claimed as passed. Physical headset, wearer, microphone and acoustic
judgment checks remain outside this session.

Local commit only; no push, Overleaf or `docs/thesis` changes. Inherited progress
and participant-readiness receipts are preserved and excluded from this commit.
Vikunja was not contacted under the session's restriction to ElevenLabs as the only
outside recipient. No desktop/APK rebuild was required because shipped code is unchanged.

Sequential ce-simplify-code/ce-code-review checks covered correctness, security, cleanup/retry, test fidelity, maintainability and project limits. No independent-peer review was run. Review receipt: `receipts/2026-11-01-intake-live/review.json`.
