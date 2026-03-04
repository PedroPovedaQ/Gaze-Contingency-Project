# Gaze Contingency Project — agents.md

## Overview

Unity 6 mixed reality project targeting the **HTC Vive Focus Vision**. Passthrough (video see-through) MR environment for a research study on gaze-contingent AI assistance. Built from Unity's MR Template with HTC Vive OpenXR integration.

**Study**: Participants search for 7 virtual objects on a real 1m x 1m table wearing the headset. Two conditions compare gaze-aware AI hints vs. standard AI hints.

## Hardware & Build

- **Headset**: HTC Vive Focus Vision (standalone Android)
- **Build**: Android ARM64, min SDK 32, Linear color space, URP forward rendering
- **Input**: Eye gaze tracking (primary), hand tracking, head tracking (fallback)
- **Scene**: Only `Assets/Scenes/SampleScene.unity` in build (index 0)

## Assembly Definitions (Critical)

Scripts live in separate assemblies. You MUST understand these boundaries — code in one assembly cannot reference types in another unless explicitly declared.

| Assembly | Location | Key Contents |
|----------|----------|--------------|
| `VivePlaneIntegration` | `Assets/Scripts/` | VivePlaneData, VivePlaneProvider |
| `MRTemplate` | `Assets/MRTemplateAssets/` | ARFeatureController, GoalManager, GazeTooltips, all template scripts |
| `ARStarterAssets` | `Assets/Samples/XR Interaction Toolkit/3.3.0/AR Starter Assets/` | ARContactSpawnTrigger, ARInteractorSpawnTrigger, ObjectSpawner |
| `VIVE.OpenXR` | Package: `com.htc.upm.vive.openxr` | PlaneDetectionManager, VivePlaneDetection, PassthroughAPI |

**Dependency chain**: Both `MRTemplate` and `ARStarterAssets` reference `VivePlaneIntegration`. `VivePlaneIntegration` references only `VIVE.OpenXR`. Custom scripts in `Assets/Scripts/` cannot import `UnityEngine.XR.ARFoundation` directly.

When adding new scripts, check which assembly they'll land in and whether the needed references exist in that `.asmdef`.

## VIVE vs AR Foundation (Key Architecture Issue)

**VIVE Focus Vision does NOT register an XRPlaneSubsystem with AR Foundation.** `ARPlaneManager.subsystem` is always null on device. The MR Template was built for Meta Quest's AR Foundation integration.

**Solution implemented**: `VivePlaneProvider` bridges VIVE's native `PlaneDetectionManager` API to the existing pipeline. `ARFeatureController.TogglePlanes()` checks if subsystem is null and delegates to the VIVE provider.

The same pattern may apply to other AR Foundation features (bounding boxes, etc.) — always verify subsystem availability on VIVE before relying on AR Foundation APIs.

### VIVE Plane Detection API

```
using VIVE.OpenXR;                                          // XrResult
using VIVE.OpenXR.Toolkits.PlaneDetection;                  // PlaneDetectionManager, PlaneDetector, PlaneDetectorLocation
using static VIVE.OpenXR.PlaneDetection.VivePlaneDetection; // Enums: XrPlaneDetectionStateEXT, XrPlaneDetectorOrientationEXT, etc.
```

Workflow: `PlaneDetectionManager.IsSupported()` -> `CreatePlaneDetector()` -> `pd.BeginPlaneDetection()` -> poll `GetPlaneDetectionState()` until `DONE_EXT` -> `GetPlaneDetections()` -> `DestroyPlaneDetector()`.

VIVE planes come from Room Setup (pre-defined, not runtime). Detection completes near-instantly. Planes are rectangular with pose already converted to Unity coords (`Quaternion.Euler(-90, 180, 0)` applied). Mesh vertices in XY plane, normal = local +Z = `transform.forward`.

## Custom Layers (Hardcoded in Scripts)

- **Layer 6**: `Spawner Contact` — ContactSpawnTrigger physics detection
- **Layer 7**: `Placeable Surface` — All detected planes (AR Foundation AND VIVE). Hardcoded in `VivePlaneProvider.cs` (`m_PlaneLayer = 7`). Changing layer assignments requires updating script constants.

## Key Scripts

| Script | Assembly | Purpose |
|--------|----------|---------|
| `VivePlaneProvider.cs` | VivePlaneIntegration | Detects VIVE Room Setup planes, creates GameObjects with MeshCollider on Layer 7 |
| `VivePlaneData.cs` | VivePlaneIntegration | Plane metadata component (Center, Normal from transform; Size, Orientation, SemanticType) |
| `ARFeatureController.cs` | MRTemplate | Central AR feature toggle. Checks subsystem null, falls back to VivePlaneProvider |
| `ARContactSpawnTrigger.cs` | ARStarterAssets | Physics trigger for spawning on planes. Falls back to VivePlaneData when ARPlane absent |
| `GoalManager.cs` | MRTemplate | Onboarding: FindSurfaces -> TapSurface. Calls `ARFeatureController.TogglePlanes(true)` |
| `GazeTooltips.cs` | MRTemplate | Gaze raycasting tooltips on surfaces |
| `ObjectSpawner.cs` | ARStarterAssets | Spawns objects given `(Vector3 position, Vector3 normal)` — agnostic to plane source |
| `VivePassthrough.cs` | Assembly-CSharp | HTC passthrough init via `PassthroughAPI.CreatePlanarPassthrough(LayerType.Underlay)` |
| `SpawnedObjectsManager.cs` | MRTemplate | Manages spawned objects with persistent XR anchors |
| `OcclusionManager.cs` | MRTemplate | Hand/object occlusion with platform-specific paths |
| `EyeGazeRayVisual.cs` | Assembly-CSharp | Orange line visual for eye gaze ray, toggleable via hand menu |
| `GazeToggleConnector.cs` | Assembly-CSharp | Wires hand menu Toggle to `ARFeatureController.ToggleGazeRay` |
| `GazeHighlightManager.cs` | Assembly-CSharp | Orange edge glow on gaze hover via MaterialPropertyBlock |
| `GazeDataLogger.cs` | Assembly-CSharp | Per-frame CSV logging of gaze pose + hovered object metadata |
| `ShapeObjectFactory.cs` | Assembly-CSharp | Auto-bootstrapping factory: random shape/color, enables gaze interaction |
| `SpawnableObjectInfo.cs` | Assembly-CSharp | Metadata component: shapeName, colorName, DisplayName |

## Spawnable Objects System

Objects are spawned by `ObjectSpawner` (triggered by tapping an AR plane) and immediately post-processed by `ShapeObjectFactory`.

**ShapeObjectFactory** (`Assets/ShapeObjectFactory.cs`) self-bootstraps via `[RuntimeInitializeOnLoadMethod]` — zero manual Inspector setup. It hooks into `ObjectSpawner.objectSpawned` and transforms each spawned object:

1. Swaps mesh to a random shape: **Sphere**, **Cube**, or **Pyramid** (meshes auto-created from `GameObject.CreatePrimitive` + procedural pyramid)
2. Assigns a random color from the pool: **Red** `(0.9, 0.15, 0.15)`, **Blue** `(0.15, 0.35, 0.9)`, **Yellow** `(0.95, 0.85, 0.1)`, **Purple** `(0.6, 0.15, 0.85)`
3. Sets `XRGrabInteractable.allowGazeInteraction = true` (required for gaze hover events — see below)
4. Adds `SpawnableObjectInfo` metadata component and renames object (e.g. `"Red_Sphere"`)

**SpawnableObjectInfo** (`Assets/SpawnableObjectInfo.cs`): Simple component with `shapeName`, `colorName`, and `DisplayName` property. Used by `GazeDataLogger` to write shape/color to CSV.

- Object pool: 3 shapes x 4 colors = 12 possible combinations
- Scale: `(0.1, 0.1, 0.1)` — produces ~10cm objects
- Material: Per-instance copy of `InteractablePrimitive` shader with `_BaseColor` set to the chosen color

## Eye Gaze Tracking & Visual Ray

Eye tracking pipeline from hardware to interaction events:

1. **Hardware**: VIVE Focus Vision eye tracking via OpenXR extensions `XR_EXT_eye_gaze_interaction` and `XR_HTC_eye_tracker`
2. **TrackedPoseDriver**: On the Gaze Interactor GameObject, drives transform from eye gaze pose
3. **XRGazeInteractor**: Built-in XRI component. Casts a ray from the user's eye gaze direction. Generates `hoverEntered`/`hoverExited` events when the ray intersects objects with `XRGrabInteractable` that have `allowGazeInteraction = true`
4. **EyeGazeRayVisual** (`Assets/EyeGazeRayVisual.cs`): Configures an always-orange `XRInteractorLineVisual` on the Gaze Interactor. Applies `m_FlipX` correction for VIVE left/right eye swap. Toggleable via hand menu.
5. **GazeToggleConnector** (`Assets/GazeToggleConnector.cs`): Wires hand menu Toggle UI to `ARFeatureController.ToggleGazeRay(bool)`

**Critical**: Objects MUST have `XRGrabInteractable.allowGazeInteraction = true` for gaze hover events to fire. Without this, the XRGazeInteractor skips the object entirely during hover evaluation. `ShapeObjectFactory` sets this flag at spawn time.

## Gaze Highlight System

Visual feedback when gaze or controller ray hovers over an object:

- **GazeHighlightManager** (`Assets/GazeHighlightManager.cs`): Attached to the Gaze Interactor. Listens to `hoverEntered`/`hoverExited`. Applies orange `_EdgeHighlightColor` via `MaterialPropertyBlock` in `LateUpdate`. Runs at `[DefaultExecutionOrder(100)]` to override the affordance system (order 0).
- **Shader**: `InteractablePrimitive.shadergraph` with properties `_EdgeHighlightColor` (Color) and `_EdgeHighlightFalloff` (float, 1.5)
- **Controller glow**: Handled by the XRI affordance system on interactable prefabs (`XRInteractableAffordanceStateProvider` → `ColorMaterialPropertyAffordanceReceiver` → `MaterialPropertyBlockHelper` → `_EdgeHighlightColor`). Theme: `EdgeColorAffordanceTheme.asset` (hovered = light blue)

**Glow precedence**:

| Scenario | Result |
|----------|--------|
| Controller only hovering | Blue edge glow (affordance system) |
| Gaze only hovering | Orange edge glow (GazeHighlightManager) |
| Both hovering | Orange wins (LateUpdate order 100 overrides affordance order 0) |
| Gaze exits, controller stays | Blue restored by affordance system |

## Gaze Data Logging (CSV Telemetry)

**GazeDataLogger** (`Assets/GazeDataLogger.cs`) logs per-frame gaze pose and hover state to CSV. Runs always, regardless of whether the gaze ray visual is visible.

**CSV file path**: `Application.persistentDataPath/gaze_log_{timestamp}.csv`
- On device: `/sdcard/Android/data/com.DefaultCompany.MRTemplateProject/files/gaze_log_*.csv`
- Retrieve: `adb pull /sdcard/Android/data/com.DefaultCompany.MRTemplateProject/files/gaze_log_*.csv`

**CSV columns**:

| Column | Type | Description |
|--------|------|-------------|
| `timestamp` | float (F3) | `Time.time` — seconds since app start |
| `frame` | int | `Time.frameCount` |
| `pos_x`, `pos_y`, `pos_z` | float (F4) | Gaze interactor world position |
| `rot_x`, `rot_y`, `rot_z` | float (F2) | Gaze interactor euler angles (degrees) |
| `hovered_object` | string | Name of hovered object (e.g. `"Red_Sphere"`) or empty |
| `hovered_shape` | string | `"Sphere"`, `"Cube"`, or `"Pyramid"` (from SpawnableObjectInfo) or empty |
| `hovered_color` | string | `"Red"`, `"Blue"`, `"Yellow"`, or `"Purple"` (from SpawnableObjectInfo) or empty |
| `ray_visible` | int (0/1) | Whether the XRInteractorLineVisual is enabled |

**Data pipeline**: `XRGazeInteractor` → `hoverEntered` event → `GazeDataLogger.OnHoverEntered` reads `SpawnableObjectInfo` → writes shape/color each frame → `hoverExited` clears fields. Flush interval: every 300 frames.

## Scene Hierarchy (SampleScene)

Root objects: `Environment`, `Lighting`, `MR Interaction Setup` (has VivePassthrough + 7 children including XR Origin), `UI` (4 children), `Permissions Manager`, `VivePlaneProvider`.

Most scene objects are prefab instances from `Assets/MRTemplateAssets/Prefabs/` or from the MR Interaction Setup prefab. Modifying scene structure often requires modifying source prefabs.

## Unity MCP

Config in `.mcp.json` connects to HTTP server at `http://127.0.0.1:8080/mcp` (hosted by the `com.coplaydev.unity-mcp` Unity package). Server must be running in the Unity Editor for MCP tools to work. After domain reload, the MCP session may need re-initialization (session IDs expire).

## Tech Stack Versions

| Package | Version |
|---------|---------|
| Unity | 6000.3.10f1 |
| URP | 17.3.0 |
| OpenXR | 1.16.1 |
| VIVE OpenXR | 2.5.1 (GitHub) |
| XR Interaction Toolkit | 3.3.1 |
| XR Hands | 1.7.3 |
| AR Foundation | 6.3.3 |
| Android XR OpenXR | 1.1.0 |
| Input System | 1.18.0 |

VIVE package source: `https://github.com/ViveSoftware/VIVE-OpenXR-Unity.git?path=/com.htc.upm.vive.openxr#versions/2.5.1`

## Conventions

- C# scripts: MonoBehaviour, `[SerializeField]`, UnityEvents, `m_` prefix for private serialized fields
- Manager pattern for subsystem controllers
- Prefab-based spawning with Layer 7 physics interaction
- VIVE scoped registry: `https://npm-registry.vive.com` (scopes: `com.htc.upm`)
