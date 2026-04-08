# System Overview

This project is a Unity 6 mixed reality study for gaze-contingent AI assistance on the HTC Vive Focus Vision.
If you are new to the codebase, the important thing to understand is that the app is not a generic Unity demo.
It is a tightly coordinated pipeline:

- the scene boots a single search task,
- the task spawns deterministic object layouts,
- gaze telemetry and dwell selection run continuously,
- a voice assistant delivers condition-dependent hints,
- trial logging writes structured data for later analysis.

## The Big Picture

At runtime, the project behaves like a small research instrument.
The main gameplay loop is:

1. Detect a table surface and build the shelf layout.
2. Show fixation cross + announce the round goal, then spawn objects.
3. Track eye gaze over the spawned objects.
4. Provide hints based on the current condition.
5. Capture the target with gaze dwell.
6. Advance to the next round and log everything.

The code is organized around that flow, not around a traditional player-controller architecture.

## Scene Assumptions

The project assumes:

- only `Assets/Scenes/SampleScene.unity` is in the build
- the headset is HTC Vive Focus Vision
- the app uses MR passthrough
- the task starts from an `ObjectSpawner`-driven interaction
- gaze tracking is the primary input, with visual ray feedback optional

The key point is that many systems auto-discover each other at runtime.
You should not expect a large amount of manual Inspector wiring.

## Main Runtime Roles

### `FindObjectGameManager`

This is the core state machine for the experiment.
It owns:

- round progression
- target selection
- spawn point generation
- round transitions
- fixation cross display
- UI updates

Short example:

```csharp
public GameState CurrentState => m_State;
public IReadOnlyList<GameObject> SpawnedObjects => m_SpawnedObjects;
public bool CurrentRoundGazeAware { get; private set; }
public string CurrentRoundConditionLabel { get; private set; } = "";
```

It also auto-attaches itself to the `ObjectSpawner` root and disables template UI systems that would interfere with the study.

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void AutoAttach()
{
    var spawner = FindObjectOfType<ObjectSpawner>();
    if (spawner != null && spawner.GetComponent<FindObjectGameManager>() == null)
    {
        spawner.gameObject.AddComponent<FindObjectGameManager>();
    }
    DisableTemplateSystems();
}
```

### `ShapeObjectFactory`

This script post-processes spawned prefabs into the actual experimental stimuli.
It assigns:

- one of the configured shapes
- one of the configured colors
- metadata for logging
- gaze interaction support

It also creates meshes procedurally where needed.

```csharp
public void ConfigureObject(GameObject obj) => OnObjectSpawned(obj);
```

```csharp
var info = obj.AddComponent<SpawnableObjectInfo>();
info.shapeName = shapeName;
info.colorName = colorName;
obj.name = info.DisplayName;
```

### `ShelfSpawner`

This script computes the virtual shelf layout on top of the detected table.
It is responsible for:

- turning the detected plane into a shelf frame
- building two bookcase columns
- computing row/column spawn points
- caching the layout so later rounds reuse the same geometry

The important point for research is determinism: once the first layout is computed, later rounds reuse the same positions.

```csharp
public static Quaternion ObjectFacingRotation { get; private set; } = Quaternion.identity;
public static Vector3 ShelfRight { get; private set; } = Vector3.right;
public static Vector3 ShelfFacing { get; private set; } = Vector3.forward;
```

```csharp
public static List<SpawnPoint> ComputeSpawnPoints(
    Vector3 tableCenter, Vector2 tableSize,
    Vector3 planeRight, Vector3 planeForward,
    int totalObjects)
```

### `SpawnableObjectInfo`

This is the metadata component attached to every spawned object.
It is the simplest way to know what an object is during gaze logging and hint generation.

```csharp
public string shapeName;
public string colorName;
public int shelfLevel;
public int shelfColumn;
```

```csharp
public string DisplayName => $"{colorName}_{shapeName}";
```

### `HintGenerator`

This script controls spoken hints.
It uses the current round state and gaze data to decide what to say.
There are two modes:

- gaze-aware: proximity feedback (`off-target` vs `on-track/very-close`)
- gaze-unaware: empathetic generic encouragement

The current round mode is synchronized from the game manager every frame.

```csharp
bool resolvedMode = m_GameManager.CurrentRoundGazeAware;
gazeAwareTips = resolvedMode;
```

Gaze-aware examples are supportive but direct:

```csharp
static readonly string[] k_GA_Hot_Track =
{
    "You're on the right track.",
    "You're getting closer. Keep searching this direction.",
};
```

```csharp
static readonly string[] k_GA_Cold =
{
    "You're way off right now.",
    "Look in a different area.",
};
```

The aware classifier is deliberately conservative and checks:

- current hovered object
- target object position
- world-space distance
- gaze angle near the target
- wrong-bookcase mismatch (forced off-target)

```csharp
if (hasLooked && IsWrongBookcase(targetInfo, lookedInfo))
{
    return Pick(k_GA_Cold);
}
```

### `VoiceAssistantController`

This is the orchestrator for voice and timing.
It wires together:

- `GazeCoverageTracker`
- `AgentContext`
- `VoiceSynthesizer`
- `HintGenerator`

It also listens to round events and starts or stops speech at the correct moments.

```csharp
m_GameManager.OnGameStarted += HandleGameStarted;
m_GameManager.OnObjectFound += HandleObjectFound;
m_GameManager.OnWrongCapture += HandleWrongCapture;
m_GameManager.OnGameCompleted += HandleGameCompleted;
m_GameManager.OnRoundTransitionStarted += HandleRoundTransitionStarted;
m_GameManager.OnRoundReady += HandleRoundReady;
```

The important behavioral detail is that goal announcement starts when the fixation cross appears, including round 1.

### `VoiceSynthesizer`

This handles TTS playback and caching.
It uses ElevenLabs, stores MP3 files locally, and stops current speech before starting new speech.

```csharp
public void Speak(string text, string context = null)
{
    Stop();
    m_CurrentContext = context;
    m_SpeakCoroutine = StartCoroutine(SpeakCoroutine(text));
}
```

```csharp
public void Stop()
{
    if (m_SpeakCoroutine != null)
    {
        StopCoroutine(m_SpeakCoroutine);
        m_SpeakCoroutine = null;
    }
    if (m_AudioSource != null && m_AudioSource.isPlaying)
        m_AudioSource.Stop();
}
```

### `GazeDataLogger`

This is the per-frame telemetry logger.
It records gaze pose, hover data, objective metadata, and blink detection.

```csharp
m_Writer.WriteLine(string.Join(",",
    "timestamp", "frame",
    "gaze_origin_x", "gaze_origin_y", "gaze_origin_z",
    "gaze_dir_x", "gaze_dir_y", "gaze_dir_z"));
```

The logger is designed to keep running even when the gaze ray visual is hidden.
That means the experiment can separate visual feedback from actual tracking.

### `TrialDataLogger`

This is the higher-level event logger.
It writes round events and summary JSON for the analysis pipeline.

```csharp
string firstCondition = ChallengeSet.GetConditionLabel(0, 1);
string secondCondition = ChallengeSet.GetConditionLabel(1, 1);
string runConditionLabel = $"alternating_{firstCondition}_then_{secondCondition}";
```

It also records the post-run NASA-TLX prompt in the event stream.

### `FindObjectUI`

This script owns the world-space HUD.
It displays:

- objective text
- round progress
- timer
- agent state debug
- fixation cross
- completion message

It also positions the goal panel on the table and places the transition cross in a separate canvas.

```csharp
m_CanvasGO.transform.rotation = Quaternion.LookRotation(Vector3.down, -facing);
```

```csharp
public void ShowFixationCross(string color = null, string shape = null)
```

## Auto-Attach Pattern

Several systems are designed to bootstrap themselves at runtime.
This reduces manual setup but also means you must understand startup order.

Examples:

- `FindObjectGameManager` attaches to `ObjectSpawner`
- `ShapeObjectFactory` attaches to `ObjectSpawner`
- `TrialDataLogger` attaches to `ObjectSpawner`
- `VoiceAssistantController` attaches to `ObjectSpawner`

Typical pattern:

```csharp
var spawner = FindObjectOfType<ObjectSpawner>();
if (spawner != null && spawner.GetComponent<ShapeObjectFactory>() == null)
{
    spawner.gameObject.AddComponent<ShapeObjectFactory>();
}
```

The benefit is less scene setup.
The tradeoff is that runtime failures can be less obvious if a required component is missing.

## Object Lifecycle

The object lifecycle is important if you are debugging gaze or spawn bugs.

1. `ObjectSpawner` creates an initial trigger object.
2. `FindObjectGameManager` intercepts that spawn and starts the game.
3. `ShapeObjectFactory` swaps the prefab into the experimental stimulus.
4. `ShelfSpawner` gives it a deterministic row and column.
5. The object gets `SpawnableObjectInfo` metadata.
6. Gaze hover and dwell systems operate on the object.
7. On correct capture, the target is removed and the next round begins.

Relevant snippet:

```csharp
obj.transform.position = sp.position;
obj.transform.rotation = ShelfSpawner.ObjectFacingRotation;

var info = obj.GetComponent<SpawnableObjectInfo>();
if (info != null)
{
    info.shelfLevel = sp.row;
    info.shelfColumn = sp.col;
}
```

## MR Template Integration

The project started from the Unity MR template, but several template systems are disabled or redirected.

Key integrations:

- plane detection falls back to VIVE-native APIs
- template coaching/tutorial UI is hidden
- gaze ray visibility is optional, but gaze tracking remains active
- passthrough is initialized through VIVE OpenXR

Example of the template suppression logic:

```csharp
foreach (var go in Object.FindObjectsOfType<GameObject>())
{
    if (go.name == "Text Poke Button Continue")
        go.SetActive(false);
}
```

This means you should be careful when modifying template prefabs.
Some things are intentionally disabled at startup.

## Build And Run

The project includes an editor helper for Android build and device run:

```csharp
[MenuItem("Tools/Codex/Build And Run Android", false, 3000)]
public static void BuildAndRunAndroidMenu()
{
    BuildAndRunAndroid();
}
```

The supporting shell script refocuses Unity and triggers that menu command.
This is intended to save time during iterative testing.

## Debugging Checklist

If something looks wrong, check these first:

- Did the object get `SpawnableObjectInfo`?
- Is the object on layer 8?
- Is gaze interaction enabled on `XRGrabInteractable`?
- Is the current round condition set correctly?
- Is the gaze ray visual disabled while gaze data still logging?
- Did the shelf layout cache reset after a full restart?

Useful snippet:

```csharp
grab.allowGazeInteraction = true;
grab.allowGazeSelect = false;
grab.allowGazeAssistance = false;
```

## What to Read Next

- [02-gameplay-round-flow.md](02-gameplay-round-flow.md)
- [03-gaze-agent-and-telemetry.md](03-gaze-agent-and-telemetry.md)
- [04-build-deploy-and-debug-workflow.md](04-build-deploy-and-debug-workflow.md)
