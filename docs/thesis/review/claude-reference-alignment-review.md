# Review: first-pass additions (RQ traceability, related-work map, reproducibility, evaluation design, Appendix E)

**Overall:** Nothing blocks advisor circulation. The additions consistently mark material as proposed, constructed, or code-inspected, and they claim no participant results. The figure count also reconciles: 3 reused, 10 new, and 1 capture make 14. Two High items should still be fixed before sending. Both concern places where design intent or unverified scope reads as inspected fact.

---

## High

**H1. Appendix E pseudocode mixes behavior confirmed in the inspected code with design requirements. The inspected file list does not cover several of the claims.**

The inspected sources are `JevDecisionClient.cs`, `SpeechDecisionMemory.cs`, and `GuidanceStateMachine.cs`. Several claims sit outside those files or are not stated as verified:

- Step 5's pre-playback checks (cancellation, clip readiness, audio occupancy)
- Step 6's commit of playback-start history
- "Correct right-trigger selection remains a separate round-manager path"

Chapter 3 also asserts as fact that "software still rechecks current evidence before playback." The scheduler, playback, and round-manager code are not in the listed files.

Suggested addition after the pseudocode lead-in:
> "Steps 2 and 4 and the memory behavior in Section~X correspond to the inspected files. Steps 3, 5, and 6 describe the required admission boundary; the scheduler, playback, and round-manager code that would implement them were not part of this inspection and must be verified in the release source."

In Chapter 3, replace "and software still rechecks current evidence before playback" with:
> "and the intended design rechecks current evidence before playback (Appendix~\ref{app:decision-contract} identifies which checks were inspected)."

**H2. The new appendix does not say whether voice condition or tone reaches the provider. This bears directly on the IV.**

Chapter 3 already says condition-blindness "must be verified against the frozen payload." Appendix E lists payload fields but does not answer that question. The quoted instruction also conditions wording on a "requested tone" (Casual/Direct/Technical). If tone or a voice identifier differs by condition, the provider's choices could vary with condition, which would confound RQ1–RQ3.

Add to "State, History, and Evidence Fields":
> "This inspection did not establish whether voice-condition identifiers, voice IDs, or condition-linked tone requests appear in the serialized payload. Until the frozen payload is audited, provider decisions cannot be assumed condition-blind, and the requested tone must be shown to be held constant across voice conditions."

(Remove "did not establish" if the inspection actually did check this, and report the result instead.)

---

## Medium

**M1. RQ3 in the traceability table is narrower than Chapter 4's exploratory list.**

Chapter 4 says the method-level questions "retain that same scope." It then lists eeriness, voice experience, search behavior, and variation of the voice contrast with starting angle. None of these appears in the RQ3 row. Either widen the row or name the leftovers.

Suggested RQ3 row:
> "How do voice conditions differ in perceived agent qualities, eeriness, voice experience, reported reliance, and search behavior, and does the voice contrast vary with starting angle?"

Evidence column: add "gaze/motion-derived search measures subject to the validity requirements in Section~X; theta-by-voice interaction (exploratory)."

**M2. The manipulation check is placed in two incompatible roles.**

The table and Chapter 7 "Results Organized by Research Question" put the voice-similarity check under RQ3. Chapter 7's existing text presents it *before* the primary contrast, as a prerequisite for interpreting RQ1.

In the RQ3 sentence, replace "alongside debrief themes and the voice-similarity manipulation check" with "alongside debrief themes." Then add before the RQ1 sentence:
> "The voice-similarity manipulation check will be reported first, as a condition for interpreting all three questions."

Make the matching edit in Table~\ref{tab:rq-map}.

**M3. Worked case 2 contradicts the reference policy's local-feedback path.**

Chapter 3, policy-table row 1, and appendix step 4 all say qualified local feedback "can bypass a new request." Case 2 describes "repeated model waits" before a looking acknowledgment. Readers cannot tell which policy case 2 assumes.

Suggested replacement for the case's fourth sentence:
> "If the policy under review routes this acknowledgment through Jev (for example, the Jev-led role in Table~\ref{tab:policy-pilots}), repeated model waits followed by playback at $t_p$ form one delayed gaze-confirmation episode; under the reference policy the acknowledgment should use the local path, and a comparable delay would instead implicate scheduling or delivery."

**M4. The Chapter 9 EQ1 paragraph overstates status and cites an undefined audit.**

- "The current alignment audit supports specific implementation checks" refers to an audit the reader has not been given.
- "the implemented inspection mechanism" conflicts with the appendix's statement that the source is an unreleased working tree.

Suggested replacement:
> "For EQ1, the inspected working implementation and the proposed data contract provide a way to inspect sensing, state, decisions, and software playback separately. The October 23 code inspection (Appendix~\ref{app:decision-contract}) documents specific request, validation, and memory behavior. It does not establish deployment, a held-out guidance error rate, or the removal of delayed confirmations. The contribution is therefore an inspection mechanism and an explicit evaluation design whose performance remains to be measured."

**M5. Participant-derived voice assets in the release manifest are potentially identifying.**

The manifest retains "voice ID/condition" and "prepared clip hashes." For the self-similar condition these are derived from a participant's voice. The caption excludes "unauthorized identifiable recordings" but says nothing about these identifiers.

Add to the Phrase and voice assets row:
> "Participant-derived voice IDs and clips are pseudonymized, access-restricted, and recorded with their provider-side retention status."

**M6. Replaying the evaluation corpus sends recorded state to a hosted model. The corpus's origin is unstated.**

If the development or held-out corpora come from participant sessions, offline replay through `typesafe-ai/jev` is further cloud processing of participant data. Chapter 9 already names "consent alignment with cloud processing" as open. Chapter 7 should connect to it.

Add to the development/evaluation subsection:
> "Corpora drawn from participant sessions will be replayed through hosted providers only where consent and data-handling terms permit; the release record will state each corpus's source (scripted, developer, pilot, or participant)."

(This is thesis text about permitted use and does not change the deliberately general IRB language.)

**M7. Judge blinding and agreement statistics are not specified.**

For policy comparisons, human reviewers and the model judge should be blind to policy identity and voice condition. The agreement statistic should also be declared in advance.

Add:
> "Reviewers and the model judge will not see policy labels or voice condition. Human–human and judge–human agreement on the calibration subset will be reported with a prespecified statistic, and episodes labeled unknown will remain in the denominator."

---

## Low

**L1. "Fourth participant hypothesis" (Ch1).** The RQs are two-sided questions, not hypotheses, and the same paragraph disclaims directional hypotheses. Replace with "rather than a fourth participant research question."

**L2. "Prespecified standardized time contrast" (Ch7).** Chapter 9 says the primary analysis and sample size are not yet finalized. "Standardized" also leaves unclear whether a raw-seconds estimate is reported. Replace with:
> "the primary voice contrast in seconds (with a standardized effect size), as specified in the final analysis plan before confirmatory recruitment."

**L3. "Stated multiplicity family" (Ch7, RQ2).** Confirm that Chapter 4 or the analysis chapter actually defines this family. If it does not, write "a multiplicity family to be defined in the final analysis plan."

**L4. Reporting destinations disagree.** The table sends RQ1 to `ch:analysis`. Chapter 7, which is `ch:evaluation`, says "the completed results chapter will answer RQ1." Pick one destination. Also confirm that `ch:analysis`, `ch:evaluation`, `ch:measurement`, and `ch:system` all resolve. Check that the Discussion actually covers independent manipulation of memory and autonomy, as Chapter 9's new cross-reference claims.

**L5. "Production endpoint wrapper" (Ch7).** The source is a working tree, not production. Replace with "the inspected request wrapper."

**L6. Causal or hedged wording in Appendix E:**
- "This protects against treating a pending controller hold as already qualified" → "This phase is intended to keep a pending controller hold distinct from a qualified one."
- "without intentionally removing current geometry" → "Factoring is intended to preserve per-candidate geometry; retained serialized requests should confirm this."

**L7. Worked case 1.** "suppress the away-direction" is unclear, since a downward cue is not necessarily away from the target. It also states a design norm as "correct." Replace with:
> "Under the intended policy, the search direction is suppressed and the hold gate preserved."

**L8. Source status should be readable from the PDF itself.** `review/implementation-contract-inspection.json` may not ship with the PDF. Add one inline sentence giving the base commit and the fact that the tree was dirty. Also replace "recovered working implementation" with "uncommitted working implementation," or explain "recovered."

**L9. Trial Review capture provenance (App. D).** Replace "An actual Trial Review capture shows its explicitly synthetic bundled demonstration" with:
> "One screenshot of the Trial Review tool displays the tool's bundled synthetic demonstration data, not participant data; its capture date and source revision are recorded in \path{figures/README.md}."

**L10. Constructed JSON example.** If its field names do not exactly match the inspected parser schema, add "schematic" to the label.

**L11. Novelty phrasing (Ch2).** "The resulting contribution is a particular combination" follows an explicit disclaimer that the review is not systematic. Soften to:
> "Within the reviewed work, this study is positioned at a particular combination…"

**L12. Layout.** `\newpage` directly after `\chapter{…}\label{…}` will likely leave the appendix title alone on a page. Remove it unless the UCF template requires appendix title pages.

**L13. Literature table.** Include the task characterizations in the final source-to-claim audit already listed in Appendix D:
- zhang2023seehear: "known-target"
- kao2021selfsimilar: "educational"
- guo2024doppelganger: "embodied collaboration"

---

## Note (no thesis change requested)

The project `CLAUDE.md` describes the following:
- 14 rounds, 56 objects, and two practice trials
- Within-session voice counterbalancing as open work

The thesis describes 40-trial blocks, 168 objects (21 per wall), and one practice trial. The thesis frames counterbalanced blocks as proposed, which is consistent with the open-work status. Even so, the thesis should not cite `CLAUDE.md` as a status source. Before circulation, confirm that no sentence describes within-session counterbalancing or the 168-object layout as implemented in a released build.
