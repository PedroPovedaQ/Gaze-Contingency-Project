# Review findings: Chapters 3–6 (no participant data)

The draft is generally careful about proposed versus implemented status. The issues below are ordered by priority. Locations are given as chapter and section.

## High priority

**1. Pointing acknowledgment: is it independent of gaze or not?**
- Ch5 "Gaze Contact…" calls controller pointing "independent evidence."
- The same section, and the `fig:evidence-events` caption, say pointing acknowledgment *requires valid nearby gaze*.
- Ch4 says gaze and controller confirmations will be "evaluated separately against their corresponding evidence."
- Ch3 never mentions the one-second hold or the gaze requirement. It only refers to "the dwell required for spoken pointing confirmation."
- **Fix:** State the full pointing gate once in Ch3. Drop "independent" in Ch5, or qualify it as "independent of trigger selection." Make Ch4's separate evaluation account for the shared gaze requirement.

**2. The reference direction for nominal wall angles is undefined.**
- The Ch4 allocation table lists "Absolute nominal wall angle" (0–180) without saying what it is measured from.
- Ch5 reports that existing theta fields are "relative to the previous target wall." The exact θ₀ is relative to the readiness cross.
- Ch3's worked example refers to "the wall opposite the starting cross."
- **Fix:** State where the readiness cross is placed on each trial (for example, on the previous target wall), and what the nominal categories are measured from. The primary covariate and the balance claims depend on this.

**3. The primary estimand is stated two ways.**
- Ch6 §Primary contrast defines the estimand as standardized to a common starting-difficulty distribution, which is marginal.
- Ch6 §Candidate model says exp(β_V) is *conditional*, and that the report "should state whether" it presents a coefficient or standardized predictions.
- **Fix:** Choose one estimand in §Primary contrast, or explicitly mark it as open in both places.

**4. The voice contrast is attributed only to "voice identity."**
- Ch4 §Design says the comparison concerns voice identity. However, the catalog voice is *chosen by the participant*, with gender presentation also selected. The clone is not chosen.
- The contrast therefore combines self-similarity with chosen-versus-assigned voice, and possibly with gender mismatch and synthesis-quality differences.
- **Fix:** Name these differences in the design rationale and in the interpretation limits.

**5. Ch3 states outcomes that have not been measured.**
- "helps a seated user find a target"
- "keeps task-critical decisions deterministic and auditable." Speech timing is decided by Jev asynchronously, so it is not deterministic.
- "reduce spatially incorrect or stale guidance"
- "so the system does not direct the participant away…"
- **Fix:** Reword these as design intent ("is designed to," "is intended to") and limit "deterministic" to spatial and eligibility logic.

## Medium priority

**6. Is Jev blind to voice condition, and is the model version fixed?** Ch3 lists "the voice changes" as a staleness condition, which suggests the snapshot may include voice information.
- State whether voice condition is visible to Jev. If it is, Jev's phrasing and timing choices could differ by condition.
- Add the language model's name and version and its sampling settings to the frozen-policy manifest (Ch3, final paragraph). Ch3 currently logs these details for ElevenLabs only.

**7. Controller-hit suppression: any object or the target only?**
- Ch3 says "A controller hit … an object they are already pointing at."
- Ch5 and the worked example say a *target* hit.
- If the rule applies to any object, pointing at a distractor would suppress valid directions. Make Ch3 specific.

**8. "Selected" is used for two different things.** Ch3 says "when gaze-vicinity regions overlap, the closest object is selected." Elsewhere "selection" means a trigger press. Use "chosen as the vicinity candidate" or similar.

**9. Motion normalization contradicts itself.**
- Ch4 reports gross totals, with covered time reported alongside them.
- Ch5 says "the denominator is observed covered time," which implies a rate.
- Choose totals or rates, or define both.

**10. The scope of the instruction count is unclear.**
- Ch4 counts "guidance utterances" during active search.
- Ch5 lists acknowledgments and reminders as separate categories.
- State whether gaze and pointing acknowledgments count as instructions.

**11. The live viewer and transfer lag appear without explanation.**
- The Ch5 timing table includes "Live transfer lag … viewer receipt," and the text mentions "a late-arriving file in the viewer."
- Ch3 describes Trial Review only as reconstruction from saved evidence and never mentions a live data link.
- Either describe the transport in Ch3 or remove the row.

**12. "Feedback that qualifies for local handling" is never defined.** This phrase (Ch3; also "time-sensitive qualified feedback") determines which cues bypass Jev, which affects latency attribution. List the categories.

**13. The scene figure shows a highlighted target.** The `fig:scene` caption describes a "highlighted target," but the target identity is withheld from participants. Add the same disclaimer the architecture caption uses ("not a participant-visible target cue").

**14. Ownership in the table and the text disagree.** In Ch3, the text gives the round manager readiness, onset, completion, and blocks. The table gives it reveal, selection outcome, and *pause*, and omits readiness. Reconcile the two, and state who owns pause intervals.

**15. The "October 20 audit" cannot be traced.** Ch5 cites it with no year and no appendix or source. Cite the record or describe it generically.

**16. The random voice slope may be over-interpreted.** In Ch6, u_Vi is described as "heterogeneous voice responses." With one block per voice, it also absorbs block-level session effects such as recalibration or fatigue. Qualify the interpretation, or consider a participant-by-block term.

**17. The vicinity region size is never given.** Ch3 and Ch5 describe the region only as "larger." The Ch5 warning about premature acknowledgment needs the actual angular or metric tolerance, or an explicit "to be set in pilot."

**18. The retrospective block reference is not explained.**
- Ch4 §Procedure asks participants to rate "Block 1" and "Block 2" after both blocks.
- It does not say how participants are reminded which voice each block used, for example a replayed sample or a label.
- This affects how valid the recalled ratings are.

## Low priority

- **Status wording in Ch3.**
  - "Participants sit…" and "we report ray intersections" use present tense for a study that has not run. Use "will."
  - "A state machine must represent these returns…" reads as a requirement. The figure caption calls the states "Implemented." Say which it is.
  - "A documented 150-ms fade": state whether it is implemented.
- **"Completes the trial" (Ch5).** "Correct trigger selection is the only event that completes the trial" sits awkwardly with stops and timeouts. Use "successfully completes."
- **Inconsistent terms.**
  - AR (Search Task) versus mixed reality (Apparatus).
  - "Time to final target gaze" (Ch4) versus "last gaze-contact time" (Ch5). "Time to" is also misleading for a last event.
  - "First controller contact" (Ch4) versus "First pointing contact" (Ch5 table).
- **Effects stated for one voice only.** "No pitch or spatial effects are applied to the self-similar voice" should cover both conditions.
- **Unclear table cell.** "Successful trigger selection by speech alone" (guidance machine row) does not make clear what it excludes. Reword.
- **Worked example (Ch3).**
  - A target "near the wall opposite" cued as "to the right" depends on the target's offset or the 180° tie convention noted in Ch5. Say so.
  - The sentence ruling out "Trial Review camera" as a reference is confusing in a description of live guidance. Move it to the Trial Review section or remove it.
- **Unclear referent.** "The memory is cleared whenever guidance resets": the spatial history, the speech/state memory, or both?
- **Offset correction undefined.** Ch4 §Procedure mentions targets "used to fit an offset correction," but Ch3 does not describe any offset correction.
- **Figure fallbacks and alt text.**
  - The architecture and procedure figures share generic alt text ("study procedure or guidance pipeline"). Write specific alt text for each.
  - The architecture fallback box lacks the panels (a)–(d) and the inset that the caption refers to.
  - The procedure fallback omits the voice audition, the voice recording, and both SSQ administrations.
