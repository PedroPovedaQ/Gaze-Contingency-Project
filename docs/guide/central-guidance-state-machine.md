# Guidance state machine — implemented locally, October 20, 2026

```mermaid
stateDiagram-v2
    [*] --> Inactive
    Inactive --> Opening: round announced
    Opening --> Suspended: search awaits valid evidence
    Suspended --> Turning: valid gaze outside wall
    Suspended --> Refining: valid gaze inside wall
    Turning --> Refining: enter target wall
    Refining --> Turning: leave target wall
    Turning --> AcquiringTarget: target wins gaze vicinity
    Refining --> AcquiringTarget: target wins gaze vicinity
    AcquiringTarget --> Acquired: gaze qualifies and controller off target
    AcquiringTarget --> ReadyToSelect: gaze qualifies and controller on target
    Acquired --> ReadyToSelect: right controller hits target
    ReadyToSelect --> Acquired: controller leaves target
    Acquired --> Refining: gaze leaves target within wall
    ReadyToSelect --> Refining: gaze leaves target within wall
    Refining --> Suspended: invalid gaze or geometry
    Turning --> Suspended: invalid gaze or geometry
    Acquired --> Suspended: invalid gaze or geometry
    ReadyToSelect --> Suspended: invalid gaze or geometry
    ReadyToSelect --> Completed: correct right-trigger selection
    Paused --> Suspended: resume and reacquire evidence
    Completed --> Opening: next round
```

The phase is recalculated from evidence; valid recovery can also enter AcquiringTarget directly. Any active search phase can pause, stop technically, or complete on a valid right-controller selection: selection does not require gaze confirmation. Paused, Stopped and Inactive are explicit lifecycle states. This diagram shows common paths, not every possible edge.

The state machine selects an allowed action separately from its phase. Looking confirmation needs 0.1 seconds; pointing confirmation needs a one-second controller hold with qualifying target gaze. A valid 0.15-second gaze departure rearms gaze confirmation; invalid tracking does not count as departure. Gate consumption happens only at actual software playback start. These are pilot interaction settings, not validated fixations.

```mermaid
flowchart LR
    A[Unity gaze and controller observations] --> B[Pure C sharp guidance state machine]
    B --> C[Eligible action and current evidence]
    C --> D[Decision provider: currently Jev]
    C --> E[Qualified local prompt events]
    D --> F[Shared request and playback scheduler]
    E --> F
    F --> G{Still relevant before playback?}
    G -->|Yes| H[Prepared audio and action commit]
    G -->|No| I[Discard and reassess]
    B --> J[Replay state snapshots]
    H --> J
    J --> K[Trial Review state markers and evaluation]
```

Sources: Assets/GuidanceStateMachine.cs, Assets/HintGenerator*.cs, Assets/GuidanceRequestScheduler.cs, Assets/VoiceSynthesizer*.cs, and the Trial Review runtime. No automatic-evaluation score is a substitute for participant perception or validated sensor accuracy.
