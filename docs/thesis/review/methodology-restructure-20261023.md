# Methodology restructure — 2026-10-23

Author-directed structure: six chapters; Methodology third and Results fourth. Preserve approved abstract, prior methods/scoring content, source snapshot, Qualtrics hold and the distinction between implemented apparatus and pending participant results.

## Sources inspected
- Gabriela R. Shamblin (2025), The Impact of Visualization Styles on Movement Imitation Accuracy in Virtual Reality: https://stars.library.ucf.edu/etd2024/224/ ; full PDF https://www.eecs.ucf.edu/isuelab/publications/pubs/GS_Thesis.pdf . Read contents and methodology opening for organization only. No prose or participant findings transferred.
- Kumaran et al. (2023), outdoor AR navigation aids: https://doi.org/10.1145/3544548.3581413 ; author PDF https://sites.cs.ucsb.edu/~holl/pubs/Kumaran-2023-CHI.pdf . Read abstract, introduction and task context; no claim that its apparatus used GPS. The author's specifically remembered GPS study has not been conclusively identified.
- HTC Focus Vision official overview https://www.vive.com/us/product/vive-focus-vision/overview/ and specifications https://www.vive.com/us/product/vive-focus-vision/specs/ . 2448x2448 per eye, 90 Hz standalone, up to 120-degree FOV. DisplayPort beta 120 Hz display and 120 Hz binocular gaze specification are separate from delivered standalone frame/sample rates.
- Existing full-text review notes support Losing, Kwok, Coughlan, Binetti, Zhang, and Chiossi citations. Low-vision relevance is a proposed adaptation, not an established validation of this method.

## Implementation sources
Assets/ChallengeSet.cs: six shape types, four colors, 168 development objects, eight planes, 80 measured trials, one practice. Eight trials per nominal absolute-angle category does not mean eight objects per plane. Final participant density remains unresolved in the protocol snapshot.
Assets/Replay/TrialReplayRecorder.cs: frame recording and shared gaze/controller evidence; rendered frames are not unique eye samples.
tools/trial-review/export_run.py: exact 17 condensed columns; reserved subjective_performance_rating blank; inherited practice/quality statuses require full trials.csv. Post-onset head-relative geometry is not pre-cue cross-relative geometry. No data or scoring semantics changed.

No new pilot or participant experiment was run for this structural edit. Results now has dedicated User Performance and User Experience sections with honest pending status and existing attributed engineering evidence.
