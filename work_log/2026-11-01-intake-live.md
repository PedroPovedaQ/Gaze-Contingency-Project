# Synthetic live intake and desktop restart validation

## Follow-up: authorized scenario A completed (2026-11-01)

Starting revision: `4aaba0d`, branch `experiment/local-jev-20261025`.
Fresh receipts: `receipts/2026-11-01-intake-live-A/`.
This follow-up supersedes the subscription-permission gate and the request below
to enable `user_read`. **No key or permission change is required for this test.**

**Implemented:** subscription HTTP 401/403 records quota as unavailable and
continues. The harness reserves TTS characters under a lock immediately before
every HTTP attempt, including retries and concurrent cache jobs, and rejects any
request that would exceed 6,000 characters. Actual submitted WAV frame counts
supply Scribe and clone seconds. Error receipts retain HTTP status and provider
reason without keys. Only the development harness, its tests and documentation
changed; no shipped worker, desktop UI or Unity runtime changed.

| Check | Executed result |
|---|---|
| A live preflight | PASS with unavailable quota: voices HTTP 200; subscription HTTP 401 (`missing_permissions`, missing `user_read`), continued without workaround. |
| A live happy worker path | PASS, 147.25 s: audition, nine River prompts/cache reuse, tone/playback, actual C# activity detector with Piper WAVs, nine Scribe transcripts, one clone, 20 cached clips, cache during review, correction preserving machine original, positive review, fake-headset transfer/assignment, consent/transcript/export checks, next development code. |
| A same-clone negative checkpoint | PASS, 1.69 s: replayed pre-review state/copied cache, stopped active cache, blocked handoff, cleanup in finally; zero additional billable POSTs. |
| Clone deletion verification | PASS: DELETE HTTP 200 and exact-ID absence in GET voices; final voice-list count 28. |
| Live harness boundary + fake-flow tests | PASS: 15 tests, including 401/403 continuation, concurrent exact-limit rejection, usage counting, refusal/no retry and real workers with fake provider. |
| Desktop Python suite | PASS: 171 tests, 135.698 s. |
| Full five-group offline harness | NOT RERUN in this follow-up. Prior run passed at 4aaba0d; no shipped-code change required rerunning it. Current fake live-flow test above was run. |
| B: desktop restart during answer/cache | NOT RUN by explicit instruction; moved to person-run rehearsal. |
| Desktop/APK rebuild | NOT RUN: shipped code unchanged. No installation. |

**Exact submitted usage:** **1,929 TTS characters** across 30 TTS POSTs;
**101.14 Scribe audio seconds** across nine POSTs (**2,427,360 samples at 24 kHz**);
**101.14 clone sample seconds** in one POST (the same number of samples).
The run made 40 billable POST attempts, all HTTP 200, and 46 requests total.
No rerun was needed. **Account quota/credit delta is unavailable**, not zero:
subscription reads before and after both returned HTTP 401. Submitted usage is a
harness count, not an account billing measurement.

**Clone:** `DEV-E2E-P900000000001-e757f63e`, ID
`o2gTwx5OSEMsD2rM0wKT`. Exactly one creation attempt/one created clone in this
follow-up; the prior blocked run created none. Deleted in negative-checkpoint
`finally`; outer `finally` verified absence. No other voice was deleted or changed.
No clone-slot or cloning-permission refusal occurred.

**River:** all nine current questions were generated once, including **Q4 and Q8**,
and each was reused from the exact-text cache with identical audio bytes and no
extra TTS request before playback. The journal records hashes for both generation
text and reused audio. The 20 study cache clips are ten phrases for each study
voice. The negative checkpoint reused copied cache files. All temporary synthetic
sessions/audio/exports were removed; this did **not** populate the installed app's
cache. Retained evidence is synthetic metadata/text, not participant data.

**Transcript accuracy sample (Piper input → Scribe output):**

- Q1: “Yes, I am over eighteen years old and I can take part in this experiment.”
  → “Yes, I am over 18 years old and I can take part in this experiment”
- Q4: “Yes, I can safely stay seated and turn in a swivel chair while using the
  controller in my right hand.” → the same words, without the final period.
- Q8: “I am wearing glasses today.” → “I am wearing glasses today”
- Q9: all 83 words matched, including punctuation in the full stored sample.

Across the nine synthetic answers, lowercase word alignment ignoring punctuation
found **1 edit / 242 reference words (0.41%)**, solely “eighteen” → “18”; numeral
spelling was deliberately not normalized. This is a synthetic fixture result,
not validation of participant speech recognition. Full inputs/outputs and counting
method: `receipts/2026-11-01-intake-live-A/transcript-accuracy.json`.

**First failure / fixes:** the first live HTTP failure was the expected subscription
401, provider status `missing_permissions`, reason: “The API key you used is missing
the permission user_read to execute this operation.” The new handling recorded it
and continued; after-run subscription measurement failed for the same reason.
There was **no unexpected live failure and no demonstrated shipped-code bug**.
While adding the offline audio-usage test, a broad `Path.read_text` mock also
replaced session reads, causing `require_development` to reject the fixture before
network submission. Replaced that mock with a real temporary synthetic-key file;
the boundary test, 15-test harness group and 171-test suite then passed. Initial
red tests also confirmed that the former quota gate and absent TTS cap failed the
new requirements before implementation.

**Boundaries verified:** PID 5228 remained open. Both configured and repository
`api_keys.json` files are byte-identical to pre-run hashes; the configured key is
absent from receipts/source changes. No alternate key, permissions change,
headset contact, microphone, participant data, app install, push, Overleaf or
`docs/thesis` edits. Sequential ce-simplify-code and ce-code-review checks found
no remaining actionable issue; no independent-peer review is claimed. Inherited
mixed progress/readiness changes remain outside the local commit.

**Remaining for a person:** scenario B restart during answer and cache belongs to
the supervised rehearsal, with deliberate separation from the existing app/session.
The full GUI and physical headset transfer, microphone/capture behavior, listening
comfort, acoustic quality/self-similarity, and wearer workflow remain unvalidated
by this worker harness. No subscription permission change is requested.

## Prior attempt at 4aaba0d (historical; superseded where stated above)

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
