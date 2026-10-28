# Two-stage adaptive assistance — 2026-10-26

## Implemented

The selected provider owns both decisions: `assistance` chooses one of eight strategies, then `assistance_phrase` chooses the exact prepared phrase for a speaking strategy. Both calls use the same provider instance. Observation skips stage two; `observe_2s` is no longer accepted. Unity measures spatial facts, enumerates valid prepared alternatives, coordinates requests, rejects stale/invalid output and plays audio. It does not rank the adaptive action or wording. Opening/confirmed selection/expired trigger feedback and tracking/truth gates remain explicit local behavior.

Phrase-stage state contains the chosen action, parent action decision ID, and `behavior.phrase_history`. History stores actual playback starts, canonical phrase, last rendered text/context, count and last-start age; proposals and pre-onset cancellations do not count. Opening speech is retained when search starts without adding presearch time to active search. Pause/resume preserves history; new round/participant resets it. An interrupted start is not proof the full clip was heard. Repetition is discouraged, not prohibited; some correction menus contain one truthful phrase.

Production ownership: `AdaptiveGuidancePolicy.cs` coordinates sequential requests and history; `JevDecisionClient.Adaptive.cs` defines both contracts; `HintGenerator.Actions.cs` enumerates candidates and validates selected wording; `VoiceSynthesizer` publishes actual starts; `StudyNavigationVoiceBridge.Adaptive.cs` enforces provider/voice/round freshness and cancellation. Each stage has a 1.5-second deadline, with a 3-second combined request limit. No failed phrase request falls back to a locally selected hint or the other provider.

## Local model and evidence

Bundle: `~/.local/share/follow-my-voice-models/20261026-minilm-pipeline-v5/`.

- `model/`: eight-class action model, initialized from adaptive-v4, full-weight fine-tuning, 2560/512/512 synthetic train/validation/test examples. Best validation epoch 2; held-out agreement 510/512; zero model gate violations in that test set.
- `phrase/model/`: separate MiniLM scalar candidate scorer initialized from the new action encoder; full-weight fine-tuning with listwise cross entropy, 768/160/160 grouped synthetic examples. Best validation epoch 3; held-out preference agreement 151/160; avoided already-spoken candidates in 85/89 eligible cases.
- Exported production phrase menus, datasets, reports, training sources, package versions and hashes are retained with the bundle. Sources were copied after training, before editing those sources.
- Supervision is authored synthetic preferences, not Jev distillation or participant outcomes. Agreement is imitation evidence only. Scores become uncalibrated softmax probabilities over offered candidates.
- Live production C# MiniLM: independent progress chose observation (71 ms); stalled case chose `orient_explicit` then “Keep turning right.” (215 ms).
- Live production C# Jev: two progress probes chose observation; two stalled probes chose `orient_explicit` then “Turn farther right.” (404/483 ms), with “Keep turning right.” recorded twice in input history. Raw synthetic evidence saved in bundle `jev-two-stage-live.json`.

## Verification and deployment

Spatial/adaptive pipeline checks cover both transport providers, sequential action/phrase ordering, selected-action candidate isolation, observation short circuit, malformed/unoffered responses, stale transitions, cancellation, actual starts and round/participant isolation. Voice isolation passed; local Python suite 36 tests and Trial Viewer Python suite 94 tests passed. Viewer native build passed at `/Users/pedro.poveda/Applications/Trial Review Two Stage.app`; action/phrase requests and parent-linked playback are inspectable.

Review was performed inline under the repository’s sequential-review instruction. It found and fixed opening-history loss at search activation. No independent cross-model review or participant outcome validation is claimed. Further simplification was avoided where it would thin boundary checks or remove meaningful regression tests.

The v5 CPU service is running on Mac loopback port 8766, with authorized wireless ADB reverse tunnel. The old v4 service was explicitly replaced only after v5 health passed on a temporary port. Temporary service stopped. Android build passed twice: 76.5 seconds for the initial pipeline and 42.3 seconds after the final opening-history terminal reset. Final APK installed and launched successfully over wireless ADB (`192.168.1.208:5555`), package `com.DefaultCompany.MixedRealityTemplate`, device lastUpdateTime `2026-10-26 22:42:03`, running PID 29511. The model tunnel was restored after installation and v5 health advertises both stages. No automatic commit or push.

## Open validation

Wearer test both providers during motion, stalls and repeated hint opportunities. Verify helpfulness, audio timing and lack of stale directions. Compare providers under the same voice/condition and inspect stage/playback traces. Synthetic scores do not establish study efficacy.
