# Protocol and survey handoff — October 20, 2026

Pedro requested this update for the main implementation thread, **Update v2 research run sheet**. Automatic delivery failed; this file is the fallback handoff.

## Scope and authoritative documents

The side conversation updated [Overleaf](https://www.overleaf.com/project/6a84bc097073254fbca6985b). **Qualtrics must remain on hold** until Pedro authorizes synchronization. No Unity code, Git state, device build or Qualtrics changes were made here. Document decisions are not proof of runtime implementation.

Claude is editing `section-03-system-description.tex` in this same Overleaf project. Read live sources before updating and preserve its recent prose. Do not bulk-upload older local copies.

## Study decisions

- Two counterbalanced voice blocks: participant-preferred catalog voice versus self-similar synthetic voice. Forty measured trials per block, 80 total; two practice trials, one per voice.
- Same gaze-contingent guidance policy and semantic phrase library across conditions. Target identity is withheld from opening instructions. Selection uses the right-controller ray and trigger.
- Readiness and the opening directional cue precede object reveal and active search timing.
- Primary outcome: active reveal-to-correct-trigger completion time, excluding logged pauses, adjusted for absolute pre-cue starting theta. Preserve signed theta too; never divide completion time by theta.
- Exact pre-cue theta is the signed horizontal angle between reference-origin-to-cross and reference-origin-to-target vectors. Keep this distinct from nominal wall assignment, post-reveal head-to-target error and accumulated head rotation.
- Planned schedule: eight trials in each absolute nominal wall-angle category (0/45/90/135/180 degrees) per block. Twenty targets above and twenty below **readiness-cross height**, not seated eye height. Realized vertical quotas and feasible ranges need runtime/pilot verification.
- Sample size, power-model details, calibration acceptance, practice proficiency, timeout and technical-stop thresholds remain to be established. No pilot results or approval claims were invented.

## Final survey decisions

After each block: **65 numeric responses**, plus technical checks and optional text.

| Component | Count | Scoring |
|---|---:|---|
| NASA-TLX | 6 | Documented 0–10 adaptation, no pairwise weighting; performance 0 Perfect to 10 Failure; individual dimensions plus complete unweighted mean |
| IMI | 24 | Interest/enjoyment 7, effort/importance 5, pressure/tension 5, value/usefulness 7; separate subscale means, no overall total |
| Guo agent perception | 21 | Q1–6 co-presence, Q13–18 intelligence, Q20–22 appealing/eerie/familiar, Q37–42 anthropomorphism |
| Custom numeric ratings | 14 | Twelve agreement items, voice liking 0–100, guidance-use percentage 0–100 |

Guo Q20–22 were explicitly restored by Pedro. Analyze appealing, eerie and familiar ratings individually; do not average them into an eeriness score. Adapt “virtual character” to “voice agent.” Source: [Guo et al. (2024), Appendix A Table A1](https://doi.org/10.1145/3651288).

All four accent questions were explicitly removed: self-described accent, preferred agent accent, perceived agent accent and accent similarity. Voice similarity and preferred/perceived gender questions remain. Existing custom familiarity/uncanniness items remain separately identified from Guo.

Custom agreement items: voice similarity, responsiveness, helpfulness, distraction, familiarity, trust, comfort, uncanniness, reliance, naturalness, willingness to follow, motivation to act without guidance. These are individual items, not a validated composite.

Baseline: researcher-assigned study code; age range, gender, handedness, visual correction, XR experience, eye-tracking experience, own-voice-clone experience, AI-assistant familiarity and 16-item SSQ. Setup: explicit agent-gender preference, liking every auditioned voice including the clone, preferred catalog-voice choice and clear/comfortable audio check. Final SSQ and open-first debrief remain.

## Completed documentation

- Removed Current status banners and obsolete/self-referential drafting language.
- Updated questionnaire, scoring guide, run sheet, protocol and manuscript copies.
- Added vector procedure and architecture figures, with PDF and editable SVG assets.
- Added 3.1 Apparatus using verified Unity 6000.3.10f1, OpenXR 1.16.1, VIVE OpenXR 2.5.1, XRI 3.3.1 and URP 17.3.0; standalone HTC Vive Focus Vision Android ARM64 rendering, eye/head tracking and right controller.
- Expanded Section 4: Task; Design and trial allocation; operational Measures; Research questions; Participants; Procedure; Pilot testing and parameter checks.
- Measures distinguish first/last observed gaze contact, controller contact and trigger selection; geometry, head-motion and zone measures; actual guidance playback counts and timing stages; subjective instruments and missing-data rules.
- Changed sources compiled. Section 4 rendered as five pages and was visually checked in Overleaf.

## Implementation and analysis alignment checks

1. Recovered analysis code distinguishes legacy planned wall theta (historically previous-target-wall-center) from first tracked **post-onset** geometry. Verify that the intended exact **pre-cue cross-based** reference is captured and exported. Do not relabel legacy fields as equivalent.
2. Existing export documentation describes NASA-TLX on 0–100, while the survey decision is raw 0–10. Retain instrument/range metadata; make any conversion explicit and avoid mixing scales silently.
3. Contact latency is a tracked ray intersection, not fixation, vicinity qualification or spoken confirmation. Missing contacts remain missing, not zero. Preserve evidence sources and sampling resolution.
4. Motion totals use valid adjacent samples and quaternion angular displacement. Axis components are rotation-vector components, not Euler yaw/pitch/roll. Do not bridge pauses or tracking gaps.
5. Trial summaries may repeat block survey values for convenience, but analyses must retain participant-by-block scope rather than treating those repetitions as independent responses.
6. Claude's current Section 3 has `[X]`/`[Y]` gaze-memory entry/blink-tolerance placeholders and an unresolved `bovo2025embardiment` citation. Verify these claims against runtime and the source before assigning values. The side conversation preserved that prose rather than inventing thresholds.

## Artifact locations

Root: `/Users/pedro.poveda/.codex/visualizations/2026/10/20/01a15a14-9f81-77d1-b70e-80ef3b93c350/`

- Latest source snapshot and revised experiment/combined documents: `protocol/experiment-measures-20261020/`
- Survey-specific revision: `protocol/eerie-no-accent-20261020/`
- Apparatus change and pre-edit Claude source: `protocol/apparatus-20261020/`
- Figure PDFs, SVGs and generation sources: `study-figures/`

These are local snapshots. Live Overleaf may contain newer concurrent edits.
