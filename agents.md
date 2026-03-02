# Gaze Contingency Project — agents.md

## Overview

Unity mixed reality project targeting the **HTC Vive Focus Vision** headset. The project implements gaze-contingent interactions in a passthrough (video see-through) mixed reality environment. Built from Unity's MR Template with HTC Vive OpenXR integration.

## Research Context

**Gaze-Contingent AI Assistance in Mixed Reality Object Search Tasks**

This is a research project investigating whether an AI agent with access to user gaze data (gaze-contingent) outperforms a standard AI agent in helping people find virtual objects in mixed reality.

### Study Design

- **Two conditions**: Gaze-aware AI vs. Standard AI (without gaze data)
- **Task**: Participants search for 7 virtual objects digitally placed on a real 1m x 1m table/island while wearing an HTC Vive Focus Vision headset
- **Gaze-aware AI**: Provides contextual hints based on where the user is currently looking at the virtual objects
- **Standard AI**: Gives general, time-based suggestions without gaze information

### Expected Outcomes

- Faster task completion
- Lower cognitive load
- Higher user satisfaction
- More relevant and timely AI assistance

## Hardware & Platform

- **Headset**: HTC Vive Focus Vision (standalone Android-based)
- **Build target**: Android (ARM64, min SDK 32)
- **Input methods**: Eye gaze tracking, hand tracking, head tracking (fallback)
- **Display mode**: Passthrough (video see-through mixed reality)

## Tech Stack

- **Engine**: Unity 6000.3.10f1
- **Render pipeline**: Universal Render Pipeline (URP) 17.3.0
- **XR runtime**: OpenXR 1.16.1
- **HTC SDK**: Vive OpenXR 2.2.0 (scoped registry: `https://npm-registry.vive.com`)
- **Interaction**: XR Interaction Toolkit 3.3.1
- **Hand tracking**: XR Hands 1.7.3
- **AR features**: AR Foundation 6.3.3
- **Input**: Unity Input System 1.18.0
- **Serialization**: Newtonsoft JSON 3.2.2

## Project Structure

```
Assets/
├── VivePassthrough.cs              # HTC passthrough initialization (underlay)
├── Scenes/
│   └── SampleScene.unity           # Main scene (only scene in build)
├── MRTemplateAssets/
│   ├── Scripts/                    # Core application scripts
│   ├── Prefabs/                    # Spawnable objects, UI elements
│   ├── Materials/
│   ├── Models/
│   ├── Textures/
│   ├── Audio/
│   └── Tutorial/                   # Onboarding content
├── XR/
│   ├── Settings/                   # OpenXR feature configuration
│   └── Loaders/                    # OpenXRLoader.asset, SimulationLoader.asset
├── XRI/                            # XR Interaction configuration
├── CompositionLayers/              # HTC composition layer support
└── Samples/                        # Imported XR Hands & Interaction Toolkit samples
```

## Key Scripts

| Script | Path | Purpose |
|--------|------|---------|
| `VivePassthrough.cs` | `Assets/` | Creates planar passthrough via `PassthroughAPI.CreatePlanarPassthrough(LayerType.Underlay)` |
| `ARFeatureController.cs` | `Assets/MRTemplateAssets/Scripts/` | Central controller toggling AR features (planes, bounding boxes, passthrough, occlusion) |
| `GazeTooltips.cs` | `Assets/MRTemplateAssets/Scripts/` | Shows contextual tooltips on surfaces using gaze raycasting |
| `GoalManager.cs` | `Assets/MRTemplateAssets/Scripts/` | Onboarding flow: FindSurfaces → TapSurface with coaching UI |
| `SpawnedObjectsManager.cs` | `Assets/MRTemplateAssets/Scripts/` | Manages spawned objects with persistent XR anchors (async) |
| `HandSubsystemManager.cs` | `Assets/MRTemplateAssets/Scripts/` | Starts/stops XR hand tracking subsystem |
| `OcclusionManager.cs` | `Assets/MRTemplateAssets/Scripts/` | Hand and object occlusion with platform-specific paths |
| `XRBlaster.cs` | `Assets/MRTemplateAssets/Scripts/` | Grab-to-aim interactive weapon demo |
| `LaunchProjectile.cs` | `Assets/MRTemplateAssets/Scripts/` | Physics-based projectile launching |
| `SaveAndLoadAnchorDataToFile.cs` | `Assets/MRTemplateAssets/Scripts/` | Persists XR anchor data to device storage |

## XR Features Enabled

- **Passthrough**: Video see-through via HTC Vive OpenXR `PassthroughAPI`
- **Eye gaze tracking**: Primary input with head-tracking fallback (`GazeInputManager.cs` in Samples)
- **Hand tracking**: Dual-hand finger tracking with pinch/poke gestures
- **AR plane detection**: Horizontal and vertical surface detection
- **AR bounding boxes**: Object classification in physical space
- **Hand occlusion**: Realistic hand-virtual object interaction
- **XR anchors**: Spatial persistence across sessions
- **Composition layers**: HTC-optimized rendering layers

## Custom Layers

- Layer 6: `Spawner Contact` — objects that can spawn interactive items
- Layer 7: `Placeable Surface` — AR planes where objects can be placed

## Build & Deploy

The project builds to Android APK for sideloading onto the Vive Focus Vision. The build includes only `Assets/Scenes/SampleScene.unity` (index 0).

Editor development uses `SimulationLoader.asset` for XR simulation without hardware.

## Conventions

- C# scripts follow Unity conventions (MonoBehaviour, `[SerializeField]`, UnityEvents)
- Manager pattern for subsystem controllers
- Async/await for anchor persistence operations
- Prefab-based spawning system
- Forward rendering path (mobile-optimized)
- Linear color space

## Scoped Registries

The project uses a scoped registry for HTC packages:
```json
{
  "name": "VIVE",
  "url": "https://npm-registry.vive.com",
  "scopes": ["com.htc.upm"]
}
```
