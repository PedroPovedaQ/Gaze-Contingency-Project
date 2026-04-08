# Gaze Agent and Telemetry Guide

This document explains how the gaze-aware assistance layer works in the current project:

- how eye gaze flows from hardware to hover detection and logging
- how highlight and dwell behavior work
- how hint generation differs between gaze-aware and gaze-unaware rounds
- what gets logged, where it goes, and how to debug incorrect hints

The code paths described here are the ones currently used by the Unity project. File names are included so you can jump straight to the implementation.

## 1. High-level flow

The runtime loop is:

1. The headset provides eye gaze pose data.
2. The `XRGazeInteractor` uses that pose to raycast into the scene.
3. Hover events update highlight state and dwell progress.
4. The hint generator decides whether to speak an `off-target` or `on-track/very-close` gaze-aware line, or a generic gaze-unaware encouragement line.
5. The telemetry logger records gaze pose, hover state, blink state, target metadata, and game state every frame.

The important thing to keep in mind is that the line visual is only a visual aid. The underlying gaze interactor and telemetry pipeline can still run even if the ray is hidden.

## 2. Eye gaze pipeline

The gaze pipeline is built on the standard Unity/XRI gaze stack plus project-specific logging and hint logic.

Relevant pieces:

- `XRGazeInteractor` drives hover detection from gaze pose.
- `EyeGazeRayVisual` renders the orange line when enabled.
- `GazeHighlightManager` listens for hover changes and drives dwell/capture feedback.
- `GazeDataLogger` records gaze and hover telemetry.
- `HintGenerator` converts gaze state into spoken feedback.

The project notes in `agents.md` are important here: the HTC Vive Focus Vision gaze tracking is available through OpenXR, but the visual line is not the same thing as the sensor data. Hiding the line does not disable tracking.

### Why the ray can be off while data is still collected

The visual line is controlled separately from the gaze interactor.

```csharp
// ARFeatureController.cs
public void ToggleGazeRay(bool enabled)
{
    if (m_GazeInteractor == null) return;

    var lineRenderer = m_GazeInteractor.GetComponent<LineRenderer>();
    if (lineRenderer != null) lineRenderer.enabled = enabled;

    var lineVisual = m_GazeInteractor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
    if (lineVisual != null) lineVisual.enabled = enabled;
}
```

That toggle only changes the renderable ray. The gaze interactor, hover callbacks, and telemetry still work because they are driven by the underlying tracked pose and interaction components, not by the line renderer itself.

That separation is intentional:

- you can run the study with a clean visual UI
- you still collect eye gaze data for analysis
- the agent can still generate hints from gaze behavior

## 3. Highlight and dwell behavior

`GazeHighlightManager` is the script that turns hover into visible feedback and capture logic.

Core responsibilities:

- track the currently hovered spawned object
- compute dwell progress toward capture
- apply an edge highlight while gaze stays on a candidate
- trigger a capture flash when dwell completes
- reset state between rounds

```csharp
// GazeHighlightManager.cs
private const float k_DwellDuration = 1.6f;
private const float k_CaptureFlashDuration = 0.25f;

public float DwellProgress => Mathf.Clamp01(m_DwellTime / k_DwellDuration);
```

The dwell path is effectively:

1. A gaze hover is detected on a spawned object.
2. The manager starts accumulating dwell time.
3. While the target remains hovered, the highlight strengthens.
4. If the target stays under gaze for long enough, the object is captured.

The logic runs in `LateUpdate`, which matters because it lets the highlight state override other visual affordances that may also be writing to the same material properties.

```csharp
// GazeHighlightManager.cs
private void LateUpdate()
{
    // resolve hovered object
    // accumulate dwell
    // update visual feedback
    // capture on threshold
}
```

### Practical note

If dwell feels wrong in a build, check two things first:

- whether the hovered object has the expected gaze-interactable setup
- whether the user is actually hovering the target object, not just looking near it

The hint system can tolerate near-misses better than the dwell capture path.

## 4. Hint generation

`HintGenerator` is where the project decides what the voice assistant says during each round.

The project currently uses two broad modes:

- gaze-aware rounds: two-level proximity feedback (`off-target` vs `on-track/very-close`)
- gaze-unaware rounds: neutral encouragement and pacing support

The round schedule alternates between aware and unaware conditions, so the analysis can compare the two experiences directly.

### Gaze-aware mode

In gaze-aware rounds, the system answers:

> Is the user looking near enough to the target area, and are they on the correct bookcase side?

If yes, the assistant uses on-track/very-close phrasing. If no, it uses off-target phrasing.

Important implementation detail:

- opposite-bookcase gaze is forced to `off-target`, even if other signals are noisy

```csharp
// HintGenerator.cs
const float k_VeryCloseDistanceMeters = 0.16f;
const float k_NearTargetDistanceMeters = 0.30f;
const float k_NearTargetAngleDeg = 14f;
const float k_NearTargetRayDistanceMeters = 0.18f;
const float k_HotMemoryWindow = 5.5f;
```

Core classifier shape:

```csharp
if (hasLooked && IsWrongBookcase(targetInfo, lookedInfo))
    return Pick(k_GA_Cold);

bool veryCloseNow = IsVeryCloseEvidence(targetInfo, lookedInfo, hasLooked);
bool nearNow = HasNearEvidence(targetInfo, lookedInfo, hasLooked);
if (veryCloseNow || nearNow)
    return Pick(veryCloseNow ? k_GA_Hot_VeryClose : k_GA_Hot_Track);
```

The aware side now uses personable phrases such as:

- off-target: `"You're way off right now."`, `"Look in a different area."`
- on-track: `"You're on the right track."`
- very-close: `"You're very close."`, `"Stay with this area."`

### Gaze-unaware mode

In gaze-unaware rounds, the assistant should not give away gaze-sensitive information. It stays supportive, calm, and non-diagnostic.

The current implementation uses a short recent-history window to avoid repeating the same encouragement line over and over.

```csharp
private const int k_UnawareNoRepeatWindow = 3;
private const float k_TipIntervalUnaware = 11f;
```

## 5. Proximity logic in detail

This is the part that most often causes confusion when debugging.

### What `on-track` means

`on-track` means the user appears to be moving toward the right target area but is not in the tightest proximity bucket.

### What `very-close` means

`very-close` means the user is tightly aligned with the target area and the assistant can use stronger keep-going language.

### What `off-target` means

`off-target` means the current evidence does not support near-target guidance.
If the system resolves gaze to the opposite bookcase column, this is forced off-target.

### Mitigations in the current code

- hovered-object evidence is preferred before physics ray fallback
- nearest-to-ray fallback exists for near-miss collider cases
- recent near-target evidence is remembered briefly (`k_HotMemoryWindow`) to reduce jittery flips
- opposite-bookcase mismatch forces off-target response

## 6. Logging outputs

`GazeDataLogger` writes a per-frame CSV file with gaze and game context. This is the main source of telemetry for later analysis.

```csharp
// GazeDataLogger.cs
if (!m_EyeDeviceSearched)
{
    var devices = new List<InputDevice>();
    InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, devices);
    if (devices.Count > 0)
        m_EyeDevice = devices[0];
    m_EyeDeviceSearched = true;
}

if (m_EyeDevice.isValid && m_EyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
{
    // log gaze pose, fixation, blink/open-state, and hover context
}
```

### What gets logged

The current CSV includes, among other fields:

- timestamp and frame index
- gaze origin and direction
- gaze rotation
- left and right eye data
- fixation point
- hovered object name, shape, color, and shelf level
- whether the hovered object matches the active target
- dwell progress
- current objective shape, color, and round index
- game state
- whether the gaze ray visual is visible
- blink state and blink count

### Where it goes

The file is written to `Application.persistentDataPath`, which on-device resolves under the app’s Android storage path. The output is a timestamped CSV file.

### Why this matters

This logger lets you separate three different questions:

1. what the participant’s eyes were doing
2. what object the interaction system thought was hovered
3. what hint the assistant chose to speak

That separation is useful when debugging incorrect feedback, because the visual ray can be hidden while the gaze data is still present in the log.

## 7. A few implementation details worth remembering

### The gaze ray visual is not the sensor

If you disable the line visual, you only hide the line. You do not disable:

- eye tracking
- hover callbacks
- dwell tracking
- hint generation
- telemetry logging

That is why the study can run with the ray turned off for UX reasons but still keep the data stream alive.

### Transition periods are intentionally quieter

During transitions, the game hides the current goal and uses a blank pause before spawning the next set of objects. This applies between rounds and at the start of round 1. That avoids priming the participant with timing cues.

### Aware and unaware rounds alternate

The condition schedule alternates by round. If you see the same condition repeating unexpectedly, check the round index and condition assignment first before assuming the gaze classifier is broken.

## 8. Debugging incorrect hints

Use this checklist when the assistant says the wrong thing.

- Confirm the current round condition is what you expect: gaze-aware or gaze-unaware.
- Check whether the gaze ray visual is merely hidden, not disabled at the tracking level.
- Inspect the CSV log to see what object was actually hovered.
- Verify whether the hovered object matches the active target shape, color, and shelf slot.
- Check whether the user was near the target but not exactly on it; proximity fallbacks may matter.
- Look for recent wrong-capture events, which can bias the next response toward off-target language.
- Check whether the hint came from aware mode or unaware mode; the phrases should be completely different.
- Make sure the target object and the hovered object both have the expected spawn metadata.
- Verify that dwell state was reset at the start of the round.
- If hints repeat too often in unaware mode, check the recent-phrase queue and the timer interval.

## 9. Code pointers

These are the main files to inspect together when debugging the gaze agent:

- [`Assets/HintGenerator.cs`](../../Assets/HintGenerator.cs)
- [`Assets/GazeHighlightManager.cs`](../../Assets/GazeHighlightManager.cs)
- [`Assets/GazeDataLogger.cs`](../../Assets/GazeDataLogger.cs)
- [`Assets/MRTemplateAssets/Scripts/ARFeatureController.cs`](../../Assets/MRTemplateAssets/Scripts/ARFeatureController.cs)

## 10. Short summary

If you only remember one thing:

- `ARFeatureController` controls the visual ray
- `GazeHighlightManager` handles hover, dwell, and capture feedback
- `HintGenerator` turns gaze state into aware or unaware speech
- `GazeDataLogger` records the data needed to audit what happened

That separation is what makes the system debuggable. When hints are wrong, always ask whether the issue is in the gaze signal, the hover/capture layer, or the language choice layer.
