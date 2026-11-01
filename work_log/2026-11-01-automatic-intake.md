# Automatic ten-question intake — November 1, 2026

**Implemented.** Pedro's current request is implemented on `experiment/local-jev-20261025`, based on a6adf39. Local commit only; no push, Overleaf publication or edits under `docs/thesis/`. Inherited `progress.md` edits and readiness receipts remain uncommitted; this session added its own progress entry without absorbing them.

## Flow and settings

New sessions use **audio-intake-v3-20261101** and the ten exact prompts in [the intake guide](../docs/guide/10-audio-intake.md). V2 sessions retain their version, questions and transcripts read-only; explicit v2/v3 assignment validation remains available.

Reserve P### → prospective consent receipt → Selected Voice audition/confirmation (one interview start) → River prompt → 880 Hz / 180 ms tone → automatic capture at 250 ms from tone onset → automatic end → local WAV/transcript record → next question. Exact-text/voice/model local River clips are reused; prompt synthesis/playback failure pauses. All ten questions and any optional fallback precede researcher eligibility review. There is no automatic eligibility pass. Positive reviewed eligibility plus ≥30 s estimated usable speech and ≥50 transcript words still gates automatic cloning/cache. Fallback remains optional and is collected before review.

Local activity detector: **20 ms**, **24 kHz** mono-downmixed PCM; subtract each window's DC mean; **RMS ≥0.01** full scale; **200 ms consecutive activity** for onset. After detected speech: **3 s silence** for eligibility Q1–4; **5 s** for open Q5–10 and fallback. Active speech resets silence. **10 s without speech** causes one replay/tone/new take; the second timeout marks the answer unanswered. **120 s maximum** per take. Silent and interrupted takes remain local, outside clone input unless explicitly saved as an answer. Settings are recorded in session/capture authorization; automatic ending receipts record reason and actual configured silence window.

Pause immediately ends microphone capture, preserves partial audio and invalidates pending prompt/tone capture. Save paused answer / continue submits a retained take; Repeat begins a new take. Pause, Repeat, Skip and withdrawal remain outside the scrolling content. A queued Skip is bound to the question shown when clicked, so an in-flight answer save cannot skip the next question. Withdrawal persists independently during ASR, blocks new work and cannot be reversed by the returning worker. Requests already sent to a provider cannot be recalled.

## Transcripts and service boundary

All ten recorded answers and fallback takes use ElevenLabs `scribe_v2`. An ASR error is persisted, audio retained and the cursor advanced; it can be retried or manually transcribed during review. Full original machine text, later machine attempts and researcher correction history are retained. Question text/version/index, coded ID, audio reference/hash, provider/model, timestamp and correction status are recorded. Q5–9 categories are researcher-confirmed; frequency defaults to Unclear. No additional narrative meaning is inferred.

Private `intake_transcripts.json` / `.csv` exports are refreshed in the participant session folder. The run exporter follows the exact desktop session UUID and P###; corrections produce new export identities. Withdrawal blocks new exports and further transcript operations; prior exports/data are retained for the approved handling procedure. No transcript is put in the headset assignment, git or thesis output. See [data dictionary](../docs/intake-data-dictionary.md).

Name/identifier flags are a conservative local heuristic, never silent redaction or a guarantee. The operator can inspect/correct all answers. Unsaved edits block question switching/final review until saved. New reservations reset eligibility selections to Unclear.

The old Mistral follow-up method is disabled even for direct callers, and its network implementation is removed. No intake answer audio/text goes to a non-ElevenLabs service. Other separately invoked project paths remain: standalone Voxtral smoke/e2e scripts can upload supplied voice samples/text to Mistral; development Jev spatial guidance has a Vercel TypeSafe endpoint; optional Codex guidance judging sends selected guidance case text through the Codex CLI. None automatically receives intake transcripts and none ran live during this verification. Study intakes remain locked to Local MiniLM for guidance.

## Verification

All final checks use synthetic data or fixtures; no live microphone, ElevenLabs request, cloning or participant recording was used.

- **607 Python tests: 606 passed, one optional statsmodels skip.** Desktop 137; analysis 375; scripts 14; script tests 19; Local MiniLM 45; native timing 17. Initial new-feature tests failed as expected. Initial broad runs used an interpreter missing pywt/docx; rerun with the existing project environment passed. A test's five-word sentence expectation was corrected from six. Unity environment-variable dumps are omitted from receipts, ANSI formatting and trailing whitespace normalized; test/error outcomes are retained. Final logs/summary are in [receipts](receipts/2026-11-01-automatic-intake/).
- **Unity study integration: 27 PASS markers; preparation: 5; intake contract: 1.** Includes explicit v2/v3 acceptance and unknown-version rejection. Desktop build: **27 PASS markers**, including automatic-flow consent/pause/withdrawal guards before microphone authorization, exactly one no-speech repeat, ten-question review/clone gates, capture isolation and synthetic PCM activity tests.
- Synthetic PCM covers exact 3/5 s endings, a **4 s open-answer pause without cutoff**, speech resetting silence, no-speech at 10 s, DC/quiet noise/isolated impulse rejection, 120 s cap and stereo downmix. These test software thresholds, not real speech detection quality.
- **Installed app: 50 states at 1440×900 and 50 at 1280×800**, with synthetic input checks; no exceptions or layout errors. Visually inspected question 10, listening, paused, failed ASR and name-flag review states. [Screenshot manifest](receipts/2026-11-01-automatic-intake/screenshots.json) records all paths/hashes. Final images live in coordination `trial-viewer-shots/automatic-intake-20261101/{1440,1280}/`.
- [Sequential code review](receipts/2026-11-01-automatic-intake/code-review.md) and simplification completed per repository instructions; no independent-peer claim. Diff whitespace passes. No thesis files changed.

## Builds and installation

Verified desktop build: `/tmp/FollowMyVoice-AutoIntake-20261101-delivery.app`. Replaced `~/Applications/Follow My Voice Study Intake.app`, rotating its former contents to `Follow My Voice Study Intake Previous.app`. All **169 files/symlinks** match the build, and Previous matched former Current. Exactly two Follow My Voice bundles remain; unrelated applications were preserved. An initial verification assertion mistakenly counted unrelated apps after both manifest checks passed; corrected the assertion without rotating again. Final captures used the installed Current bundle.

APK built in **373.9 s** for the runtime assignment-version change. The required refocus/build menu helper reconnected wireless ADB but Unity activation failed **-609**; batch packaging completed instead. Known vendor spectator/manifest NullReferenceExceptions were nonfatal; no vendor code changed. Build-generated preloaded-asset settings churn was restored to its original bytes.

Immediately before deployment, the pinned Focus Vision was **online, asleep, Android activity STOPPED, unassigned**. The old application status was stale and is not current wearer evidence. Install succeeded, Local MiniLM reconnection reported ready, and launch returned Status: ok. Installed APK SHA-256 matched build: **7c572774669b8436978f64435602b50dfd46ea0c4dc2996e9c0bcae05c0eb326**. Receipts include fresh precheck, installation log and hash comparison. Keep the Mac awake and ADB connected for local-model sessions.

## Exact thesis changes requested

1. Replace the nine-question/manual Record–Stop description with **audio-intake-v3-20261101**, ten exact fixed prompts from the guide: four closed eligibility questions, five open background questions, one ~30 s directional speech prompt. Retain older-session versions as historical.
2. Describe one researcher start, River → tone → automatic capture/save/transcription → next; give the 20 ms / .01 RMS / 200 ms onset, 3 s vs 5 s silence, 10 s one-repeat no-speech policy and 120 s cap. Label these authored engineering settings, **not validated voice-activity thresholds**.
3. Retain prospective consent before capture; Pause/Repeat/Skip/withdrawal controls; all questions and optional fallback before human eligibility review; no automatic pass; unchanged ≥30 s / ≥50-word clone admission gate. Do not say cloning occurs during unanswered questions.
4. State that **all answers** retain full ElevenLabs scribe_v2 transcripts, linked to P###, with machine original and correction/retry provenance, local JSON/CSV participant exports and researcher review/correction. Add the data-dictionary fields; do not insert actual transcripts or raw audio into the thesis.
5. State that background categories require researcher confirmation and unclear frequency stays Unclear. Narrative portions have transcript text only, without inferred extra coding. Name flags are fallible review prompts; no silent redaction.
6. Remove any generated clarification/Mistral step from the study flow. ElevenLabs alone receives intake voice data; separate development provider/evaluation tools do not automatically consume these transcripts.
7. Update procedure/flow figures and software provenance to the new local commit and synthetic screenshot receipts. **No wearer, microphone, voice-clone quality or participant validation claim** follows from these software checks. Full wearer rehearsal, approved consent/protocol wording/retention and survey release remain pending. The 96-trial task, gaze geometry and analysis results are unchanged.

Vikunja was unavailable (localhost:3456 connection refused); no task was created or changed.
