# Gaze Contingency Project

A mixed reality research study investigating how gaze-contingent AI assistance affects user performance, cognitive load, and task satisfaction in spatial search tasks.

## Research Question

> How does a gaze-contingent AI agent affect user performance, cognitive load, and task satisfaction in mixed reality object search tasks compared to a standard AI agent without gaze awareness?

## Setup

- **Hardware:** HTC Vive Focus Vision (standalone, integrated eye tracking)
- **Platform:** Unity 6, OpenXR, VIVE native APIs
- **AI Pipeline:** OpenAI GPT-4o-mini (hint generation) + ElevenLabs TTS (speech synthesis)

## Architecture

All components auto-attach at runtime to `ObjectSpawner` via `[RuntimeInitializeOnLoadMethod]`:

- `FindObjectGameManager` — game state machine, objective tracking, grab validation
- `ShapeObjectFactory` — assigns shapes/colors/colliders to spawned objects
- `VoiceAssistantController` — orchestrates the AI voice assistant pipeline
- `AgentContext` — builds structured scene snapshots (including gaze data) for the LLM
- `HintGenerator` — adaptive timing + OpenAI API calls for contextual hints
- `VoiceSynthesizer` — ElevenLabs TTS with interruption support
- `GazeHighlightManager` — orange glow on gaze-hovered objects
- `GazeDataLogger` — per-frame CSV logging of gaze telemetry

## API Keys

Place in `Assets/StreamingAssets/api_keys.json`:
```json
{
  "openai_key": "sk-...",
  "elevenlabs_key": "..."
}
```

## Current Development

See [Enhanced Gaze Contingency Plan](docs/enhanced-gaze-contingency-plan.md) for the active implementation plan covering:
- Task redesign: 20 objects across 3 vertical shelf levels
- Gaze coverage tracking and behavior classification
- Adaptive hint timing driven by gaze patterns
- Enhanced LLM context with gaze history and zone coverage
