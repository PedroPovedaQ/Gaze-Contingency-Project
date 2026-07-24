# System Diagram

## Flow Summary

This diagram shows the full runtime and analysis loop for the project.

At a high level, the system does four things:

1. Boots the mixed-reality scene and detects a usable table surface.
2. Waits for the participant to tap the table, then starts the experiment once any spoken intro is finished.
3. Runs repeated search rounds where gaze controls hover, dwell capture, hint generation, and event logging.
4. Saves the run data so it can be pulled from the headset and analyzed locally.

The boxes in the flowchart include the main file that owns each step, so the chart can be used both as a runtime overview and as a code map. In general:

- `FindObjectGameManager.cs` controls experiment state and round progression.
- `ShelfSpawner.cs` builds the bookshelf structure and spawn positions used by each round.
- `ShapeObjectFactory.cs` post-processes spawned prefabs into the actual experimental stimuli.
- `SpawnableObjectInfo.cs` attaches the per-object metadata used throughout the study.
- `FindObjectUI.cs` controls the visible study UI.
- `HintGenerator.cs` controls gaze-aware and gaze-unaware hint behavior.
- `VoiceAssistantController.cs` controls spoken instructions and audio feedback.
- `VoiceSynthesizer.cs` handles TTS generation and playback for spoken prompts.
- `GazeHighlightManager.cs`, `GazeDataLogger.cs`, and `TrialDataLogger.cs` handle gaze interaction and logging.
- The `analysis/` scripts turn exported run data into plots, tables, and statistics.

Repository: [Gaze Contingency Project](https://github.com/PedroPovedaQ/Gaze-Contingency-Project)

<div style="page-break-after: always;"></div>

```mermaid
flowchart TD
    A["Launch app<br/>GazeContingencyStudyScene.unity"] --> B["Detect table plane<br/>VivePlaneProvider.cs"]
    A --> C["Show start prompt<br/>FindObjectUI.cs"]
    A --> D["Prepare voice + game systems<br/>VoiceAssistantController.cs<br/>FindObjectGameManager.cs"]

    B --> E["User taps table<br/>FindObjectGameManager.cs"]
    E --> F{"Intro still speaking?"}

    F -->|Yes| G["Show: Please wait...<br/>FindObjectUI.cs"]
    G --> H["Wait for intro to finish<br/>VoiceAssistantController.cs"]
    F -->|No| I["Start experiment<br/>FindObjectGameManager.cs"]
    H --> I

    I --> J["Show fixation cross + announce goal<br/>FindObjectUI.cs<br/>VoiceAssistantController.cs"]
    J --> K["Spawn bookshelf objects<br/>ShelfSpawner.cs<br/>ShapeObjectFactory.cs"]

    K --> L["Eye gaze tracks hovered object<br/>GazeHighlightManager.cs"]
    L --> M["Dwell captures object<br/>GazeHighlightManager.cs"]

    M --> N{"Correct object?"}
    N -->|Yes| O["Next round or finish<br/>FindObjectGameManager.cs"]
    N -->|No| P["Show wrong feedback + play buzz<br/>FindObjectUI.cs<br/>VoiceAssistantController.cs"]

    L --> Q["Generate hints<br/>HintGenerator.cs"]
    Q --> R{"Gaze-aware round?"}
    R -->|Yes| S["Use gaze to say cold / on-track / very-close<br/>HintGenerator.cs"]
    R -->|No| T["Give generic encouragement<br/>HintGenerator.cs"]

    I --> U["Log data<br/>GazeDataLogger.cs<br/>TrialDataLogger.cs"]
    U --> V["gaze_log.csv<br/>GazeDataLogger.cs"]
    U --> W["trial_events.csv<br/>TrialDataLogger.cs"]
    U --> X["trial_summary.json<br/>TrialDataLogger.cs"]

    X --> Y["Pull GazeData from headset<br/>adb pull"]
    Y --> Z["Run analysis script<br/>scripts/run-analysis-pipeline.sh<br/>analysis/run_analysis.py"]
    Z --> AA["Plots + stats in analysis/results<br/>analysis/plots.py<br/>analysis/stats.py"]
```
