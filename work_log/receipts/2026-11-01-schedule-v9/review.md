# Schedule v9 review

Sequential main-thread review under the repository AGENTS tool mapping; no independent-peer review claimed. Base: `85072c1`. Scope: 96-trial runtime, validation/replay/export compatibility, power simulation/report, documentation and verification artifacts. Inherited readiness edits preserved; raw audit receipts and inherited progress excluded from the planned commit.

## Actionable findings

- Fixed report input validation: reject 32-trial inputs labeled as 96 and incomplete/under-refined N=20 comparisons; negative tests cover both.
- Simplified legacy sensitivity schedule validation to explicit allowed counts; retained old v1/v2 contracts.
- Fixed remaining current flowchart branches at the old 32/64 boundary and the inherited reference to a nonexistent readiness report.
- Corrected test fixtures at the new boundary and the test-only object-count typo; runtime remains 168 objects.

## Coverage

- Correctness: exact signed-step, height and identity allocations; shuffle groups, immediate-repeat avoidance, participant seed, practice isolation, full/quick/debug lengths and break only at index 48.
- Compatibility/API: saved version/count metadata remains authoritative; v7/v8 replay/export and legacy 14/80 trial readers retained. No recording migration or participant data writes.
- Testing: production C# schedule cross-checked by Python; both Unity graphics modes and offline 50-seed checks; all active Python suites and pinned numerical references; desktop build and synthetic UI capture.
- Statistical integrity: original 0889229 generator/diagnostics/fits exactly reproduced at block size 32; same assumptions, seed policy and fitting model. Failed fits remain in denominators; seed-prefix refinements replace rather than pool; successful-only censoring bias retained explicitly. N=20 compares 10,000 replicates per cell for both designs.
- Maintainability/scope: existing schedule constants drive consumers; no new assembly references, participant/microphone/voice-provider behavior or model changes. Historical reports are immutable; current documentation distinguishes implementation, proposals and open decisions.
- Deployment: inactive headset verified before replacement, standard Current/Previous desktop rotation, APK hash equality, MiniLM v9 health. Paused headset is not wearer evidence.

## Verdict

Code/documentation review complete with no remaining actionable findings. Final generated-report numbers, figure rendering and artifact hashes are verified separately in the power archive validation receipt before commit.
