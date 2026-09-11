# Gameplay Round Flow

This guide walks through the full lifecycle of a run in the Find Object game, from the first tap that starts the session to the final completion screen.

**Implemented:** neutral profile selection → self-similar enrollment → both phrase libraries prepared and audio samples accepted → two practice trials → seven experimental trials → block-1 NASA-TLX and break → seven trials in the other voice → block-2 NASA-TLX. Guidance is always gaze-contingent. See [QA procedure](../voice-study-qa.md).

## What this game is doing

The game is a controlled visual-search task. Every round places one target and 55 distractors into a fixed 56-object array. The participant must use gaze to locate and dwell on the correct object.

The important design property is that the run is not improvisational. The shelf layout, target schedule, and object combinations are deterministic. That makes the study repeatable and makes the analytics interpretable.

## Timeline of a complete run

1. The first surface tap establishes the shelf geometry; progress waits for both voice libraries and accepted playback samples.
2. Two separate practice trials expose the task in each assigned voice. A researcher checkpoint starts the measured run.
3. `OnGameStarted` opens one run folder for all 14 experimental trials.
4. Each transition sets the assigned block voice and announces the goal while objects are prepared but hidden.
5. `OnRoundReady` records object readiness. After the announcement completes, `BeginSearch` reveals objects, resets dwell, starts the search clock and emits `OnSearchStarted`.
6. Correct capture stops search timing and logs the completed trial; wrong capture remains within that trial.
7. After trial seven, logs flush, NASA-TLX records block 1 and a checkpoint holds the break. The next voice is applied only after confirmation.
8. After trial fourteen, the summary is finalized and NASA-TLX records block 2. Timeout, withdrawal and technical stops retain an incomplete summary without replacement trials.
9. Explicit pause hides objects and stops speech/dwell/timing; confirmation resumes the same trial with accumulated search time preserved.

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
- the remaining neutral distractors needed to bring the total to 56 objects.

The round record stores both the target and the full object array:

```csharp
s_Rounds[r] = new RoundDef
{
    roundIndex = r,
    blockIndex = 0,
    target = target,
    objects = objects.ToArray()
};
```

**Implemented:** All 14 rounds use gaze-contingent hints, regardless of participant number or selected voice. `ChallengeSet.IsGazeAware()` always returns true, and `blockIndex` is 0 for trials 1–7 and 1 for trials 8–14. Voice order is saved per coded participant.

## Spawning and configuration

Once the game starts, `DoSpawnRound()` performs the actual round setup.

First it reads the deterministic round definition:

```csharp
var round = ChallengeSet.Rounds[m_CurrentRound];
m_CurrentTarget = (round.target.shape, round.target.color, round.target.colorValue);
```

The manager exposes `CurrentRoundGazeAware = true` and `CurrentRoundConditionLabel = "gaze_aware"` throughout the run. `HintGenerator` has no unaware toggle or control policy.

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
- A single gaze-contingent policy is used throughout either voice run.

If you are debugging the game, the most useful mental model is:

`tap -> bootstrap -> build shelf once -> first-round cross+announcement -> spawn deterministic round -> dwell capture -> transition cross -> blank pause -> next round -> repeat -> completion`
