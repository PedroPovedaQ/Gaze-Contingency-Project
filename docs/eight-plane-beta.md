# Eight-plane rotational search

**Implemented October 23, 2026:** compact vertical layout `rotational_scatter_v5_compact_vertical` uses a 2.2036 m wall (70% of the former 3.148 m). Object centers span 0.10–2.1036 m above the floor; radius, horizontal extent, shape scale, 21 objects per wall and 0.20 m minimum center separation are unchanged. Deterministic placement is regenerated within this shorter range. New replay snapshots save exact local wall bottom/height; legacy object coordinates and inferred walls are not compressed retroactively. Earlier geometry descriptions below are historical.

**Implemented locally — October 1, 2026; headset acceptance pending.** This is the current seated octagonal layout. Historical fixed-seed, table/shelf, 10-trial-block, first-person/target-naming and either-controller descriptions are superseded. The [round-flow guide](guide/02-gameplay-round-flow.md) owns current lifecycle behavior.

## Scene reference and geometry

After voice setup/audio checks, use the right-ray **Center and begin** checkpoint while facing the intended room front. Floor tracking must be available. The captured seated position, horizontal heading and floor reference define the layout; turning your head does not move it. A room-setup table is not required.

Eight vertical tangent faces surround the participant at 45-degree intervals. Each holds 21 objects (168 total). `RotationalSearchLayout` defaults to a 1.5 m horizontal radius and slot heights from 0.10 to 3.048 m above the floor; runtime Inspector settings can change the range before centering. Nominal slot sampling uses 0.20 m minimum center separation and a corner inset. These are implementation settings, not validated comfort or visual-angle limits. Saved object transforms account for subsequent mesh-specific positioning.

Thin noninteractive outlines meet at their corners. At outline radius `r + 0.08 m`, half-width is `(r + 0.08) × tan(π/8)`. There are no opaque backing panels or new physical walls. **Settings → Guidance boundary** toggles wall outlines and degree labels together without changing object positions or target selection. The right controller can open settings; another hand/controller is unnecessary.

## Allocation and hidden-target guidance

The current `previous-wall-angle-v5-stratified-40-trials` schedule has two 40-trial blocks plus two separate practices. There are eight instances of each absolute theta category per block (0, 45, 90, 135, 180 degrees), arranged in independently shuffled five-trial groups and mirrored direction pairs. Participant-specific seeds and actual assignments are saved; `rotational_scatter_v4` describes the scatter method, not a promise that old generator versions used identical seeds.

Theta is the shortest turn from the previous target wall, established by a world-fixed readiness cross. The first practice initializes at room front. The participant is not instructed to reset to room front after every trial. One second of continuous valid gaze inside the padded cross and completed readiness speech unlock search; this gate is distinct from object selection.

The agent knows the hidden target. It first guides horizontal turning toward its wall, then finer horizontal alignment and up/down adjustments. Both voices use shared participant-directed commands. Horizontal zone membership is independent of ordinary pitch changes; near-vertical gaze with unstable azimuth is unavailable evidence. See [guidance policy and pilot thresholds](guide/03-gaze-agent-and-telemetry.md).

Use the right ray to highlight an object and a fresh trigger press to confirm. Wrong choices continue the same search; correct choices stop active timing, record selection and play a chime/varied reinforcement before transition. Gaze does not select objects. The timing/progress panel remains, without a target label.

## Open decisions and headset checks

- **Signed heights:** random floor-relative scatter does not implement 20 above / 20 below. Reference, zero/deadband and allowable magnitude need to be frozen before balancing.
- Verify floor alignment and usable heights/distances while seated, especially extreme upper/lower positions and seams. Confirm object overlap/visibility on the actual headset.
- With the left controller off, verify centering, settings, boundary toggle, pause/continue/repeat, highlight, one entry haptic and fresh-trigger selection across all faces.
- Check independent gaze targets and drift; looking upward within the correct wall must not retrigger arrival. Test blinks, loss/recovery, boundary jitter and overshoot without inferring fixation validity.
- Complete a fresh 80-trial run and reconcile geometry, schedule, outcomes, voice/block boundaries and recording completeness. Historical P020 is not a validation run for this design.

[Schedule details](balanced-angle-schedule.md) · [Setup/session schematic](../resources/figures/follow-my-voice-study-flow-20261001.md) · [Headset QA](voice-study-qa.md)
