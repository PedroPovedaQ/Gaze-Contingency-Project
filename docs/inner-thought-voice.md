# Self-similar inner-thought voice pilot

**Implemented:** `inner-thought-v1` applies only to decoded self-similar speech. Neutral speech bypasses the effect. A 2.4 kHz one-pole low-pass contributes 40% of the direct voice; the original contributes 60%. Quiet filtered reflections at 24 ms (14%) and 52 ms (7%) suggest an internal, softened voice without pitch shifting or a separate cue. The perceptual result still needs listening evaluation; it is a creative treatment, not a validated simulation of inner speech.

Processing runs once when a clip enters the prepared library, before the existing RMS/headroom normalization. Raw MP3 caches stay unchanged. Duration and immediate direct onset are preserved, and reflections have no feedback or appended tail. Interrupting playback stops the whole treated clip. No AudioSource filter state can leak into neutral speech. The cached waveform, including its treatment, is what replay saves.

Each voice-manifest clip and `audio_playback_start` event carries `effect_profile` (`inner-thought-v1` or `dry`). Existing gain, peak and RMS fields describe the waveform after treatment and normalization calculations. Equal RMS is an engineering level control, not proof of equal perceived loudness.

**Protocol proposal:** use the treatment during pilot listening and assess clarity, self-recognition, comfort and perceived internal-voice quality. This changes the self-similar condition's timbre as well as its voice identity; document that distinction before interpreting condition effects. It is not yet a finalized run-sheet/IRB manipulation.

**Verification:** the voice-isolation suite exercises neutral bit-for-bit bypass, stereo channel independence, immediate direct onset, reflection timing, silence/reset, manifest identity and matched-library levels/headroom. Hardware listening is still needed, especially rapid interruptions and the transition back to neutral speech.
