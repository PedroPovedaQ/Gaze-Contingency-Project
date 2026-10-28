# Intake eligibility review after all questions — October 28, 2026

**Implemented.** Pedro authorized finishing the nine questions before researcher eligibility review. Work is local to `experiment/local-jev-20261025`, starting at `50d1893`. No thesis edits or publication.

## Phase order

- `awaiting_consent` → `select_voice` → `interview` (all nine fixed questions).
- At the ninth answer, if speech ≥30 s and words ≥50: `eligibility_review`.
- Otherwise: `fallback_available` (still Audio intake). **Read fallback passage / add more speech** → `fallback`. Completing the take reaches `eligibility_review` if the thresholds are met, or returns to `fallback_available` for another optional take. **Continue to eligibility review** reaches `eligibility_review` without adding speech. This keeps fallback optional; it adds no consent gate.
- A single saved positive review → `interview_complete`, where automatic clone starts on the next existing poll if both thresholds are met. Automatic cache starts when the clone is ready, with the same review/threshold requirements. The cache statuses remain `not_started` → `preparing` → `ready` → `transferred`.
- A negative, unclear or skipped eligibility answer → `screening_stopped`, retaining all answers exactly as before, with no clone/cache. Withdrawal remains available under the existing controls and blocks subsequent work.

The stepper is **Audio intake ✓ → Eligibility review → Clone & cache**. The eligibility receipt is recorded once, after all nine questions and any fallback takes. All usable answers count in the immutable clone snapshot. Continuing to review below either threshold leaves cloning disabled; additional speech must be collected before review.

Consent, voice-cloud permission, the fixed River voice/questions, 880 Hz/180 ms cue, microphone onset, question-heard gate, withdrawal controls, speech-quality heuristic and ≥30 s / ≥50 word limits are unchanged. Advancing to a different question or the fallback now invalidates the previous prompt revision/audio so a synthesis failure cannot reuse the previous question’s heard marker. Retry still preserves the submitted answer without counting it twice.

## Verification

- 127 desktop Python tests pass (including 35 focused participant intake/audio tests). Regressions cover uninterrupted nine-question order, pre-review clone rejection, positive review, negative/unclear review after nine answers, preserved answer files, cache rejection before review, skipped eligibility, optional/repeated fallback, unchanged thresholds, prompt-failure retry and withdrawal across phases. Provider/remote operations use synthetic fixtures and mocks.
- Isolated desktop Unity build succeeds with all 24 PASS markers. Editor checks cover step mapping, Audio intake completion tick, reviewed/threshold clone and cache predicates, withdrawal, nine-answer capture fixtures and capture isolation.
- Final `--capture` run: 46 states at 1440×900 and 1280×800, 92 PNGs total, no exceptions or layout errors. Both runs pass the existing synthetic input-handler checks. Inspected interview, review, fallback choice, fallback, screening stopped and post-review clone screens, including light/compact review. Capture mode bypasses workers, providers, live/ADB, microphone, saved participant folders and audio output.
- Source comparisons verify unchanged consent/capture/tone/withdrawal methods, question list and quality thresholds; `git diff --check` passes.

Final screenshots: coordination `trial-viewer-shots/after/intake-order-20261028-final/` and its `1280/` subdirectory. New states: `43-intake-fallback-available.png`, `44-intake-fallback.png`, `45-light-intake-eligibility.png`, `46-intake-reviewed-awaiting-clone.png`. Existing intake states 18–24 and 32–40 are regenerated. Hashes and test/build/capture logs are in [receipts](receipts/2026-10-28-intake-review-order/).

## Installation

Built `/tmp/Follow My Voice Intake Order 20261028-final.app`, installed the new bundle at `~/Applications/Follow My Voice Intake Order 20261028.app`, and captured that bundle before updating `~/Applications/Follow My Voice Study Intake.app`.

Preserved the exact prior main app as `~/Applications/Follow My Voice Study Intake Backup 20261028b.app`. Every file hash and symlink target matches build = new bundle = installed, and original = backup. The earlier `Backup 20261028.app` remains untouched. Installation receipt: `receipts/2026-10-28-intake-review-order/installation.json`.

PIDs 38585 and 38591 retain their original October 27 start times and parent relationship. No microphone, real ElevenLabs/provider operation, real cloning, headset or ADB command was used. No participant data was inspected or modified. No wearer rehearsal is claimed. Inherited `progress.md` edits are preserved; that file remains outside this commit. No push, `docs/thesis/` edits or Overleaf actions.

## Thesis handoff

The existing claim that researcher review stops screening on negative or unclear eligibility remains correct. Add timing: “All nine audio-intake questions, and any optional insufficient-speech reading fallback, are completed before the researcher reviews the four eligibility answers. Only a positive review and at least 30 seconds of estimated speech and 50 transcript words permit automatic cloning and caching.” Remove any statement that review occurs after question four or that cloning starts while baseline questions are still being answered. No research threshold, question wording or measured result changes.
