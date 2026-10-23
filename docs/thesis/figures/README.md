# Figure provenance

The 14 numbered figures combine three project assets, ten new vector explanations, and one actual application capture. None depicts newly collected participant results.

## Reused project assets

- `image.png`: existing Overleaf search-scene illustration, preserved in `source-snapshot/`. Drawn object count is schematic.
- `study-procedure-current.svg/pdf`: current project procedure diagram. Editable SVG copied from Overleaf; the working PDF was regenerated with embedded fonts. The active labels use neutral voice; the source snapshot remains unchanged.
- `voice-guidance-memory-architecture.svg/pdf`: current Overleaf architecture diagram. Same font-embedding treatment, with the active label updated to neutral voice; source snapshot remains unchanged.

## New explanatory figures

`generate-thesis-diagrams.py` creates paired editable SVG and vector PDF files. It requires Python, CairoSVG, and pypdf; run from any directory. Tested versions are CairoSVG 2.9.1 and pypdf 6.21.2. Install these in the Python environment used to run the generator (`python -m pip install CairoSVG==2.9.1 pypdf==6.21.2`). On this machine the local Cairo library is under `/opt/homebrew/lib`. The generated diagrams describe the source snapshot and planned measurement contract; their captions identify implementation versus proposal. Geometry and timeline figures are schematic and not measured traces.

Each export runs `normalize-pdf-font-maps.py` to make implicit identity maps in embedded TrueType CID fonts explicit, as required by PDF/UA-2. After exporting another SVG manually, run that helper on its PDF too. It preserves existing mappings and content streams and leaves already-normalized files untouched. The thesis's LuaTeX import keeps each PDF's original transparency group rather than appending a duplicate group dictionary. The checked-in figure PDFs require no Python dependencies for an ordinary thesis build.

- `comparison-framework`: intended voice manipulation and common task.
- `guidance-states`: common state families, not an exhaustive transition graph.
- `evidence-events`: distinct gaze, controller, and trigger events.
- `bounded-memory`: different spatial and speech/state history limits.
- `starting-geometry`: nominal wall assignment versus exact cross-relative angle.
- `height-reference`: cross-relative vertical demand versus live guidance.
- `latency-definition`: qualification versus software delivery delay.
- `data-provenance`: raw records, summaries, reconstruction, and analysis.
- `survey-scopes`: final administration with two block references.
- `analysis-units`: repeated trials and participant-by-block ratings.

## Application screenshot

`trial-review-synthetic-demo.png` was captured directly through the native UI from the verified October 20 Trial Review application at `live-audio-detached-verified/Trial Review.app`. It displays the bundled **SYNTHETIC / Run 1** demonstration with three sample trials. It is not a wearer recording, not a current live session, and not evidence for measured system performance. The caption preserves the application's missing-state and reconstructed-highlight caveats. The screenshot has not been composited or redrawn.

No new screenshots of actual participant recordings or model-evaluation results are represented in this draft.
