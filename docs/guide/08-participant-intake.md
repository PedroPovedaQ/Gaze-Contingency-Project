# Participant intake from Trial Viewer

**Implemented — November 1, 2026:** the current operator instructions are [Consent-first audio intake and study setup](10-audio-intake.md). The October 25 prototype is superseded; its manual capture, generated follow-ups and concurrent cloning are no longer available.

New sessions use the ten fixed v3 questions, prospective consent, Selected Voice confirmation, River prompts and a tone before automatic answer capture. All questions and optional fallback precede human eligibility review; the reviewed eligibility and speech-adequacy gates precede cloning/cache preparation. Pause, Repeat, Skip and withdrawal remain available.

**Implemented service boundary:** participant answer audio goes to ElevenLabs for transcription and cloning. Prompts and study speech use ElevenLabs synthesis. The worker reads only `elevenlabs_key` from private `~/.config/follow-my-voice/api_keys.json`; no credentials are packaged in the desktop app. Generated follow-up methods and options have been deleted. The [network audit](../study-network-hosts.md) distinguishes this flow from optional development/evaluation tools.

**Implemented storage:** `~/Documents/Trial Review Intake/<session UUID>/` retains private answer audio, full transcripts, corrections and exports, linked to P###. The headset receives the minimal validated session assignment and prepared voice cache, without intake answers or transcripts. Old sessions retain their recorded versions; historical provider labels remain readable without network access.

**Open decision:** approved consent/protocol wording, retention/deletion arrangements, survey release and wearer rehearsal remain pending. Software checks do not establish ethics approval or microphone/clone quality.

## Offline verification

- `python3 scripts/check-study-providers.py`
- `python3 -m unittest discover -s tools/trial-review -p 'test_*.py'`
- `python3 -m unittest discover -s scripts/tests -p 'test_study_providers.py'`

These checks use synthetic data and fake providers, without microphone capture or live provider requests.
