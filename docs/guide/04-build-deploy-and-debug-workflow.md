# Build, Deploy, and Debug Workflow

This guide covers the practical workflow for editing the Unity project safely, validating compile errors, building to the Vive Focus Vision, and collecting logs/data.

It is written for day-to-day use, not as an architecture overview.

## What to do first

When making changes:

1. Edit the relevant script or prefab.
2. Run the compile check before trying a device build.
3. If Unity is already open, let it recompile fully.
4. Use the build-and-run workflow to push to device.
5. Pull logs and CSVs only after the run finishes.

## Local safety checks

### Compile check

Use the repo compile guard before building:

```bash
./scripts/unity-compile-check.sh
```

What it does:

- Runs a headless Unity compile check when the Editor is closed.
- Falls back to parsing the live Unity `Editor.log` when the Editor is already open on this project.
- Fails on `CS` compiler errors, `scripts have compile errors`, or `Compilation failed`.

Relevant implementation:

```bash
if [[ -f "$ROOT_DIR/Temp/UnityLockfile" ]]; then
  echo "Unity lockfile detected. Checking live Editor.log instead of batchmode..."
  if print_recent_editor_errors "$EDITOR_LOG_MAC"; then
    echo "Unity compile check passed (Editor.log)."
    exit 0
  fi
fi
```

### Git hook

Install the pre-commit hook once:

```bash
./scripts/install-git-hooks.sh
```

The hook runs the compile check automatically on staged `.cs`, `.asmdef`, `.asmref`, `.unity`, and `.prefab` changes.

If you need an emergency bypass:

```bash
SKIP_UNITY_LINT=1 git commit ...
```

## Build and deploy

### Manual build workflow

The project now has a Codex build command inside Unity:

- `Tools > Codex > Build And Run Android`

The command is implemented in:

```csharp
[MenuItem("Tools/Codex/Build And Run Android", false, 3000)]
public static void BuildAndRunAndroidMenu()
{
    BuildAndRunAndroid();
}
```

The build uses the enabled scenes from Build Settings and launches the player on device:

```csharp
var options = new BuildPlayerOptions
{
    scenes = enabledScenes,
    target = BuildTarget.Android,
    locationPathName = apkPath,
    options = BuildOptions.AutoRunPlayer
};
```

### One-command refocus + deploy

Run:

```bash
./scripts/refocus-unity-and-build-device.sh
```

This script:

- Refocuses Unity.
- Clicks `Tools > Codex > Build And Run Android`.
- Starts the Android build and deploy flow on the connected device.

The script is the preferred end-of-edit action in this repo.

## Where the data goes

The session data is organized by `SessionConfig` into:

```text
{Application.persistentDataPath}/GazeData/
  P001/
    run_001_gaze_unaware_YYYY-MM-DD_HH-mm-ss/
      gaze_log.csv
      trial_events.csv
      trial_summary.json
```

Relevant code:

```csharp
public static string RootPath => Path.Combine(Application.persistentDataPath, k_RootFolder);
```

```csharp
string folderName = $"run_{RunNumber:D3}_{ConditionLabel}_{timestamp}";
CurrentRunFolder = Path.Combine(participantDir, folderName);
Directory.CreateDirectory(CurrentRunFolder);
```

### Files to expect

- `gaze_log.csv`: per-frame eye tracking and hover state.
- `trial_events.csv`: event-level round/capture/transition logging.
- `trial_summary.json`: per-run summary metrics.

### On device

On Android, the files live under the app's external files directory, usually:

```text
/sdcard/Android/data/<package-name>/files/GazeData/
```

For this project, the package has been `com.DefaultCompany.MixedRealityTemplate` in recent builds, so the path may look like:

```text
/sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeData/
```

If the package name changes, update the path accordingly.

## Pulling logs

### Pull a whole run folder

```bash
adb pull /sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeData/P001/run_001_gaze_unaware_2026-04-07_19-10-01 ./run_001
```

### Pull all gaze CSVs

```bash
adb pull /sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeData ./GazeData
```

### Pull only the latest gaze log

```bash
adb pull /sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeData/P001/run_001_gaze_unaware_2026-04-07_19-10-01/gaze_log.csv
```

## How the round flow works

The gameplay loop is driven by `FindObjectGameManager`.

### Round start

When the game starts, round 1 now uses the same transition ritual as later rounds (fixation cross + goal announcement + blank pause) before objects spawn:

```csharp
CurrentRoundGazeAware = ChallengeSet.IsGazeAware(m_CurrentRound, participantNumber);
CurrentRoundConditionLabel = ChallengeSet.GetConditionLabel(m_CurrentRound, participantNumber);
m_State = GameState.Transitioning;
StartCoroutine(BeginFirstRoundTransition());
```

The UI and hint system are updated immediately:

```csharp
if (hints != null) hints.gazeAwareTips = gazeAware;
m_UI.SetAgentState(gazeAware, CurrentRoundConditionLabel);
```

### Spawn and finalize

The object set for the round is determined by `ChallengeSet`, then instantiated by `ShapeObjectFactory`.

Important details:

- Every round is deterministic.
- The same target/distractor layout is recreated from seed `42`.
- `ShapeObjectFactory` assigns shape, color, collider, metadata, and gaze interaction.

Example:

```csharp
grab.allowGazeInteraction = true;
grab.allowGazeSelect = false;
grab.allowGazeAssistance = false;
```

### Capture

`GazeHighlightManager` handles dwell-based capture. When dwell completes:

```csharp
if (m_DwellTime >= k_DwellDuration)
    CaptureObject(hoveredObj);
```

The round advances after the correct object is captured.

### Transition

On round completion:

1. Active objects are destroyed.
2. The objective HUD is hidden.
3. The fixation cross is shown.
4. The next goal text appears in the top-left of the cross box.
5. A blank pause of `0.2` to `1.3` seconds happens.
6. The next round is spawned.

Relevant code:

```csharp
m_UI.HideObjectiveDuringTransition();
m_UI.ShowFixationCross(nextTarget.color, nextTarget.shape);
yield return new WaitForSeconds(k_TransitionPause);
m_UI.HideFixationCross();
float blankPause = Random.Range(k_BlankPauseMin, k_BlankPauseMax);
yield return new WaitForSeconds(blankPause);
m_State = GameState.Playing;
yield return DoSpawnRound();
```

## Gaze and hints

### Why the ray can be hidden but gaze still works

The visible gaze line is only a rendering layer. The actual gaze interactor and eye tracking remain active.

Relevant behavior:

```csharp
var lineRenderer = m_GazeInteractor.GetComponent<LineRenderer>();
if (lineRenderer != null) lineRenderer.enabled = enabled;

var lineVisual = m_GazeInteractor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
if (lineVisual != null) lineVisual.enabled = enabled;
```

So:

- ray visual off = invisible cursor line
- gaze tracking still on = hover, dwell, logging, and hints still work

### Hint conditions

The project currently alternates conditions by round:

- round 1: gaze-unaware
- round 2: gaze-aware
- round 3: gaze-unaware
- round 4: gaze-aware

This is defined in `ChallengeSet`:

```csharp
return (roundIndex % 2) == 1;
```

### Gaze-aware hints

The aware mode is proximity-based only.

Core idea:

- `on-track` / `very-close` when gaze evidence is near target
- `off-target` when evidence is not near target
- no generic coaching phrases

Example logic:

```csharp
if (hasLooked && IsWrongBookcase(targetInfo, lookedInfo))
    return Pick(k_GA_Cold);

bool veryCloseNow = IsVeryCloseEvidence(targetInfo, lookedInfo, hasLooked);
bool nearNow = HasNearEvidence(targetInfo, lookedInfo, hasLooked);
return Pick((veryCloseNow || nearNow) ? (veryCloseNow ? k_GA_Hot_VeryClose : k_GA_Hot_Track) : k_GA_Cold);
```

### Gaze-unaware hints

The control condition is generic and empathetic, but not gaze-reactive:

```csharp
static readonly string[] k_GU_General =
{
    "You're doing okay. Keep scanning.",
    "Take your time. Check shape and color.",
    "No rush. Scan one object at a time.",
};
```

The unaware mode also uses a no-repeat window so back-to-back encouragement does not feel robotic.

## Debugging incorrect hints

If gaze-aware hints feel wrong, check these in order:

1. Confirm the current round condition in the UI debug panel.
2. Check whether `FindObjectGameManager.CurrentTarget` matches what is shown in UI.
3. Verify the gaze ray is hitting the intended shelf object, not a nearby distractor.
4. Confirm the object has `SpawnableObjectInfo` with correct `shapeName`, `colorName`, `shelfLevel`, and `shelfColumn`.
5. Check whether the target is physically near but a collider on another object is intercepting the ray.
6. Inspect `gaze_log.csv` and `trial_events.csv` for the hovered object and objective index.
7. If the problem only happens when the gaze line is hidden, remember the line visual does not control gaze collection.

Useful code paths:

```csharp
if (hasLooked && IsWrongBookcase(targetInfo, lookedInfo))
    return Pick(k_GA_Cold);
```

```csharp
if (TryGetGazedSpawnInfo(out var lookedInfo) && lookedInfo != null)
{
    int rowDist = Mathf.Abs(targetInfo.shelfLevel - lookedInfo.shelfLevel);
    int colDist = Mathf.Abs(targetInfo.shelfColumn - lookedInfo.shelfColumn);
}
```

```csharp
float angle = Vector3.Angle(forward, toTarget);
if (angle <= maxAngleDeg) return true;
```

## Common failures

### Compile error

Run:

```bash
./scripts/unity-compile-check.sh
```

If Unity is already open, the script reads the latest `Editor.log` instead of trying to start a second editor instance.

### Shortcut conflict

If Unity shows a shortcut conflict dialog for the Codex build command, use the menu item directly:

- `Tools > Codex > Build And Run Android`

The repo script already uses the menu path rather than a conflicting shortcut.

### Accessibility permission

If the automation script cannot control Unity, macOS may be blocking `System Events`.

Fix:

- `System Settings > Privacy & Security > Accessibility`
- Allow the terminal or Codex app to control the machine.

### No device detected

Check:

1. USB cable and power.
2. Developer mode on the Vive Focus Vision.
3. USB debugging accepted on the headset.
4. `adb devices` lists the device as `device`, not `unauthorized`.

Example:

```bash
adb devices
```

### Build succeeded but app did not launch

Check `Editor.log` for lines like:

```text
Application "...apk" installed to device "... [VIVE Focus Vision]".
Launching application "...UnityPlayerGameActivity" on device ...
```

If those are missing, the build may have succeeded but the deploy step did not complete.

## Useful scripts

```bash
./scripts/unity-compile-check.sh
./scripts/refocus-unity-and-build-device.sh
./scripts/install-git-hooks.sh
```

## Short version

If you only remember one loop, use this:

```bash
./scripts/unity-compile-check.sh
./scripts/refocus-unity-and-build-device.sh
adb pull /sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeData ./GazeData
```
