# Gaze Contingency Project — Functionality Guide

This guide explains how every piece of the VR "Find the Object" game works, from plane detection to eye gaze tracking to the AI voice assistant. It is written for someone new to Unity and assumes no prior knowledge of XR development.

---

## Table of Contents

1. [How Unity Works (Quick Primer)](#1-how-unity-works-quick-primer)
2. [Project Architecture Overview](#2-project-architecture-overview)
3. [Auto-Attach Pattern](#3-auto-attach-pattern)
4. [Plane Detection — How the Table Is Found](#4-plane-detection--how-the-table-is-found)
5. [Object Spawning — What Happens When You Tap](#5-object-spawning--what-happens-when-you-tap)
6. [Shape and Color Assignment](#6-shape-and-color-assignment)
7. [The Find the Object Game Mode](#7-the-find-the-object-game-mode)
8. [Heads-Up Display (HUD)](#8-heads-up-display-hud)
9. [Eye Gaze Tracking](#9-eye-gaze-tracking)
10. [Gaze Highlighting](#10-gaze-highlighting)
11. [Gaze Data Logging](#11-gaze-data-logging)
12. [The AI Voice Assistant](#12-the-ai-voice-assistant)
13. [Feature Toggling (Hand Menu)](#13-feature-toggling-hand-menu)
14. [File Reference](#14-file-reference)
15. [Data Flow Diagrams](#15-data-flow-diagrams)

---

## 1. How Unity Works (Quick Primer)

Unity is a game engine where everything in your scene is a **GameObject**. Each GameObject has **Components** attached to it that define its behavior. For example:

- A `MeshRenderer` component makes an object visible.
- A `Rigidbody` component gives it physics (gravity, collisions).
- A `MonoBehaviour` script (written in C#) adds custom logic.

### Key Lifecycle Methods

Every C# script that inherits from `MonoBehaviour` can use these methods, which Unity calls automatically:

| Method | When It Runs |
|--------|-------------|
| `Awake()` | Once, when the component is first created |
| `OnEnable()` | Each time the component is enabled |
| `Start()` | Once, on the first frame after `Awake()` |
| `Update()` | Every frame (~72 fps in VR) |
| `LateUpdate()` | Every frame, after all `Update()` calls finish |
| `OnDisable()` | Each time the component is disabled |
| `OnDestroy()` | When the GameObject is destroyed |

### Coroutines

Coroutines are Unity's way of doing asynchronous work (like waiting for an API response) without freezing the game. They use `yield return` to pause and resume across frames:

```csharp
IEnumerator DoSomethingAsync()
{
    Debug.Log("Starting...");
    yield return new WaitForSeconds(2f);  // wait 2 seconds
    Debug.Log("Done!");
}
```

You start a coroutine with `StartCoroutine(DoSomethingAsync())`.

### Events and Delegates

Scripts communicate through **events** — one script fires an event, and other scripts that subscribed to it get notified:

```csharp
// Publisher
public event System.Action<int> OnScoreChanged;
OnScoreChanged?.Invoke(newScore);  // fire the event

// Subscriber
scoreManager.OnScoreChanged += HandleScoreChanged;
void HandleScoreChanged(int score) { /* react */ }
```

---

## 2. Project Architecture Overview

The entire system is built around a single pre-existing component called **ObjectSpawner** (from Unity's XR Interaction Toolkit starter assets). All custom scripts auto-attach themselves to the ObjectSpawner's GameObject at scene load, forming a component stack:

```
ObjectSpawner (pre-existing, from XRI Starter Assets)
│
├── ShapeObjectFactory        — assigns shapes, colors, and physics
├── FindObjectGameManager     — runs the game logic
├── FindObjectUI              — shows the HUD
└── VoiceAssistantController  — AI voice assistant
    ├── AgentContext           — builds scene descriptions for the LLM
    ├── HintGenerator          — calls OpenAI for hints
    └── VoiceSynthesizer       — calls ElevenLabs for speech

Gaze Interactor (on the XR Gaze Interactor GameObject)
├── GazeHighlightManager      — orange glow on gazed objects
├── GazeDataLogger            — CSV telemetry logging
└── EyeGazeRayVisual          — orange ray visualization
```

### Why This Architecture?

- **Zero manual setup**: No dragging components in the Inspector. Everything auto-attaches at runtime using `[RuntimeInitializeOnLoadMethod]`.
- **Event-driven**: Components communicate through C# events, not direct references. This means you can remove the voice assistant and the game still works perfectly.
- **Graceful degradation**: If the OpenAI or ElevenLabs API fails, the game continues silently — no crashes, just log warnings.

---

## 3. Auto-Attach Pattern

Every major script uses this pattern to attach itself to the right GameObject at scene load:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void AutoAttach()
{
    var spawner = FindObjectOfType<ObjectSpawner>();
    if (spawner != null && spawner.GetComponent<MyScript>() == null)
    {
        spawner.gameObject.AddComponent<MyScript>();
        Debug.Log("[MyScript] Auto-attached");
    }
}
```

**How it works:**
1. Unity calls `AutoAttach()` automatically after the scene loads (the `[RuntimeInitializeOnLoadMethod]` attribute tells it to).
2. It searches the entire scene for an `ObjectSpawner` component.
3. If found, and if this script isn't already attached, it adds itself as a component.

This means you never need to manually add components in the Unity Inspector — just write the script and it wires itself up.

---

## 4. Plane Detection — How the Table Is Found

Before any game objects can be spawned, the system needs to detect real-world surfaces (tables, floors, walls). This project uses **VIVE's native plane detection** instead of Unity's AR Foundation, because it is faster and more reliable on the VIVE Focus Vision headset.

### VivePlaneProvider.cs

This script bridges VIVE's proprietary `PlaneDetectionManager` API with our game system.

**What it does:**
1. At startup, calls VIVE's `PlaneDetectionManager.GetPlaneDetections()` to discover all detected planes from the headset's Room Setup.
2. For each detected plane, creates a Unity GameObject with:
   - A visible mesh (semi-transparent surface)
   - A `MeshCollider` (so raycasts and touches can hit it)
   - A `VivePlaneData` component (stores metadata)

**Key timing detail:** VIVE plane detection relies on the headset's Room Setup data. It typically takes ~45 seconds after app launch for the XR subsystem to fully initialize and deliver plane data. During this time, you'll see "No planes detected yet" in the logs — this is normal.

### VivePlaneData.cs

A lightweight data component attached to each detected plane GameObject. It stores:

| Property | Description |
|----------|-------------|
| `Size` | Width and height of the plane (Vector2) |
| `Center` | World-space center position |
| `Normal` | Which direction the plane faces |
| `IsHorizontalUp` | Whether this is a floor/table (vs. a wall) |
| `SemanticType` | VIVE's classification (table, floor, wall, etc.) |

The game uses `IsHorizontalUp` to find tables for object placement.

---

## 5. Object Spawning — What Happens When You Tap

The spawn flow starts when you point your controller at a detected plane and tap:

```
User taps plane
    → ARContactSpawnTrigger fires UnityEvent
        → ObjectSpawner.TrySpawnObject(position, normal)
            → Instantiates a prefab at that position
            → Fires objectSpawned event
                → ShapeObjectFactory.OnObjectSpawned() — assigns shape/color
                → FindObjectGameManager.OnObjectSpawned() — game logic
```

### ObjectSpawner (Pre-existing)

This is part of Unity's XR Interaction Toolkit sample. It:
- Instantiates a random prefab from its list at the given position
- Fires `objectSpawned` (an `Action<GameObject>` event) so other scripts can post-process the spawned object

### ARContactSpawnTrigger (Modified)

Detects when the controller touches a plane surface. Originally worked only with AR Foundation planes; modified to also work with our `VivePlaneData` planes.

---

## 6. Shape and Color Assignment

### ShapeObjectFactory.cs

Every spawned object starts as a generic prefab (a cylinder from the starter assets). ShapeObjectFactory transforms it into one of 12 possible shape-color combinations.

**Available shapes:** Sphere, Cube, Pyramid
**Available colors:** Red, Blue, Yellow, Purple

**What happens in `OnObjectSpawned()`:**

1. **Hide child visuals**: The starter prefab has decorative child objects (rings, crosses). These are hidden (not destroyed, because destroying them breaks the XR affordance system).

2. **Pick shape and color**: Either from a pre-specified queue (for game mode) or randomly (for free play).

3. **Swap the mesh**: Replaces the `MeshFilter` with the chosen shape mesh (Sphere, Cube, or a procedurally generated Pyramid).

4. **Replace the collider**: Removes the old collider and adds a shape-appropriate one. Colliders are intentionally 30% oversized to make gaze targeting easier in VR.

5. **Apply material**: Creates a per-instance material with the chosen base color using the `InteractablePrimitive` shader (which supports edge highlights).

6. **Enable gaze interaction**: Sets `allowGazeInteraction = true` on the `XRGrabInteractable` so the eye gaze can hover over it. Re-registers colliders with the XR Interaction Manager.

7. **Add metadata**: Attaches a `SpawnableObjectInfo` component with `shapeName` and `colorName` fields. Renames the GameObject to something like "Red_Sphere".

8. **Fire event**: Invokes `objectFullyConfigured` so the game manager can wire up grab detection.

### The Combo Queue

For the game mode, ShapeObjectFactory has a queue system:

```csharp
m_Factory.EnqueueCombo("Red", "Sphere");  // next spawn will be a Red Sphere
m_Spawner.TrySpawnObject(position, normal);
```

When the queue is empty, shapes and colors are picked randomly.

### Procedural Pyramid Mesh

Since Unity doesn't have a built-in pyramid primitive, `CreatePyramidMesh()` generates one programmatically with:
- 4 triangular side faces
- A double-sided base (visible from both above and below)
- Explicit normals for flat shading
- UV coordinates for the edge highlight shader

---

## 7. The Find the Object Game Mode

### FindObjectGameManager.cs

This is the main game orchestrator. Here's how a complete game session works:

#### Starting a Game

1. **First tap triggers the game**: When the player taps any plane while the game is idle, the spawned object is treated as a "trigger" — its position is recorded and the object is immediately destroyed.

2. **Find the nearest table**: The system searches all `VivePlaneData` components for the closest horizontal-up plane (a table).

3. **Generate 9 objectives**: From the 12 possible shape-color combos (3 shapes x 4 colors), 9 are randomly selected using a Fisher-Yates shuffle.

4. **Calculate grid positions**: A 3x3 grid is computed on the table surface with:
   - 5cm margin from edges
   - ±2cm random jitter per position
   - 5cm above the surface (objects then fall with gravity)

5. **Spawn all 9 objects**: Using the combo queue system, all 9 shape-color combinations are spawned at the grid positions.

6. **Show first objective**: The HUD displays "Find: Red Sphere" (or whatever the first target is).

7. **Start timer**: A live timer begins counting up.

#### During Gameplay

The game listens for **grab events** on each spawned object via `XRGrabInteractable.selectEntered`:

- **Correct grab**: The object matches the current target.
  - Object is deactivated (disappears)
  - Counter increments
  - Next objective is shown
  - `OnObjectFound` event fires (voice assistant reacts)

- **Wrong grab**: The object doesn't match.
  - "Wrong object!" flashes on the HUD for 0.8 seconds
  - `OnWrongGrab` event fires (voice assistant may give a hint)

#### Game Completion

When all 9 objects are found:
- Timer stops
- Completion panel shows: "All 9 objects found! Time: 42.3s"
- `OnGameCompleted` event fires (voice assistant congratulates)
- After 5 seconds, everything resets and the game returns to idle

#### Game State Machine

```
IDLE ──(first tap)──> PLAYING ──(all found)──> COMPLETED ──(5s delay)──> IDLE
```

During `PLAYING`, additional taps are blocked (stray spawns are destroyed).
During `COMPLETED`, all spawns are blocked.

### Events for External Systems

The game manager exposes these events that the voice assistant (and any future systems) can subscribe to:

| Event | Arguments | When |
|-------|-----------|------|
| `OnGameStarted` | none | After all 9 objects spawn |
| `OnObjectFound` | `int objectiveIndex` | Correct grab |
| `OnWrongGrab` | `string grabbed, string wanted` | Wrong grab |
| `OnGameCompleted` | `float elapsedSeconds` | All 9 found |

### Public Read-Only State

Other scripts can read (but not modify) the game state:

| Property | Type | Description |
|----------|------|-------------|
| `CurrentState` | `GameState` (enum) | Idle, Playing, or Completed |
| `Objectives` | `IReadOnlyList<(shape, color, colorValue)>` | All 9 targets in order |
| `SpawnedObjects` | `IReadOnlyList<GameObject>` | All 9 spawned objects |
| `CurrentObjectiveIndex` | `int` | Which objective is current (0-8) |
| `FoundCount` | `int` | How many have been found |

---

## 8. Heads-Up Display (HUD)

### FindObjectUI.cs

The HUD is a world-space canvas that floats in front of the player's view. It is created entirely in code — no prefabs or Inspector setup needed.

#### Canvas Setup

```
World-Space Canvas (scale: 0.001, so 1 unit = 1mm)
├── Background Panel (400x240, black 70% opacity)
│   ├── ObjectiveText (top, 36pt, "Find: Red Sphere")
│   ├── ProgressText (middle, 28pt, "3 / 9 found")
│   └── TimerText (bottom, 26pt, yellow, "12.5s")
└── CompletionPanel (green, hidden until game ends)
    └── CompletionText (40pt, "All 9 found! Time: 42.3s")
```

#### LazyFollow

The canvas uses Unity's `LazyFollow` component to smoothly follow the player's head:

```csharp
m_LazyFollow.targetOffset = new Vector3(0f, 0.25f, 1.2f);
// 1.2m forward, 25cm above eye level
m_LazyFollow.applyTargetInLocalSpace = true;
```

This means the HUD is always 1.2 meters in front of you and slightly above your line of sight, so it doesn't block your view of the table.

#### Wrong Feedback Animation

When you grab the wrong object:
1. The objective text changes to red "Wrong object!"
2. After 0.8 seconds, it automatically restores to the original objective text
3. This is handled in `Update()` by checking `m_WrongFeedbackEndTime`

#### Live Timer

The timer updates every frame in `Update()`:

```csharp
float elapsed = Time.time - m_TimerStartTime;
m_TimerText.text = $"{seconds:F1}s";  // e.g., "12.5s"
```

When the game completes, `StopTimer()` returns the final elapsed time.

---

## 9. Eye Gaze Tracking

The VIVE Focus Vision headset has built-in eye tracking hardware. Unity's XR Interaction Toolkit provides the `XRGazeInteractor` to process eye gaze data.

### How Eye Gaze Works in This Project

1. **Hardware**: The headset's eye cameras track pupil position at high frequency.
2. **XR Subsystem**: VIVE's OpenXR runtime converts pupil data into a world-space ray (origin + direction).
3. **XRGazeInteractor**: Unity's component casts this ray into the scene and determines which `XRGrabInteractable` objects it hits.
4. **interactablesHovered**: The list of objects currently under the gaze ray. This is what our scripts poll.

### The Gaze Interactor

The gaze interactor is a GameObject in the scene with these components:
- `XRGazeInteractor` — processes eye tracking data
- `GazeHighlightManager` — applies orange highlights
- `GazeDataLogger` — records gaze data to CSV
- `EyeGazeRayVisual` — shows the orange ray

---

## 10. Gaze Highlighting

### GazeHighlightManager.cs

This script makes objects glow orange when you look at them with your eyes.

**How it works (every frame in `LateUpdate()`):**

1. Read `m_GazeInteractor.interactablesHovered` to get all objects under the gaze.
2. For each hovered object, find all its `Renderer` components.
3. Apply an orange edge highlight using a `MaterialPropertyBlock`:
   ```csharp
   m_PropertyBlock.SetColor("_EdgeHighlightColor", orange);
   m_PropertyBlock.SetFloat("_EdgeHighlightFalloff", 1.5f);
   renderer.SetPropertyBlock(m_PropertyBlock);
   ```
4. Track which renderers were highlighted last frame.
5. Clear the highlight on renderers that are no longer being gazed at.

**Why `LateUpdate()` and execution order 200?**

The XR Interaction Toolkit's controller highlight runs in its own `LateUpdate()`. By running at execution order 200 (higher = later), the gaze highlight overrides any controller blue highlight, ensuring the orange glow always wins when both controller and gaze are on the same object.

### InteractableHighlight.cs

A simpler per-object script that applies a light blue highlight when a **controller** (not gaze) hovers over the object. Runs at execution order 100 (before gaze), so gaze always takes priority.

### EyeGazeRayVisual.cs

Configures the visible ray coming from your eyes:
- Orange color (distinguishes from cyan controller rays)
- 0.02 unit width
- 10 meter max length
- Includes a fix for VIVE Focus Vision: the X position is flipped in `LateUpdate()` to correct a known left/right eye swap bug

---

## 11. Gaze Data Logging

### GazeDataLogger.cs

Records eye gaze data to a CSV file for research analysis.

**What is logged every frame:**

| Column | Example | Description |
|--------|---------|-------------|
| `timestamp` | 12.345 | Time.time in seconds |
| `frame` | 892 | Frame number |
| `pos_x/y/z` | 0.1234 | Gaze interactor world position |
| `rot_x/y/z` | 45.00 | Gaze direction (euler angles) |
| `hovered_object` | Red_Sphere | Name of gazed object (empty if none) |
| `hovered_shape` | Sphere | Shape of gazed object |
| `hovered_color` | Red | Color of gazed object |
| `ray_visible` | 1 | Whether the gaze ray visual is shown |

**File location:** `Application.persistentDataPath/gaze_log_2026-03-03_14-30-00.csv`
On the VIVE Focus Vision, this is: `/sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/`

**Performance:** The logger buffers writes and flushes to disk every 300 frames to avoid I/O stuttering.

**Hover tracking:** Uses `hoverEntered` and `hoverExited` events (not polling) to track which object the gaze is on. When the gaze moves to a new object, it reads the `SpawnableObjectInfo` component to get shape/color metadata.

---

## 12. The AI Voice Assistant

The voice assistant is a multi-component system that gives the player spoken hints during the game. It uses two external APIs:

- **OpenAI GPT-4o-mini** — generates contextual text hints
- **ElevenLabs** — converts text to natural-sounding speech

### Component Overview

```
VoiceAssistantController (orchestrator)
├── AgentContext         → "What's happening in the game right now?"
├── HintGenerator        → "Generate a helpful hint" (calls OpenAI)
└── VoiceSynthesizer     → "Say this out loud" (calls ElevenLabs)
```

### VoiceAssistantController.cs — The Orchestrator

This is the "brain" that decides what to say and when. It subscribes to the four game events:

**Game Started:**
```
"Let's play! Find the Red Sphere. Look around the table!"
```
This is a direct TTS call (no LLM) for instant response.

**Object Found:**
```
"Great job! Now find the Blue Cube."
```
Also direct TTS. Additionally:
- Interrupts any in-progress hint speech
- Cancels any pending hint generation
- Resets hint timers for the new objective

**Wrong Grab:**
Triggers the HintGenerator to produce a contextual hint after a 3-second delay.

**Game Completed:**
```
"Amazing! You found all the objects in 42 seconds! Great work!"
```
Direct TTS with the elapsed time.

**API Key Loading:**
Keys are stored in `Assets/StreamingAssets/api_keys.json` (gitignored for security):
```json
{
  "openai_key": "sk-proj-...",
  "elevenlabs_key": "sk_..."
}
```
The controller reads this file at startup using `JsonUtility.FromJson`.

### AgentContext.cs — Scene Knowledge Builder

This script builds a structured text description of the current game state for the LLM. It reads from multiple sources:

**Example output:**
```
CURRENT TARGET: Blue Cube
PROGRESS: 3/9 found (searching for #4)
TIME SEARCHING: 23 seconds
PLAYER IS LOOKING AT: Red Sphere
GAZE DIRECTION: looking left, looking down at the table

OBJECTS ON TABLE:
  - Red Sphere: 0.3m to your left and 0.2m ahead
  - Blue Cube: 0.5m to your right and 0.4m ahead [TARGET]
  - Yellow Pyramid: 0.1m to your left and 0.3m behind you
  ...
```

**Spatial descriptions** are computed by converting object world positions into player-local space using `Camera.main.transform.InverseTransformPoint()`. This produces natural language like "0.5m to your right and 0.4m ahead" instead of raw coordinates.

**Gaze information** is read from the same `XRBaseInputInteractor.interactablesHovered` that GazeHighlightManager uses. The context includes:
- What object the player is currently looking at
- Whether they are gazing at the target object
- General gaze direction (looking left, right, down at table, etc.)

**`IsGazingAtTarget()`** is used by the HintGenerator for the "gaze nudge" feature — if the player stares at the correct object for 10 seconds without grabbing it, the assistant encourages them to pick it up.

### HintGenerator.cs — When and What to Hint

This script has two responsibilities: deciding **when** to request a hint, and making the **OpenAI API call**.

#### Timing Logic (checked every frame in `Update()`)

| Trigger | Delay | Example Situation |
|---------|-------|-------------------|
| First hint | 15s after objective starts | Player has been searching for a while |
| Subsequent hints | Every 20s | Player is still stuck |
| After wrong grab | 3s | Player grabbed wrong object |
| Gaze nudge | 10s of looking at target | Player sees it but hasn't grabbed |
| Minimum gap | 8s between any hints | Prevents spamming |

The timing gets more aggressive over time — if the player has been struggling for over 60 seconds, hints become more specific and direct.

#### OpenAI API Call

The system prompt defines the assistant's persona:

> You are a friendly VR game assistant helping a player find colored shapes on a table. Give SHORT hints (1-2 sentences, under 30 words). Use spatial directions (left, right, ahead, behind). Be warm and encouraging. Don't give exact answers immediately — get more specific if the player has been struggling longer. Vary your phrasing. You have access to the player's eye gaze data — use it to guide them. If they're looking in the wrong direction, gently redirect. If they're close to the target, encourage them.

The user message includes the full scene context from AgentContext plus a situation description like "The player just grabbed the wrong object. Help redirect them to the correct one."

**API configuration:**
- Model: `gpt-4o-mini`
- Max tokens: 80
- Temperature: 0.8 (varied, creative responses)
- Timeout: 10 seconds

**Cancellation:** When the player finds an object, any in-flight API call is cancelled (`m_CancelRequested` flag checked after `yield return request.SendWebRequest()`).

### VoiceSynthesizer.cs — Text to Speech

Converts text to spoken audio using ElevenLabs API.

#### Audio Pipeline

```
Text → ElevenLabs API → MP3 bytes → Temp file → AudioClip → AudioSource
```

1. **API call**: POST to `https://api.elevenlabs.io/v1/text-to-speech/{voiceId}` with the text and model.
2. **Response**: Raw MP3 audio bytes.
3. **Save to temp file**: Writes MP3 to `Application.temporaryCachePath/tts_temp.mp3`.
4. **Decode**: Uses `UnityWebRequestMultimedia.GetAudioClip()` to decode the MP3 into a Unity `AudioClip`.
5. **Play**: Sets the clip on an `AudioSource` and plays it.
6. **Cleanup**: Deletes the temp file after playback.

**Voice configuration:**
- Voice: Rachel (`21m00Tcm4TlvDq8ikWAM`) — warm, clear female voice
- Model: `eleven_turbo_v2_5` (lowest latency)
- Audio: Non-spatialized (`spatialBlend = 0`) — the voice sounds like it's "in your head" rather than coming from a position in the room
- Volume: 0.7

#### Interruption System

The synthesizer tracks **what** the current speech is about using a context string (e.g., "hint", "welcome", "congrats"). When the player finds an object:

```csharp
m_VoiceSynthesizer.InterruptIfAbout("hint");
// Only stops if current speech is a hint — won't interrupt congratulations
```

`Stop()` immediately halts the AudioSource and cancels any in-progress coroutine.

---

## 13. Feature Toggling (Hand Menu)

### ARFeatureController.cs

Manages toggleable AR features from the hand menu:

| Feature | What It Does |
|---------|-------------|
| Planes | Show/hide detected plane meshes |
| Plane Visualization | Toggle mesh visibility (colliders stay) |
| Gaze Ray | Show/hide the orange eye gaze ray |
| Passthrough | Toggle camera passthrough |
| Bounding Boxes | Toggle scene understanding |

**Important detail about Gaze Ray toggle:** When you toggle the gaze ray off, only the visual ray disappears. The gaze **interactor** remains active, so:
- Eye gaze data continues to be logged
- Gaze highlights still work
- The voice assistant still knows what you're looking at

### GazeToggleConnector.cs

A simple bridge script that connects a UI Toggle in the hand menu to `ARFeatureController.ToggleGazeRay()`. Uses Unity's event system:

```csharp
toggle.onValueChanged.AddListener(isOn => featureController.ToggleGazeRay());
```

---

## 14. File Reference

### Core Game Logic
| File | Lines | Purpose |
|------|-------|---------|
| `FindObjectGameManager.cs` | ~376 | Game orchestration, state machine, objectives |
| `FindObjectUI.cs` | ~194 | World-space HUD with LazyFollow |
| `ShapeObjectFactory.cs` | ~290 | Shape/color assignment, mesh generation |
| `SpawnableObjectInfo.cs` | ~18 | Per-object metadata (shape, color) |

### Eye Gaze System
| File | Lines | Purpose |
|------|-------|---------|
| `GazeHighlightManager.cs` | ~95 | Orange highlight on gazed objects |
| `GazeDataLogger.cs` | ~100 | CSV logging of per-frame gaze data |
| `EyeGazeRayVisual.cs` | ~50 | Orange ray configuration |
| `InteractableHighlight.cs` | ~60 | Blue highlight for controller hover |

### AI Voice Assistant
| File | Lines | Purpose |
|------|-------|---------|
| `VoiceAssistantController.cs` | ~136 | Orchestrator, event handling |
| `AgentContext.cs` | ~165 | Scene state for LLM prompts |
| `HintGenerator.cs` | ~195 | OpenAI API + timing logic |
| `VoiceSynthesizer.cs` | ~173 | ElevenLabs TTS + audio playback |

### Plane Detection
| File | Lines | Purpose |
|------|-------|---------|
| `VivePlaneProvider.cs` | ~200 | VIVE native plane detection bridge |
| `VivePlaneData.cs` | ~50 | Plane metadata component |
| `ARFeatureController.cs` | ~150 | Feature toggling |

### Utilities
| File | Lines | Purpose |
|------|-------|---------|
| `GazeToggleConnector.cs` | ~20 | UI toggle wiring |
| `VivePassthrough.cs` | ~30 | Passthrough initialization |
| `PlaneDebug.cs` | ~25 | Diagnostic logging |
| `ShapePrefabGenerator.cs` | ~50 | Editor tool for prefab creation |

---

## 15. Data Flow Diagrams

### Spawn and Game Flow

```
Player taps table
    │
    ▼
ARContactSpawnTrigger
    │ UnityEvent
    ▼
ObjectSpawner.TrySpawnObject(pos, normal)
    │ objectSpawned event
    ├──────────────────────────────────┐
    ▼                                  ▼
ShapeObjectFactory                FindObjectGameManager
    │                                  │
    ├─ Pick shape (Sphere/Cube/Pyramid)│
    ├─ Pick color (Red/Blue/Yellow/Purple)
    ├─ Swap mesh, collider, material   │
    ├─ Enable gaze interaction         │
    ├─ Add SpawnableObjectInfo         │
    │                                  │
    │ objectFullyConfigured event      │
    └──────────────────────────────────┘
                    │
                    ▼
         Wire grab detection
         (XRGrabInteractable.selectEntered)
                    │
                    ▼
            Player grabs object
                    │
            ┌───────┴───────┐
            ▼               ▼
        Correct          Wrong
            │               │
    Deactivate obj    Flash "Wrong!"
    Advance objective  OnWrongGrab event
    OnObjectFound event     │
            │               ▼
            │      HintGenerator
            │      (3s delay → OpenAI → TTS)
            ▼
    All 9 found?
    ├─ No → Show next objective
    └─ Yes → OnGameCompleted
              Show completion screen
              5s → Reset to Idle
```

### Voice Assistant Flow

```
Game Event
    │
    ├─ OnGameStarted ────→ Direct TTS: "Let's play! Find the..."
    │
    ├─ OnObjectFound ────→ Interrupt hints
    │                      Direct TTS: "Great job! Now find..."
    │                      Reset hint timers
    │
    ├─ OnWrongGrab ──────→ HintGenerator.OnWrongGrab()
    │                      (3s delay)
    │                          │
    │                          ▼
    │                      AgentContext.BuildContextPrompt()
    │                          │ (scene state text)
    │                          ▼
    │                      OpenAI GPT-4o-mini
    │                          │ (hint text)
    │                          ▼
    │                      VoiceSynthesizer.Speak(hint)
    │                          │
    │                          ▼
    │                      ElevenLabs TTS API
    │                          │ (MP3 audio)
    │                          ▼
    │                      AudioSource.Play()
    │
    └─ OnGameCompleted ──→ Cancel all hints
                           Direct TTS: "Amazing! You found all..."
```

### Eye Gaze Data Flow

```
VIVE Eye Tracking Hardware
    │
    ▼
OpenXR Runtime (pupil → world ray)
    │
    ▼
XRGazeInteractor
    │ interactablesHovered (list of hit objects)
    │
    ├─────────────────────┐─────────────────────┐
    ▼                     ▼                     ▼
GazeHighlightManager  GazeDataLogger       AgentContext
    │                     │                     │
    │ Orange glow via     │ CSV per-frame       │ "Player is looking at
    │ MaterialPropertyBlock│ logging             │  Red Sphere, 0.3m left"
    │                     │                     │
    ▼                     ▼                     ▼
Visual feedback      Research data        LLM context for hints
```
