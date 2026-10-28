# Sequential local review

Reviewed the changed state transitions, UI poll triggers, Python clone/cache entry gates, capture fixtures, unit/editor tests and procedure wording in the main thread. No subagents used.

Checked all-nine ordering, one immutable review, no/unclear/skipped stopping behavior, fallback opt-in and repetition, no pre-review clone/cache, all-answer sample accounting, positive-review auto-start, withdrawal and source-data retention. Confirmed consent/tone/capture/withdrawal invariant methods against HEAD 50d1893 with the receipt in invariants.json.

Resolved findings: invalidate stale prompt playback revision when advancing to another question/fallback, including synthesis failure; remove the empty fallback-choice card and describe skipped items as completed, not recorded. Rebuilt and reran the full desktop suite and both capture sizes after these changes. No unresolved findings within this change.

Validation is synthetic/local. Existing saved participant sessions were not opened or migrated, and no provider/headset/microphone operations were exercised.
