# Schedule v8 code review

Scope: HEAD to final working tree, including inherited schedule work and the new ScheduleChecks fixture; progress.md excluded from commit. Intent: exactly 32 measured trials per voice, 64 total, with signed-step/height/identity allocation and historical replay compatibility.

Coverage: ce-code-review correctness, testing, adversarial, contract, maintainability and project-standards lenses performed sequentially in the main thread under the repository's Task/Subagent mapping. No independent peer or subagent review is claimed. Standards: root agents.md / supplied AGENTS.md. No Compound Packs configured. Review checked generator allocation indexing (all 32 IDs exactly once/block), target bag boundaries, deterministic draws, block transition, full/debug/quick manifests, legacy v5/v7 branches, replay/export saved metadata, power-simulation geometry, and scope exclusions.

Findings resolved before final validation:
- Editor allocation export inherited ambient voice-block/quick-test flags. Fixture now sets study/block mode before export and restores prior flags/debug limit. Python validator accepted the actual C# export.
- Headless screenshot capture called Camera.Render on the Null graphics device and crashed native URP line rendering. Guard only the capture, then independently run and inspect GPU capture; full integration checks passed both modes.
- Existing stationary-menu fixture lacked following/provider labels now read by LateUpdate. Added fixture UI components; full integration passed.
- Power-planning code assumed multiples of five. New version supports 32 with 4/8/8/8/4 geometry, while v1 preserves historical groups; allocation, simulation and plotted CLI artifact tests pass.

Simplification pass: existing bag shuffle and seeded layout logic retained; no new runtime abstractions or dependencies. No proof tests removed or weakened. Height and direction contracts remain explicit at generator, independent validator and geometry integration boundaries.

Residual risks: no wearer completion, comfort or eye accuracy validation. Historical PDFs/illustrations are marked historical, not regenerated. Android build logged a vendor VIVE spectator-camera preprocessor NullReferenceException at GetOpenXrExtensionInfo (null extension string Split); Unity continued, returned success, and the exact APK installed/launched. No vendor/package changes made for this schedule task.

Actionable Findings: none remaining for the schedule change.

Verdict: ready for the requested local commit; no publication or participant-data work authorized or performed.
