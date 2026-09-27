# Self-similar inner-thought voice

**Implemented:** `inner-thought-v2-soft-close` (replaces the inaudible `inner-thought-v1`) applies only to decoded self-similar speech; neutral speech returns the decoded buffer untouched. It was chosen by listening from six prototypes (soft clone, LPC whisper, voice + whisper double, breath/swell overlay, echo, ElevenLabs v3 whisper). The inner quality comes mainly from the performance of a softly-read clone; the processing is close-mic polish only:

- 80 Hz high-pass, +3 dB low shelf at 200 Hz (proximity warmth), -1.5 dB peak at 3 kHz (Q 1);
- 2.5:1 levelling above 1.4x the phrase's own RMS (5 ms attack, 150 ms release);
- a dark stereo room: fixed-seed noise impulse, 3.5 kHz low-pass, 0.35 s RT60, 8 ms gap, different seed per ear, mixed at -24 dB. A 0.4 s tail is appended so it can decay.

The words start at sample 0 (no onset delay relative to neutral speech). Output is stereo; `AudioClip.Create` replaces the decoded clip. Rendering is deterministic.

**Level matching:** RMS is measured over the words only (`speech_samples`, the leading interleaved samples before the tail), both on first load and in `MatchLibraryLevels`, so the tail never raises the clone's speech level. Peak/headroom limits still use the whole clip. Each manifest clip records `effect_profile` and `speech_samples`; `audio_playback_start` records `effect_profile`. Equal RMS is an engineering control, not proof of equal perceived loudness.

**Performance dependency — protocol decision open:** the prototype that was selected cloned the voice from a *soft* reading of the enrollment passage. The approved run sheet and protocol say "Read the displayed passage in your natural voice after the tone," and the on-screen recording text says "Use your natural guiding voice." Neither was changed. With a natural-voice enrollment, only the close-mic treatment applies. Asking for a soft reading changes participant-facing instructions and the self-similar manipulation, and needs protocol review first. Clone ids persisted in PlayerPrefs keep whichever style they were enrolled with.

**Verification:** the voice-isolation suite checks exact neutral bypass, stereo output with the 0.4 s tail, zero onset delay, centred direct sound before the 8 ms room gap, decorrelated room per ear, tail decay, determinism, silence, malformed PCM rejection, manifest identity, words-only level matching and headroom. The C# output matched the listening prototype within 0.5 dB (0.98–0.99 correlation; room noise seeds differ). It takes about 45–100 ms per phrase on a desktop CPU. It runs during library preparation, or on first use for on-demand phrases; headset timing and listening are still pending.

**Reproduce the prototypes:** they are local-only (`.context/inner-thought/v2/`, gitignored). They used Voxtral clones of the ElevenLabs Rachel voice read normally, softly (`[softly]`) and whispered. The neutral lines used the external wording; the thinking lines used first-person wording.
