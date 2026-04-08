# Gaze Contingency Guide Set

This folder is a complete walkthrough of how the project works, from runtime architecture to round logic, gaze hints, logging, and device workflow.

## Read Order

1. [01-system-overview.md](01-system-overview.md)  
   Big-picture architecture, key scripts, and how systems connect at runtime.
2. [02-gameplay-round-flow.md](02-gameplay-round-flow.md)  
   Exact per-round lifecycle, transition timing, deterministic challenge generation, and spawn logic.
3. [03-gaze-agent-and-telemetry.md](03-gaze-agent-and-telemetry.md)  
   Eye-gaze pipeline, gaze-aware hint behavior, dwell, and what gets logged.
4. [04-build-deploy-and-debug-workflow.md](04-build-deploy-and-debug-workflow.md)  
   Safe edit workflow, compile checks, build/deploy steps, and data retrieval.

## Scope

These guides match the current codebase behavior, including:

- alternating gaze-aware / gaze-unaware rounds
- two-level gaze-aware hints (`on-track/very-close` vs `off-target`) with wrong-bookcase mismatch guard
- empathetic control-mode hints with repetition control
- fixation cross + randomized blank pause transition flow (including first round start)
- table-mounted objective panel and transition-only goal text
- NASA-TLX submission prompt after run completion

## Primary Source Files

- `Assets/FindObjectGameManager.cs`
- `Assets/FindObjectUI.cs`
- `Assets/HintGenerator.cs`
- `Assets/GazeHighlightManager.cs`
- `Assets/GazeDataLogger.cs`
- `Assets/ShelfSpawner.cs`
- `Assets/ChallengeSet.cs`
- `Assets/MRTemplateAssets/Scripts/ARFeatureController.cs`
