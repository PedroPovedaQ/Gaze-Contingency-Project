# Enhanced Gaze Contingency System

## Context

The Gaze Contingency Project is a VR research study testing whether gaze-aware AI hints improve spatial search performance vs. time-only hints. The current implementation has two weaknesses:

1. **Task too easy** — 9 objects in a 3x3 grid on a flat table. Players can scan left-to-right and find everything without needing gaze guidance.
2. **Gaze contingency too thin** — Only one gaze-driven trigger (10s target stare nudge). All other hint timing is fixed intervals (15s first, 20s subsequent). Gaze data is in the LLM prompt but doesn't drive behavior.

**Goal:** Make the gaze-contingent agent behave meaningfully differently from a time-only agent, so the research can show a real effect.

---

## Changes Overview

| Area | What | Files |
|------|------|-------|
| Task difficulty | 56 objects per round on a deterministic 2-column × 7-row bookshelf layout | `ShapeObjectFactory.cs`, `FindObjectGameManager.cs`, `SpawnableObjectInfo.cs`, `ShelfSpawner.cs` |
| Gaze tracking | Per-object fixation tracking, gaze history, behavior classification | New `GazeCoverageTracker.cs` |
| LLM context | Add gaze history, examined/unexamined objects, zone coverage to prompt | `AgentContext.cs` |
| Adaptive timing | Dynamic hint intervals based on gaze behavior, new triggers | `HintGenerator.cs` |
| Wiring | Connect new components | `VoiceAssistantController.cs` |

---

## Phase 1: Task Difficulty

### 1A. Add shapes — `Assets/ShapeObjectFactory.cs`

Add **Cylinder** (Unity built-in `PrimitiveType.Cylinder`) and **Star** (procedural mesh, like existing Pyramid) to `EnsureInitialized()`.

- `m_ShapeMeshes` grows from 3 → 5
- `m_ShapeNames` becomes `{ "Sphere", "Cube", "Pyramid", "Cylinder", "Star" }`
- 6 shapes x 4 colors = **24 distinct combos**
- Add collider cases in `ReplaceCollider()`: Cylinder → CapsuleCollider, Star → SphereCollider

### 1B. Add shelf level field — `Assets/SpawnableObjectInfo.cs`

Add `public int shelfLevel;` (0=table, 1=lower shelf, 2=upper shelf) and a `LevelName` property for display.

### 1C. Create shelf spawner — new `Assets/ShelfSpawner.cs`

Static utility class. Called by `FindObjectGameManager.StartGame()` after plane detection.

- Creates 2 shelf platforms above the detected table plane (+25cm, +50cm)
- Each shelf = GameObject with BoxCollider + semi-transparent MeshRenderer
- Computes grid positions per level: ~7 objects on table, ~7 on shelf 1, ~6 on shelf 2
- Returns `(List<GameObject> shelfObjects, List<List<Vector3>> positionsPerLevel)`

Shelf geometry:
- Dimensions match detected table size minus 3cm inset per side
- Thickness: 1.5cm, alpha: 0.3 (semi-transparent white)
- Box colliders for physics — objects drop onto them via gravity (existing behavior)

### 1D. Multi-level spawning — `Assets/FindObjectGameManager.cs`

- `k_ObjectCount` from 9 → 20
- Replace inline 3x3 grid computation with `ShelfSpawner.CreateShelvesAndPositions()` call
- Track shelf GameObjects in `m_ShelfObjects` list, destroy in `ResetAfterDelay()`
- Track `m_LevelAssignments` (parallel to `m_SpawnedObjects`) — set `shelfLevel` on each object's `SpawnableObjectInfo` in `OnObjectFullyConfigured()`

---

## Phase 2: Gaze Coverage Tracking

### 2A. Create tracker — new `Assets/GazeCoverageTracker.cs`

Auto-attaches to ObjectSpawner. Shares the gaze interactor from `GazeHighlightManager` (resolved in `Start()`, same pattern as `AgentContext`).

**Data structures:**
- `Dictionary<string, ObjectFixationRecord>` — per-object: total fixation time, visit count, last visit time, shelf level
- `FixationEvent[]` ring buffer (capacity 20) — recent fixation sequence with object ID, duration, shelf level
- `float[]` zone tracking — last scan time and total fixation time per shelf level

**Update logic (every frame):**
1. Read `m_GazeInteractor.interactablesHovered` for current hover target
2. On hover change: finalize previous fixation (add to records, push to ring buffer), start new one
3. Update zone tracking times

**Behavior classification — `ClassifyBehavior()` → `GazeBehavior` enum:**

Examines last 10 fixation events from ring buffer:
- **Systematic**: avg fixation > 1.5s, mostly unique objects (low revisit), good coverage
- **Erratic**: avg fixation < 0.6s, high zone-switching (>4 in 10 events)
- **Stuck**: >70% of events in one zone, search time > 15s
- **Normal**: everything else

**Public API:**
- `ClassifyBehavior()` → GazeBehavior enum
- `GetRecentFixations(int count)` → last N fixation events
- `GetExaminedObjects(float minDuration)` → objects with fixation >= threshold
- `GetUnexaminedObjects()` → objects never/barely looked at
- `GetZoneLastScanTime(int level)` → when zone was last scanned
- `GetRevisitCount(string objectId)` → visit count for confusion detection
- `GetMostRevisitedNonTarget()` → non-target with highest revisit count
- `Reset()` → clear all data (called on game start)

---

## Phase 3: Enhanced LLM Context

### 3A. Enhance prompt — `Assets/AgentContext.cs`

Change `Initialize()` signature to accept `GazeCoverageTracker`. Add four new sections to `BuildContextPrompt()`:

```
GAZE HISTORY (recent): blue sphere (2.1s) → red cube (0.8s) → yellow pyramid (0.4s)
OBJECTS EXAMINED: Red Sphere (3.2s), Blue Cube (1.1s), Yellow Pyramid (0.4s)
OBJECTS NOT YET EXAMINED: Purple Cylinder, Blue Star [and 8 more]
ZONE COVERAGE: table=well-scanned, lower shelf=briefly glanced, upper shelf=NOT SCANNED
```

Zone classification thresholds: <1s = NOT SCANNED, 1-5s = briefly glanced, 5-15s = partially scanned, >15s = well-scanned.

Cap lists to prevent prompt bloat: examined max 10 entries, unexamined max 5 + "N more", history max 8 events.

Rename "OBJECTS ON TABLE" → "OBJECTS ON DISPLAY", add shelf level to each object's description (e.g., "on lower shelf").

Add vertical component to `DescribeObjectRelativeToPlayer()` ("above/below eye level").

### 3B. Update system prompt — `Assets/HintGenerator.cs`

Update `k_SystemPrompt` to mention shelves and vertical directions.

---

## Phase 4: Adaptive Hint Timing

### 4A. Replace fixed timing — `Assets/HintGenerator.cs`

Current fixed timing: first hint 15s, subsequent 20s, min gap 8s.

New adaptive timing based on `GazeCoverageTracker.ClassifyBehavior()`:

| Behavior | Hint Interval | Rationale |
|----------|--------------|-----------|
| Systematic | 12-15s | Player is doing fine, give them space |
| Normal | 9-12s | Moderate pacing |
| Erratic | 5-7s | Player is lost, help sooner |
| Stuck | 7-10s | Player fixated on one area, redirect |

### 4B. New trigger conditions — `Assets/HintGenerator.cs`

Priority chain in `Update()`:

| Priority | Trigger | Condition | Situation description |
|----------|---------|-----------|----------------------|
| 1 | Gaze nudge (existing) | 10s staring at target | "Player looking at target but hasn't grabbed..." |
| 2 | Wrong grab (existing) | 3s after wrong grab | "Player grabbed wrong object..." |
| 3 | **Zone neglect** (NEW) | Any zone unscanned 20s+ | "Player has not looked at the [zone]..." |
| 4 | **Revisit confusion** (NEW) | Same non-target viewed 3+ times | "Player keeps looking at [object]..." |
| 5 | **Adaptive timer** (NEW) | Dynamic interval from behavior | Behavior-specific description |

Behavior-specific situation descriptions:
- Erratic: "Player's gaze is erratic, they seem lost. Give a clear, calming directional hint."
- Stuck: "Player has only been searching one area. Suggest they look elsewhere."
- Systematic + >45s: "Player is searching methodically but hasn't found it yet. Give a more specific hint."
- Default + >60s: "Player has been struggling for over a minute. Give a more direct hint."

### 4C. Wire tracker — `Assets/VoiceAssistantController.cs`

- Create `GazeCoverageTracker` in `OnEnable()`, pass to `AgentContext` and `HintGenerator`
- Call `tracker.Reset()` in `HandleGameStarted()`
- Update welcome message to mention shelves

---

## Edge Cases & Risks

- **Spawn order**: Shelves must be created before objects so colliders exist for gravity drop — guaranteed since `ShelfSpawner` is called synchronously before the spawn loop
- **Object sliding off shelves**: Add friction (physics material) to shelf colliders, or set objects kinematic after 0.5s settling
- **Prompt length**: Capped lists prevent context window issues. Total growth ~300-400 chars
- **Auto-attach ordering**: GazeCoverageTracker resolves gaze interactor in `Start()` (not `OnEnable()`), same safe pattern as AgentContext
- **GazeDataLogger compatibility**: Unchanged — reads the same hover data independently, no conflicts

---

## Verification

1. **Shelf spawning**: Start game → verify 2 transparent shelf platforms appear above table → objects distributed across 3 levels
2. **Shape variety**: Verify all 6 shapes render correctly, colliders are targetable by gaze
3. **Coverage tracking**: Add `Debug.Log` in GazeCoverageTracker → verify fixation records accumulate → verify behavior classification changes as you look around systematically vs. erratically
4. **Enhanced prompts**: Log `BuildContextPrompt()` output → verify GAZE HISTORY, EXAMINED, UNEXAMINED, ZONE COVERAGE sections appear with correct data
5. **Adaptive timing**: Compare hint timing with systematic scanning (should be 25-30s intervals) vs. erratic looking (should be 8-12s intervals)
6. **Zone neglect trigger**: Avoid looking at one shelf level for 20s+ → verify hint fires directing you there
7. **Revisit confusion**: Look at the same wrong object 3+ times → verify hint fires redirecting you
8. **End-to-end**: Play a full run and verify hints are contextual, reference gaze behavior appropriately, and the round flow/logging remain stable across the deterministic shelf layout

---

## Implementation Order

1. `ShapeObjectFactory.cs` — add Cylinder + Star meshes and colliders
2. `SpawnableObjectInfo.cs` — add `shelfLevel` field
3. `ShelfSpawner.cs` — new static utility
4. `FindObjectGameManager.cs` — deterministic 56-object round spawning on the bookshelf layout
5. *Test: game runs with the current deterministic 56-object round setup and shelf layout*
6. `GazeCoverageTracker.cs` — new component
7. `VoiceAssistantController.cs` — wire tracker
8. *Test: tracker accumulates data*
9. `AgentContext.cs` — enhanced prompt
10. *Test: prompt includes new sections*
11. `HintGenerator.cs` — adaptive timing + new triggers
12. *Test: full adaptive behavior*
