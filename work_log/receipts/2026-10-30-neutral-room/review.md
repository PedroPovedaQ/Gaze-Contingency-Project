# Neutral room code review

## Scope and intent

Reviewed the room change against `fb6e439`, including new StudyRoomEnvironment/StudyRoomChecks files and all modified runtime, replay, capture, tests and documentation. Inherited progress.md excluded. Intent: invisible layout guides by default, neutral floor/distant walls without input interference, matching replay with legacy support, verified thesis images and local build/commit only.

## Coverage

Applied ce-simplify-code and ce-code-review sequentially in the main thread, as required by the supplied repository Task/Subagent mapping. No independent agent or external peer review is claimed. Correctness, testing, adversarial failure scenarios, maintainability/resource ownership, additive API contract and project-standards lenses were applied. Root agents.md and supplied instructions govern; CLAUDE.md's older study counts are explicitly superseded. No Compound Packs or relevant learnings corpus configured.

Traced creation through centering/start/practice/blocks and cleanup through post-run/reset; checked five-panel mesh winding/size/origin, layer and collider absence, unchanged selection/readiness geometry, default/wrist/live guide visibility, material lifetime, built-in replay shader inclusion, URP player shader compilation, optional JSON field and old-schema fallbacks. Checked camera masks include Ignore Raycast. No public input authorization or voice pipeline changed.

Simplification: one shared geometry implementation serves runtime and replay; no copied room constants or new package. Removed the capture-only label-hiding override so the figure uses runtime visibility. Existing tests retained. Comments documenting Unity lifecycle and historical recording semantics retained. No behavior-bearing simplification beyond that deletion.

Resolved issues during implementation: template mesh occluded new room (observed GPU images; runtime suppression/restoration fixed); initial wall height would expose sky behind high objects (raised to 6 m); editor destruction did not restore backdrop (failing lifecycle assertion; ExecuteAlways fixed it). Final GPU/headless checks pass, old/new replay switches pass, all six final PNGs inspected. Test strategy was source characterization before visual changes, GPU verification for appearance, and integration assertions for interaction and lifecycle; no claim of a pre-implementation red test for pure styling.

## Actionable Findings

None remaining for the requested local change. No proof tests weakened or removed. No independent-review corroboration is claimed.

## Limitations

No wearer/headset verification or photometry. Wireless helper found no device; APK remains uninstalled. Existing VIVE spectator and manifest build-hook exceptions also occur in the previous build, while both builds report success. Built-in desktop and URP headset renderers share room dimensions/colors, but this is not display photometric equivalence.

## Verdict

Ready for the requested local commit. Installation and wearer acceptance remain external next steps; no publishing authorized.
