# Spatial confetti audio — 2026-10-26

## Implemented

Correct target selection starts a 0.65-second pop and paper-rustle sound on the TargetCrumble root, at the selected renderer bounds center, with the explosion. Wrong selections and timeouts do not call this effect. The existing success chime and voice feedback are unchanged.

The original synthesized asset is mono 44.1 kHz, 16-bit PCM, peak amplitude 0.65; it combines a short decaying noise pop, a low descending impulse, and a modulated rustle tail (local deterministic seed 2601026). No external sound license or network generation is involved. Unity preloads the resource before scene startup and reuses it across bursts.

AudioSource uses fully 3D blending, logarithmic attenuation from 1 to 15 meters, volume 0.55, no looping and no Doppler. This uses Unity built-in positional panning/attenuation; the project has no HRTF spatializer configured. The source is owned by the independent 1.8-second burst root, so final-trial target deletion cannot cut it off. Researcher pause pauses/resumes the sound alongside the visual effect. Shared imported clip ownership remains with Unity. The replay captures the clip/start through the existing effect channel; replay audio is not a binaural reconstruction.

## Verification

Unity Check Study Feedback passed with new checks for a non-null mono clip, fully 3D audio at the target center, duration within effect lifetime, no Doppler/looping and survival after target deletion. Existing timeout, feedback, no-physics and experiment RNG checks also passed. git diff --check passed.

Android build succeeded in 96.1 seconds (1165.0 MB), installed and launched on Wi-Fi headset 192.168.1.208:5555 with Status: ok. In-headset listening remains required to judge loudness and localization.
