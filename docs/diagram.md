# Current system diagram

**Implemented locally — October 1, 2026; headset acceptance pending.** The runtime uses seated octagonal search, hidden targets, the same gaze-contingent commands in both voice conditions, and right-controller ray/trigger selection. The older table-tap, dwell-selection, alternating gaze-awareness and 14-round diagrams are historical. Existing `diagram.pdf`, `diagram_chart.png` and other old exports have not been regenerated and must not be treated as current.

**Protocol proposal:** consent, external questionnaires, breaks, refitting and debrief sit around the runtime. Their final instruments and operator order require research review. The figure does not establish approval, eye-tracking validity or power.

[Setup/session vector figure and caption](../resources/figures/follow-my-voice-study-flow-20261001.md) · [Runtime overview](guide/01-system-overview.md) · [Round flow](guide/02-gameplay-round-flow.md) · [Telemetry](guide/03-gaze-agent-and-telemetry.md)

```mermaid
flowchart TD
    A["Setup with right controller ray + trigger<br/>VoiceModeSelector / StudyRayControls"] --> B["Choose presentation and one of six voices<br/>Record natural sample; prepare/check both voices"]
    B --> C["Fit and calibrate; independent Eye check<br/>Center seated octagonal layout"]
    C --> D["One separate practice trial<br/>First assigned voice"]
    D --> E["Researcher checkpoint starts measured run<br/>32 trials per voice; saved counterbalanced order"]
    E --> F["Previous-target-wall readiness cross<br/>TrialReadinessGate / FindObjectGameManager"]
    F --> G{"Valid continuous readiness<br/>AND announcement finished?"}
    G -->|No| F
    G -->|Yes| OPN["Opening direction completes while targets stay hidden"]
    OPN --> H["Reveal 168 objects; begin active-search clock"]
    H --> I["Shared valid/fresh gaze<br/>Horizontal guidance, then fine / vertical cues"]
    I --> J["Right-ray object highlight + entry haptic<br/>Fresh trigger confirms selection"]
    J --> K{"Correct target?"}
    K -->|No: retain target| I
    K -->|Yes| L["Record capture; stop search clock<br/>Chime + varied active-voice reinforcement"]
    L --> M{"Measured trial 32 or 64?"}
    M -->|No| F
    M -->|32| N["Break / comfort / tracking check<br/>Confirm next voice block"]
    N --> F
    M -->|64| O["Both retrospective block questionnaires; interview / debrief<br/>Save and verify session package"]
    H -. "Explicit pause: hide stimuli,<br/>pause search clock; Continue resumes" .-> H
    I -. "Invalid/stale gaze: suspend new cues" .-> I
    H --> P["Separate linked recording layers"]
    P --> Q["EyeMeasurements: raw observations,<br/>validity, timing and independent checks"]
    P --> R["GazeReplays: scene, controller/head/gaze,<br/>selection, guidance and audio events"]
    P --> S["GazeData: trial/events/object/survey files<br/>and compatibility gaze CSV"]
    Q --> T["Post-session quality and identity checks<br/>One performance/subjective CSV per trial"]
    R --> T
    S --> T
    R --> U["Unity replay / browser timeline<br/>Original samples and gaps; optional local video"]
```

The first practice initializes at room front; later readiness uses the previous target wall. The diagram's loop summarizes measured trials; practice remains separately identified. A correct choice ends active-search timing before success feedback, and recording continues through that feedback. Invalid gaze is not a head-gaze fallback. Explicit participant-requested pauses and technical stops retain their outcomes.

The editable Mermaid source is [diagram.mmd](diagram.mmd). This diagram summarizes responsibilities; precise thresholds, missingness rules and recorded-field definitions belong to the linked guides and source code.
