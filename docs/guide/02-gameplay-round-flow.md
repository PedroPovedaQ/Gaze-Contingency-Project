# Gameplay Round Flow

This guide walks through the full lifecycle of a run in the Find Object game, from the first tap that starts the session to the final completion screen.

The short version is:

1. The first tap creates a temporary trigger object.
2. The game manager turns that into the actual session start.
3. Shelf geometry is created once and cached.
4. A deterministic 14-round challenge set is generated from a fixed seed.
5. Round 1 starts with fixation cross + spoken goal (same as later rounds).
6. Each round spawns 42 configured objects on the shelf grid.
7. Eye-gaze dwell captures the target after sustained focus.
8. A transition cross appears, the next goal is shown, then the game pauses briefly.
9. The next round spawns.
10. After the last target is found, the run ends and the NASA-TLX prompt is shown.

## What this game is doing

The game is a controlled visual-search task. Every round places one target and 41 distractors into a fixed 42-object array. The participant must use gaze to find and dwell on the correct object.

The important design property is that the run is not improvisational. The shelf layout, target schedule, and object combinations are deterministic. That makes the study repeatable and makes the analytics interpretable.

## Timeline of a complete run

This is the exact order of major events.

1. Participant taps a plane or start surface.
2. `ObjectSpawner` creates a temporary object.
3. `FindObjectGameManager.OnObjectSpawned()` intercepts that object.
4. The game waits for voice readiness if needed.
5. `StartGame()` resolves the spawn center and shelf orientation.
6. `ShelfSpawner.CreateShelvesAndSpawnPoints()` builds the shelf geometry once.
7. `ChallengeSet.Rounds` is loaded and round 1 is selected.
8. A fixation cross appears with round 1 goal text in the top-left.
9. The voice assistant starts announcing the round goal.
10. The cross disappears after the transition pause.
11. A randomized blank pause runs.
12. `DoSpawnRound()` enqueues the target and distractor combinations.
13. Objects are instantiated and configured.
14. `XRGrabInteractable` components are attached fresh to each object.
15. The gaze dwell selector begins monitoring the objects.
16. The current objective is shown on the table-facing HUD.
17. Player searches and dwells on an object.
18. `GazeHighlightManager` captures the object after sustained dwell.
19. If correct, the game enters transition mode.
20. Current objects are destroyed.
21. A fixation cross appears with the next goal text in the top-left.
22. The cross stays visible for the transition pause.
23. The cross disappears.
24. A randomized blank pause runs.
25. The next round spawns.
26. This loop repeats until round 14 ends.
27. The completion HUD appears and asks for NASA-TLX submission.

## First tap: how the session starts

The game does not begin from a menu button in the usual sense. It begins when the `ObjectSpawner` emits its first spawn event. That initial spawn is treated as the session trigger, not as one of the actual round objects.

The game manager listens for that event:

```csharp
void OnObjectSpawned(GameObject obj)
{
    if (m_State == GameState.Idle)
    {
        var voice = GetComponent<VoiceAssistantController>();
        if (voice != null && !voice.IsReady) { StartCoroutine(WaitThenStart(obj)); return; }
        StartGame(obj);
    }
    else Destroy(obj);
}
```

If voice services are not ready yet, the manager waits. Once ready, `StartGame()` uses the tap location as the initial spawn center, then starts a transition-style first-round intro (cross + goal announcement + blank pause) before spawning round 1 objects.

That startup behavior is deliberate: round 1 now mirrors inter-round timing so participants always receive the goal announcement during a fixation phase, not over active object search.

## Shelf generation

Shelf generation happens once per run, not every round.

The shelf system is responsible for:

- deciding which way the bookshelf should face,
- creating the visible bookshelf geometry,
- computing the fixed spawn slots for all round objects,
- caching that layout so later rounds can reuse it.

The layout is computed from the detected table and the player’s viewpoint. The shelf faces toward the player, and the object-facing rotation is cached for all later spawns.

```csharp
ObjectFacingRotation = Quaternion.LookRotation(facingDir, Vector3.up);
ShelfRight = shelfRight;
ShelfFacing = facingDir;
```

The shelf rows are fixed:

```csharp
const int k_Rows = 7;
const int k_Cols = 2;
```

Each slot uses a deterministic Y position. The shelf code spaces rows evenly and then computes positions within each shelf column:

```csharp
float y = tableY + row * rowH + k_PlankThickness + 0.04f;

Vector3 pos = colCenters[col]
    + shelfRight * localX
    + shelfForward * localZ
    + Vector3.up * (y - tableY);
```

The important consequence is that the same round always uses the same shelf geometry and the same slot structure, which is what makes the study repeatable.

## Deterministic challenge set

The challenge set is pre-generated with a fixed RNG seed:

```csharp
var rng = new System.Random(42);
```

This means the experiment is deterministic across participants. Everyone gets the same round sequence, the same target/distractor composition, and the same shelf slot mapping.

Each round contains:

- 1 target object,
- 13 same-color distractors,
- 13 same-shape distractors,
- the remaining neutral distractors needed to bring the total to 42 objects.

The round record stores both the target and the full object array:

```csharp
s_Rounds[r] = new RoundDef
{
    roundIndex = r,
    blockIndex = r % 2,
    target = target,
    objects = objects.ToArray()
};
```

The schedule is now alternating by round:

- round 1: gaze-unaware
- round 2: gaze-aware
- round 3: gaze-unaware
- round 4: gaze-aware
- and so on

The code that decides this is:

```csharp
public static bool IsGazeAware(int roundIndex, int participantNumber)
{
    _ = participantNumber;
    return (roundIndex % 2) == 1;
}
```

So the current condition schedule is not participant-counterbalanced by block anymore. It is an alternating round-by-round schedule.

## Spawning and configuration

Once the game starts, `DoSpawnRound()` performs the actual round setup.

First it reads the deterministic round definition:

```csharp
var round = ChallengeSet.Rounds[m_CurrentRound];
m_CurrentTarget = (round.target.shape, round.target.color, round.target.colorValue);
```

Then it resolves the condition for that round and updates the hint generator and UI:

```csharp
bool gazeAware = ChallengeSet.IsGazeAware(m_CurrentRound, participantNumber);
CurrentRoundGazeAware = gazeAware;
CurrentRoundConditionLabel = ChallengeSet.GetConditionLabel(m_CurrentRound, participantNumber);

var hints = GetComponent<HintGenerator>();
if (hints != null) hints.gazeAwareTips = gazeAware;
m_UI.SetAgentState(gazeAware, CurrentRoundConditionLabel);
```

### How each object is configured

The manager computes a fixed list of spawn points from the shelf layout:

```csharp
m_SpawnPoints.AddRange(ShelfSpawner.ComputeSpawnPoints(
    m_SpawnCenter, m_PlaneSize, m_PlaneRight, m_PlaneForward, ChallengeSet.ObjectsPerRound));
```

Then it instantiates a prefab for each object and immediately strips out stale interactable state from the prefab copy:

```csharp
var obj = Instantiate(prefabs[0]);

var oldGrab = obj.GetComponent<XRGrabInteractable>();
if (oldGrab != null) DestroyImmediate(oldGrab);
```

After that, the shape/color factory applies the deterministic combo:

```csharp
m_Factory.EnqueueCombo(def.shape, def.color);
m_Factory.ConfigureObject(obj);
```

`ShapeObjectFactory` is what swaps the mesh, applies the color, sizes the object, and adds metadata:

```csharp
var info = obj.AddComponent<SpawnableObjectInfo>();
info.shapeName = shapeName;
info.colorName = colorName;
obj.name = info.DisplayName;
```

It also makes gaze interaction possible:

```csharp
grab.allowGazeInteraction = true;
grab.allowGazeSelect = false;
grab.allowGazeAssistance = false;
```

The game manager then places the object at the assigned shelf slot and corrects special cases like the pyramid base height:

```csharp
obj.transform.position = sp.position;
obj.transform.rotation = ShelfSpawner.ObjectFacingRotation;

if (info.shapeName == "Pyramid")
{
    var p = obj.transform.position;
    p.y -= obj.transform.localScale.y * 0.5f;
    obj.transform.position = p;
}
```

Finally, the game waits one frame, adds a fresh `XRGrabInteractable`, waits for registration, resets gaze dwell state, and only then shows the objective.

## Dwell capture

The actual selection mechanic is gaze dwell.

`GazeHighlightManager` watches the hovered objects on the gaze interactor. When the user keeps looking at the same object long enough, it captures it.

```csharp
if (hoveredObj != null && hoveredObj == m_DwellTarget && !m_CapturedThisTarget)
{
    m_DwellTime += Time.deltaTime;

    if (m_DwellTime >= k_DwellDuration)
        CaptureObject(hoveredObj);
}
```

The dwell threshold is:

```csharp
const float k_DwellDuration = 1.6f;
```

That means the participant must sustain gaze, not just glance over an object.

If the captured object matches the current target’s shape and color, the round counts as correct. Otherwise the UI shows wrong feedback and the game continues.

## Transition cross

When the player finds the correct object, the game does not immediately spawn the next round. It switches into a transition state.

The transition does three things:

1. pauses the timer,
2. destroys all current round objects,
3. shows a fixation cross with the next round’s goal text.

The UI helper for the cross is:

```csharp
public void ShowFixationCross(string color = null, string shape = null)
{
    if (m_CrossGoalText != null)
    {
        bool hasGoal = !string.IsNullOrEmpty(color) && !string.IsNullOrEmpty(shape);
        m_CrossGoalText.enabled = hasGoal;
        m_CrossGoalText.text = hasGoal ? $"Goal: {color} {shape}" : "";
    }
    if (m_CrossCanvasGO != null) m_CrossCanvasGO.SetActive(true);
}
```

That goal text is drawn in black at the top-left of the cross box.

The game manager shows it like this:

```csharp
var nextTarget = ChallengeSet.Rounds[m_CurrentRound].target;
m_UI.ShowFixationCross(nextTarget.color, nextTarget.shape);
OnRoundTransitionStarted?.Invoke(m_CurrentRound, nextTarget.color, nextTarget.shape);
```

## Blank pause

After the transition cross is hidden, the game inserts a blank pause before the next objects appear. This is intentional and is there to reduce anticipation.

The pause is randomized between 0.2 and 1.3 seconds:

```csharp
const float k_BlankPauseMin = 0.2f;
const float k_BlankPauseMax = 1.3f;
```

And the runtime code is:

```csharp
float blankPause = Random.Range(k_BlankPauseMin, k_BlankPauseMax);
yield return new WaitForSeconds(blankPause);
```

This means the participant sees:

1. transition cross,
2. next-goal label,
3. blank screen,
4. next round.

## Next-round spawn

After the blank pause, the manager resets gaze dwell and spawns the next round:

```csharp
if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

m_State = GameState.Playing;
yield return DoSpawnRound();
```

The important point is that the next round is not a continuation of the previous hovered state. It is a fresh state with:

- a new target,
- a new object array,
- a reset dwell timer,
- updated hint mode,
- a fresh UI objective.

## End of run

When the last target is found, the game switches to `Completed`, stops the timer, and shows the completion panel.

```csharp
if (m_CurrentRound >= k_TotalRounds)
{
    m_State = GameState.Completed;
    float elapsed = m_UI.StopTimer();
    m_UI.ShowCompletion(k_TotalRounds, elapsed);
    OnGameCompleted?.Invoke(elapsed);
    StartCoroutine(ResetAfterDelay());
}
```

The completion HUD explicitly asks for the post-run survey:

```csharp
m_CompletionText.text =
    $"All {total} rounds complete!\n" +
    $"Time: {timeStr}\n\n" +
    "Please submit NASA-TLX now.";
```

## Why the flow is structured this way

This flow is intentionally rigid.

- Deterministic challenge generation keeps the study reproducible.
- Shelf caching keeps layout stable across rounds.
- Fresh object instantiation prevents stale interactable state.
- Dwell capture standardizes selection.
- Transition cross plus blank pause reduces anticipatory behavior.
- Alternating aware/unaware rounds make the condition schedule easy to analyze.

If you are debugging the game, the most useful mental model is:

`tap -> bootstrap -> build shelf once -> first-round cross+announcement -> spawn deterministic round -> dwell capture -> transition cross -> blank pause -> next round -> repeat -> completion`
