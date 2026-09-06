# 2026-09-06 — Self-Similar Voice Mode: handoff / current state

Handoff note for the next agent (Codex) continuing the self-similar voice work.

## Where things are

- **Working directory:** `/Users/pedro.poveda/Gaze Contingency Project` (the MAIN project, not a
  worktree).
- **Branch:** `v2`. The voice feature branch `feat/self-similar-voice` was **merged into `v2`**
  (merge commit `bbfdcef`, no conflicts). All voice code + the IRB docs now live together on `v2`.
- The feature originated in the `podgorica` worktree
  (`conductor/workspaces/Gaze Contingency Project/podgorica`, branch `feat/self-similar-voice`).
  That worktree is no longer needed — work from the main dir on `v2`.
- Unity Editor is open on the main project (Unity `6000.3.10f1`, build target Android).
- `Assets/StreamingAssets/api_keys.json` (gitignored, local only) has BOTH
  `elevenlabs_key` and `mistral_key`. Do NOT print, commit, or move these values.

## What the feature does

Adds an on-entry **launch-mode picker**: Standard (generic ElevenLabs voice) vs **Self-Similar**
(clone the participant's own voice via Mistral **Voxtral** and speak every assistant line in it).

On-entry panel (world-space, in front of the camera):
- **Trigger** or keyboard **1** → Standard voice.
- **A/primary** or keyboard **2** → Self-Similar → stop the generic intro → show a reading passage
  with a 3-2-1 countdown → record ~40 s from the mic → clone via Voxtral → speak in the clone.
- The clone `voice_id` is persisted to `PlayerPrefs["selfsim_voice_id"]` and reused on later runs
  (skips re-recording). Clear with `PlayerPrefs.DeleteKey("selfsim_voice_id")` or
  `defaults delete "unity.DefaultCompany.Gaze Contingency Project" selfsim_voice_id` (quit Unity first).

## Key files (all on `v2` now)

- `Assets/VoiceModeSelector.cs` — on-entry picker; input handling (edge-triggered); `EnrollFlow`
  coroutine (passage + countdown + record); persistence/reuse of the clone id.
- `Assets/VoiceEnrollment.cs` — mic capture → WAV → `VoxtralClient.CloneVoice`; Android mic permission.
- `Assets/VoxtralClient.cs` — Voxtral clone (`POST /v1/audio/voices`) + TTS (`POST /v1/audio/speech`,
  model `voxtral-mini-tts-2603`, base64 `audio_data`). JSON-vs-raw guard to avoid cache poisoning.
- `Assets/WavUtility.cs` — 16-bit PCM WAV encoding from AudioClip.
- `Assets/VoiceSynthesizer.cs` — branches on `SessionConfig.Voice`: Voxtral for self-similar,
  ElevenLabs for generic (also the fallback). Voice-aware disk cache key (`vx-{voiceId}` vs `el`).
- `Assets/VoiceAssistantController.cs` — loads keys (`ApiKeys.mistral_key`), wires up
  `VoiceSynthesizer` + `VoiceEnrollment` + `VoiceModeSelector`. Auto-attaches to `ObjectSpawner`
  (so voice systems only spin up in the real MR scene).
- `Assets/SessionConfig.cs` — `enum VoiceCondition { Generic, SelfSimilar }`,
  `Voice`, `SelfSimilarVoiceId`, `SelfSimilarEnrollmentPending`; run-folder label gets a voice tag.
- `scripts/voice-prep/e2e_clone_test.py` — stdlib-only e2e contract test (clone→TTS→delete);
  reads keys from `api_keys.json`, never prints them. Verified all HTTP 200.

Scene with the voice systems: `Assets/Scenes/GazeContingencyStudyScene.unity` (the ONE scene in
Build Settings). An empty/default scene shows nothing — the systems need `ObjectSpawner`.

## Next steps

1. In Unity: open `Assets/Scenes/GazeContingencyStudyScene.unity`.
2. **Headset adb authorization is the current blocker.** macOS sees the HTC hardware (KONA-QRD,
   vendor 0x0bb4) over USB, but `adb devices` was empty → USB debugging not authorized. Put the
   Focus Vision on, tap **Allow** on "Allow USB debugging?" (check "Always allow from this
   computer"). Then `adb devices` should show `FA5AR3N00385  device`.
3. Build Profiles → Android active, Run Device = VIVE_Focus_Vision → **Build And Run** (⌘B).
4. Test on device: press **2**, read the passage during recording, confirm the assistant then
   speaks in the cloned voice; confirm `[VoiceMode]`/`[VoiceAssist]`/`[VoiceSynth]` logcat lines.

## Gotchas

- **adb version conflict:** system adb is `36.0.2`, Unity's bundled adb is `36.0.0`. Running system
  `adb` starts a server that evicts Unity's, making the device flap in/out of Unity's Run Device
  list. Fix: point Unity → Settings → External Tools → Android SDK at the system SDK
  (`~/Library/Android/sdk`) so only one adb revision is used, or avoid running system `adb` while
  Unity holds the device.
- A previous Editor session crashed (see `mono_crash.*` in the `podgorica` worktree, not committed).

## ⚠️ IRB constraint — DEV ONLY

This live/cloud path (headset holds the Mistral key, uploads the participant's voice to
`api.mistral.ai`, clone lives on Mistral's servers) is **NOT** what the approved IRB packet
describes (offline OpenVoice, no third party, no on-device key, 24 h deletion). Use it only for
DEV/testing with the researcher's own voice. Real-participant use requires an IRB modification.
The production path is offline batch-render of the finite phrase set + ship clips (no network,
no key on headset) — sketched in the original plan.

## Commits

- `825c5d5` feat(voice): show reading passage + countdown on enroll, persist clone
- `75ef3d1` feat(voice): add self-similar voice launch mode (Mistral Voxtral)
- `bbfdcef` Merge branch 'feat/self-similar-voice' into v2

## Paused (not merged)

A test-enabling refactor (`Assets/VoiceCloning/` asmdef + pure helpers + EditMode tests) was
started in `podgorica` and paused per the user. It was intentionally left out of the merge.
