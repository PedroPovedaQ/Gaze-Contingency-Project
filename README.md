# Gaze Contingency Project

A mixed reality research study investigating how gaze-contingent AI assistance affects user performance, cognitive load, and task satisfaction in spatial search tasks for CAP 6117- Mixed Reality Project at UCF.

## Research Question

> How does a gaze-contingent AI agent affect user performance, cognitive load, and task satisfaction in mixed reality object search tasks compared to a standard AI agent without gaze awareness?

## Requirements

- **Unity** 6000.3.10f1 (Unity 6 LTS)
- **Hardware:** HTC Vive Focus Vision (standalone Android ARM64, integrated eye tracking)
- **Android SDK:** minimum API level 32
- **Scene:** `Assets/Scenes/GazeContingencyStudyScene.unity` (single scene build)

## Getting Started

1. Open the project in Unity 
2. Confirm the build scene is `Assets/Scenes/GazeContingencyStudyScene.unity`
3. Switch build target to **Android** in Build Settings
4. Place the ElevenLabs key in `Assets/StreamingAssets/api_keys.json`:
   ```json
   {
     "elevenlabs_key": "..."
   }
   ```
   This file is gitignored and must be created locally.
5. Connect the Vive Focus Vision over USB with **Developer Mode** and **USB Debugging** enabled
6. Verify the headset is visible to `adb`:
   ```bash
   adb devices
   ```
7. Build and deploy from the repo root:
   ```bash
   ./scripts/refocus-unity-and-build-device.sh
   ```
8. After the run, pull the recorded data if needed:
   ```bash
   adb pull /sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeData ./GazeData
   ```

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
| `HintGenerator` | `Assets/HintGenerator.cs` | Adaptive timing plus local gaze-aware and gaze-unaware hint selection |
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

| Behavior | Criteria |
|----------|----------|
| **Systematic** | Long fixations (>1.5s), mostly unique objects |
| **Normal** | Default / moderate patterns |
| **Erratic** | Short fixations (<0.6s), rapid zone switching |
| **Stuck** | >70% of gaze in one zone, searching >15s |

## Data Collection

- **Gaze telemetry:** CSV files at `Application.persistentDataPath/gaze_log_*.csv`
- **Columns:** timestamp, frame, position (x,y,z), rotation, hovered object name/shape/color, ray visibility

## Documentation

- [`agents.md`](agents.md) — Technical architecture, assembly structure, VIVE-specific workarounds
- [`FUNCTIONALITY_GUIDE.md`](FUNCTIONALITY_GUIDE.md) — Detailed code walkthroughs for all systems
- [`docs/guide/README.md`](docs/guide/README.md) — Multi-part practical guide set (system overview, round flow, gaze/telemetry, build/debug workflow)
- [`docs/enhanced-gaze-contingency-plan.md`](docs/enhanced-gaze-contingency-plan.md) — Implementation plan for the gaze contingency enhancement
- [`docs/rotational-ar-search-experiment-direction.md`](docs/rotational-ar-search-experiment-direction.md) — Proposed redesign: seated participant, rotation-only AR object search around the body
- [`docs/system-description-draft.md`](docs/system-description-draft.md) — Manuscript-style system description draft
- [`docs/experiment-section-draft.md`](docs/experiment-section-draft.md) — Manuscript-style experiment section draft

## Key Files

- `Assets/FindObjectGameManager.cs` — main experiment state machine and round flow
- `Assets/FindObjectUI.cs` — world-space study UI, start prompt, timer, completion flow
- `Assets/HintGenerator.cs` — gaze-aware and gaze-unaware hint timing and content
- `Assets/VoiceAssistantController.cs` — spoken instructions, round announcements, and feedback cues
- `Assets/GazeDataLogger.cs` — per-frame gaze logging and blink metrics
- `Assets/TrialDataLogger.cs` — event log and per-run summary metrics
- `Assets/Scenes/GazeContingencyStudyScene.unity` — main Unity scene in the build
- `Assets/Scripts/VivePlaneProvider.cs` — Vive plane detection bridge used instead of AR Foundation plane subsystems
- `analysis/run_analysis.py` — main analysis entry point
- `scripts/refocus-unity-and-build-device.sh` — default build-and-run command for the headset
