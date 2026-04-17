# Gaze Contingency Project

A mixed reality research study investigating how gaze-contingent AI assistance affects user performance, cognitive load, and task satisfaction in spatial search tasks.

## Research Question

> How does a gaze-contingent AI agent affect user performance, cognitive load, and task satisfaction in mixed reality object search tasks compared to a standard AI agent without gaze awareness?

## Requirements

- **Unity** 6000.3.10f1 (Unity 6 LTS)
- **Hardware:** HTC Vive Focus Vision (standalone Android ARM64, integrated eye tracking)
- **Android SDK:** minimum API level 32
- **Scene:** `Assets/Scenes/SampleScene.unity` (single scene build)

## Getting Started

1. Open the project in Unity 6000.3.10f1
2. Switch build target to **Android** (File > Build Settings)
3. Place API keys in `Assets/StreamingAssets/api_keys.json`:
   ```json
   {
     "openai_key": "sk-...",
     "elevenlabs_key": "..."
   }
   ```
   This file is gitignored and must be created locally.
4. Connect Vive Focus Vision via USB with **Developer Mode** and **USB Debugging** enabled
5. Build and Run

## Compile Lint Guard

To catch C# compile errors automatically before commits:

1. Install hooks once:
   ```bash
   ./scripts/install-git-hooks.sh
   ```
2. Run manual check anytime:
   ```bash
   ./scripts/unity-compile-check.sh
   ```

Notes:
- If Unity Editor is open, the script reads the latest compiler output from `Editor.log`.
- If Unity Editor is closed, it runs a headless batch compile check.
- Emergency bypass for one commit: `SKIP_UNITY_LINT=1 git commit ...`

## How It Works

The player taps a detected table surface to start the game. Each round spawns 42 virtual objects arranged on a deterministic bookshelf layout (2 columns × 7 rows) anchored to the detected table. The object pool currently uses 6 shapes × 4 colors, and the player must find the current target object via eye-gaze dwell capture.

An AI voice assistant watches the player's eye gaze in real-time and provides spoken hints:

- **Adaptive timing:** Hints fire every 5-15 seconds depending on gaze behavior — faster when the player is lost, slower when scanning systematically
- **Gaze-aware context:** The LLM receives which objects the player has looked at, for how long, which shelf levels they've scanned, and their recent gaze history
- **Triggered hints:** Zone neglect (haven't looked at a shelf), revisit confusion (staring at the same wrong object), gaze nudge (looking at the target but not grabbing it)

## Architecture

All components auto-attach at runtime to `ObjectSpawner` via `[RuntimeInitializeOnLoadMethod]` — no manual Inspector wiring needed.

### Game Logic
| Component | File | Role |
|-----------|------|------|
| `FindObjectGameManager` | `Assets/FindObjectGameManager.cs` | Game state machine, objective tracking, grab validation, object freezing |
| `ShapeObjectFactory` | `Assets/ShapeObjectFactory.cs` | Assigns shape mesh (Sphere/Cube/Pyramid/Cylinder/Star), color, colliders |
| `ShelfSpawner` | `Assets/ShelfSpawner.cs` | Creates virtual shelf platforms above detected table, computes spawn grid per level |
| `FindObjectUI` | `Assets/FindObjectUI.cs` | World-space HUD with LazyFollow (objective, progress, timer) |
| `SpawnableObjectInfo` | `Assets/SpawnableObjectInfo.cs` | Per-object metadata: shape, color, shelf level |

### AI Voice Assistant
| Component | File | Role |
|-----------|------|------|
| `VoiceAssistantController` | `Assets/VoiceAssistantController.cs` | Orchestrator — wires all sub-components, subscribes to game events |
| `AgentContext` | `Assets/AgentContext.cs` | Builds structured text prompt for LLM (scene state, gaze data, coverage) |
| `HintGenerator` | `Assets/HintGenerator.cs` | Adaptive timing + OpenAI GPT-4o-mini calls with gaze-aware situation descriptions |
| `VoiceSynthesizer` | `Assets/VoiceSynthesizer.cs` | ElevenLabs TTS (eleven_turbo_v2_5, "Rachel" voice) with interruption support |

### Eye Gaze System
| Component | File | Role |
|-----------|------|------|
| `GazeCoverageTracker` | `Assets/GazeCoverageTracker.cs` | Tracks per-object fixation time, gaze history, zone coverage, behavior classification |
| `GazeHighlightManager` | `Assets/GazeHighlightManager.cs` | Orange edge glow on gaze-hovered objects via MaterialPropertyBlock |
| `GazeDataLogger` | `Assets/GazeDataLogger.cs` | Per-frame CSV telemetry (position, rotation, hovered object, ray visibility) |
| `EyeGazeRayVisual` | `Assets/EyeGazeRayVisual.cs` | Configures orange gaze ray visual with VIVE flip-X correction |

### MR Foundation
| Component | File | Role |
|-----------|------|------|
| `VivePlaneProvider` | `Assets/VivePlaneProvider.cs` | VIVE-native plane detection (bypasses AR Foundation subsystem limitation) |
| `VivePassthrough` | `Assets/VivePassthrough.cs` | Passthrough MR environment setup |
| `GazeToggleConnector` | `Assets/GazeToggleConnector.cs` | Runtime toggle for gaze ray visibility |

## Gaze Behavior Classification

`GazeCoverageTracker` classifies the player's gaze pattern every frame based on the last 10 fixation events:

| Behavior | Criteria | Hint Interval |
|----------|----------|---------------|
| **Systematic** | Long fixations (>1.5s), mostly unique objects | 12-15s |
| **Normal** | Default / moderate patterns | 9-12s |
| **Erratic** | Short fixations (<0.6s), rapid zone switching | 5-7s |
| **Stuck** | >70% of gaze in one zone, searching >15s | 7-10s |

## Data Collection

- **Gaze telemetry:** CSV files at `Application.persistentDataPath/gaze_log_*.csv`
- **Columns:** timestamp, frame, position (x,y,z), rotation, hovered object name/shape/color, ray visibility

## Documentation

- [`agents.md`](agents.md) — Technical architecture, assembly structure, VIVE-specific workarounds
- [`FUNCTIONALITY_GUIDE.md`](FUNCTIONALITY_GUIDE.md) — Detailed code walkthroughs for all systems
- [`docs/guide/README.md`](docs/guide/README.md) — Multi-part practical guide set (system overview, round flow, gaze/telemetry, build/debug workflow)
- [`docs/enhanced-gaze-contingency-plan.md`](docs/enhanced-gaze-contingency-plan.md) — Implementation plan for the gaze contingency enhancement
