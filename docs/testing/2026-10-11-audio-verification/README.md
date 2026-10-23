# Neutral speech verification evidence — 2026-10-11

Two Eric cache clips contained trailing speech absent from their approved scripts.
The original “Look left, please.” clip produced the live Scribe transcript
“Look left plea- Leee SSS knees”. Its SHA-256 is
`fc0e8723298e34f4e275d200db18ac36276f763a62bf1d55fbc8fc4980e6f450`.
The other quarantined original, “Now, look left.”, has SHA-256
`eef46d428fdadf810af0006cafcd8661949b0fa74126e8d82ab04876fb9d0bc2`.
Local Whisper base and small both reported unwanted trailing words for these originals.

An initial replacement passed local Whisper but still contained added speech according
to live Scribe; it was superseded before delivery. This demonstrates why a successful
local transcript alone was insufficient evidence of a clean replacement.

`replacement-results.json` records the two final live Scribe checks and exact replacement
hashes. Both final replacements used neutral Eric, `eleven_turbo_v2_5`, English,
stability 0.8, similarity boost 0.75, style 0 and speaker boost enabled. These manual
repair settings are evidence about these clips; runtime synthesis settings were not
changed. The final files and matching receipts were copied to the Vive cache and hashes
verified. Prior bytes remain backed up on device; trial records were not edited.

Runtime policy: `scribe-v2-verbatim-v1`, no expected-text hint, verbatim transcription,
reported audio events retained, exact normalized-word matching and up to three fresh
synthesis attempts. Matching receipts are required for new audio-library admission.
Tests: `./scripts/test-voice-isolation.sh`; Unity Tools > Codex > Check Study Integration.
Both passed. Acoustic quality remains a wearer check: ASR can miss defects and can
reject valid speech. No claim of perfect audio detection or full-library live Scribe
validation is made.
