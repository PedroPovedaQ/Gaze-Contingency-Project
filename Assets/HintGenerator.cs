using UnityEngine;

/// <summary>Shared valid gaze drives coarse wall guidance, then two-axis target refinement.</summary>
[DefaultExecutionOrder(350)] // After ControllerRaySelector's current-frame telemetry.
public class HintGenerator : MonoBehaviour
{
    public const string AreaCorrectionPhrase = DirectionalHintPolicy.AreaPhrase;
    const string k_TargetReturnCategory = "target_passage_return";
    const string k_RevisitCategory = "recent_target_return";
    const double k_DirectionSettleSeconds = GuidanceSpeechPolicy.DirectionSettleSeconds;
    const float k_TipInterval = .6f; // Pilot cadence; same policy in both voice conditions.
    readonly GuidanceTurnState m_Turn = new GuidanceTurnState();
    readonly GazeReturnCueGate m_TargetRevisit = new GazeReturnCueGate();
    int m_ProgressVariant;
    float m_TurnRemaining;
    string m_OpeningTurn;
    double m_LastTargetVisitTime = -1;
    long m_LastTargetVisitSequence = -1;
    double m_InvalidGazeSince = -1;
    readonly GazeTargetHistory m_TargetHistory = new GazeTargetHistory();
    float m_YawError, m_PitchError;
    float m_VerticalError, m_VerticalFraction = float.NaN;
    bool m_VerticalMeasured;
    int m_CandidateVerticalBand, m_LastVerticalBand;
    bool m_GeometryAvailable, m_GazeOnTarget;
    string m_GazeRayHit = "unavailable";
    readonly GazeZoneEntryGate m_ZoneEntry = new GazeZoneEntryGate(6, k_DirectionSettleSeconds);
    readonly GazeZoneEntryGate m_PointingEntry = new GazeZoneEntryGate(0, 1.0);
    readonly GazeZoneEntryGate m_LookingEntry = new GazeZoneEntryGate(0,
        DirectionalHintPolicy.LookingHoldSeconds, DirectionalHintPolicy.LookingExitSeconds);
    readonly GazeReturnCueGate m_ReturnCue = new GazeReturnCueGate();
    readonly StudyPromptDelay m_SelectionReminder = new StudyPromptDelay(3);
    bool m_TargetAcknowledged;
    string m_ReturnDirection, m_CurrentHint, m_Candidate;
    string m_LastSuspension;
    GazeObservation m_EvaluatedSample;
    double m_CandidateSince;
    float m_LastTipTime;
    bool m_TipsSuppressed = true;
    int m_Variant, m_AreaVariant, m_ReturnVariant, m_LookingVariant;
    bool m_ContinuationUsed;
    VoiceSynthesizer m_Voice;
    SpatialVoice.StudyNavigationVoiceBridge m_Navigation;
    FindObjectGameManager m_GameManager;
    ControllerRaySelector m_Controller;
    GazeTargetVicinity m_Vicinity;
    SpawnableObjectInfo m_EvaluatedTarget;
    string m_LastDirection;
    float m_LastDirectionError;
    bool m_VicinityMeasured, m_ControllerEvidenceValid, m_ControllerOnTarget;
    float m_VicinityError, m_VicinityRadius;
    GameObject m_ClosestVicinityObject;
    int m_VicinityCandidateCount;
    bool m_VicinityAmbiguous;
    float m_ClosestVicinityError;
    public GazeTargetVicinity Vicinity => m_Vicinity;
    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null) => m_Voice = voice;
    void Start()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
        m_Navigation = GetComponent<SpatialVoice.StudyNavigationVoiceBridge>();
        m_Vicinity = GetComponent<GazeTargetVicinity>() ?? gameObject.AddComponent<GazeTargetVicinity>();
    }
    public void OnNewObjective()
    {
        m_Voice?.ClearGuidanceEvidence();
        m_SelectionReminder.Reset(); m_TargetAcknowledged = false;
        m_Turn.Prime(m_OpeningTurn); m_OpeningTurn = null;
        m_TargetRevisit.Reset(); m_ProgressVariant = 0;
        m_LastTargetVisitTime = -1; m_LastTargetVisitSequence = -1;
        m_InvalidGazeSince = -1;
        m_ZoneEntry.Reset(); m_PointingEntry.Reset(); m_LookingEntry.Reset(); m_ReturnCue.Reset(); m_TargetHistory.Reset();
        m_CurrentHint = m_Candidate = m_ReturnDirection = null;
        m_LastDirection = null; m_ContinuationUsed = false;
        m_LastSuspension = null;
        m_Variant = m_AreaVariant = m_ReturnVariant = m_LookingVariant =
            (ChallengeSet.ScheduleSeed % 8 + (m_GameManager != null ? m_GameManager.CurrentObjectiveIndex : 0)) % 8;
        m_LastTipTime = Time.time; m_TipsSuppressed = false;
    }
    public void OnWrongCapture() { }
    public void CancelPending()
    {
        m_Voice?.ClearGuidanceEvidence();
        m_Navigation?.Cancel();
        m_SelectionReminder.Reset(); m_TargetAcknowledged = false;
        m_Turn.Reset(); m_TargetRevisit.Reset(); m_OpeningTurn = null;
        m_TipsSuppressed = true; m_ZoneEntry.Reset(); m_PointingEntry.Reset(); m_LookingEntry.Reset(); m_ReturnCue.Reset(); m_TargetHistory.Clear();
        m_Vicinity?.Hide();
        m_CurrentHint = m_Candidate = m_ReturnDirection = null; InterruptGuidance(true);
        m_LastDirection = null;
    }
    void LateUpdate()
    {
        if (m_Voice == null || m_GameManager == null || m_TipsSuppressed) { PreviewPausedVicinity(); return; }
        if (!m_GameManager.SearchActive || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
        { InterruptGuidance(true); m_TargetHistory.Clear(); PreviewPausedVicinity(); m_CurrentHint = null; return; }
        string desired = Evaluate(out bool valid, out bool inside, out bool far);
        bool pointing = EvaluatePointing(valid);
        bool looking = EvaluateLooking(valid);
        bool revisit = m_TargetRevisit.Update(valid, looking, Time.timeAsDouble);
        bool selectionReminderDue = m_SelectionReminder.Step(valid && looking && m_TargetAcknowledged, Time.unscaledDeltaTime);
        bool lookingEntry = m_LookingEntry.Update(valid, looking, Time.timeAsDouble);
        if (valid && looking && m_LookingEntry.InsideStable)
        { m_LastTargetVisitTime = m_EvaluatedSample.receiptTime; m_LastTargetVisitSequence = m_EvaluatedSample.sequence; }
        // Hold duration belongs to the controller. Gaze must be valid/nearby at
        // announcement, but a blink must not erase a continuously tracked ray hit.
        bool pointingEntry = m_PointingEntry.Update(m_ControllerEvidenceValid, m_ControllerOnTarget, Time.timeAsDouble);
        UpdateTargetHistory(valid);
        bool entry = m_ZoneEntry.Update(valid, inside, Time.timeAsDouble);
        bool returning = m_ReturnCue.Update(valid, inside, Time.timeAsDouble);
        UpdateSpeechEvidence(valid, inside, looking);
        if (!valid)
        {
            string reason = m_EvaluatedSample == null ? "no_gaze_observation" :
                string.IsNullOrEmpty(m_EvaluatedSample.invalidReason) ? "unusable_gaze_or_target_geometry" : m_EvaluatedSample.invalidReason;
            if (m_LastSuspension != reason)
                Record(Decision("suspended", reason, null, "", false, inside, far, false));
            m_LastSuspension = reason;
            if (m_InvalidGazeSince < 0) m_InvalidGazeSince = Time.realtimeSinceStartupAsDouble;
            // Suspend decisions immediately, but do not chop a qualified sentence
            // for a brief blink. Sustained loss still stops speech.
            if (Time.realtimeSinceStartupAsDouble - m_InvalidGazeSince >= GuidanceSpeechPolicy.TrackingGrace)
            { InterruptGuidance(); m_CurrentHint = null; }
            m_Candidate = null;
            m_LastDirection = null;
            return; // Tracking loss never invents head-gaze guidance or a wall exit.
        }
        m_InvalidGazeSince = -1;
        m_LastSuspension = null;
        int verticalBand = DirectionalHintPolicy.VerticalBand(m_VerticalFraction);
        if (desired != m_Candidate || (DirectionalHintPolicy.HasVertical(desired) && verticalBand != m_CandidateVerticalBand))
        { m_Candidate = desired; m_CandidateVerticalBand = verticalBand; m_CandidateSince = Time.timeAsDouble; }
        bool stable = Time.timeAsDouble - m_CandidateSince >= k_DirectionSettleSeconds;
        // Ordinary cues finish. Qualified corrections can request bounded parallel
        // Jev advice; approval still needs fresh evidence before and after the fade.
        // Exact right-controller hit + nearby valid gaze outranks search directions.
        // Recheck after the short audio fade; no announcement can survive a lost hit.
        if ((m_CurrentHint == DirectionalHintPolicy.LookingPhrase ||
            m_CurrentHint == DirectionalHintPolicy.SelectionReminderPhrase) && m_Voice.IsBusy) return;
        if (pointing && pointingEntry)
        {
            string phrase = DirectionalHintPolicy.PointingPhrase;
            var decision = Decision("requested", "controller_target_gaze_vicinity", phrase, phrase, true, inside, far, true);
            bool currentInside = inside;
            if (m_Voice.TryAreaCorrection(phrase,
                () => { Evaluate(out bool v, out currentInside, out _); return Active() && EvaluatePointing(v) && m_PointingEntry.InsideStable; },
                () => {
                    Record(Decision("committed", "pointing_revalidated", phrase, phrase,
                        true, currentInside, false, true, decision.decisionId));
                    m_PointingEntry.MarkSpoken(Time.timeAsDouble);
                    m_TargetAcknowledged = true;
                    if (currentInside) m_ZoneEntry.MarkSpoken(Time.timeAsDouble);
                    m_TargetHistory.Clear(); m_ReturnCue.MarkSpoken(Time.timeAsDouble);
                    m_LookingEntry.MarkSpoken(Time.timeAsDouble); m_LastTipTime = Time.time;
                    m_LastDirection = null;
                }, "controller_target_gaze_vicinity", decision.decisionId))
            { Record(decision); m_CurrentHint = phrase; }
            return; // Do not keep saying look up/down while the person is pointing at it.
        }
        if (m_CurrentHint == DirectionalHintPolicy.PointingPhrase && m_Voice.IsBusy) return;
        if (looking)
        {
            if (lookingEntry)
            {
                string category = DirectionalHintPolicy.LookingPhrase;
                string phrase = DirectionalHintPolicy.Variant(category, m_LookingVariant);
                var decision = Decision("requested", "gaze_target_vicinity_hold", category, phrase, true, inside, far, true);
                if (m_Voice.TryAreaCorrection(phrase,
                    () => { Evaluate(out bool v, out _, out _); EvaluatePointing(v); return Active() && EvaluateLooking(v) && m_LookingEntry.InsideStable; },
                    () => {
                        Record(Decision("committed", "looking_revalidated", category, phrase, true, inside, far, true, decision.decisionId));
                        m_LookingEntry.MarkSpoken(Time.timeAsDouble);
                        m_TargetAcknowledged = true;
                        // One acknowledgment is enough when eyes and controller
                        // already agree; do not follow it with a redundant "on it".
                        if (m_ControllerOnTarget) m_PointingEntry.MarkSpoken(Time.timeAsDouble);
                        m_LookingVariant++;
                        if (inside) m_ZoneEntry.MarkSpoken(Time.timeAsDouble);
                        m_TargetHistory.Clear(); m_ReturnCue.MarkSpoken(Time.timeAsDouble);
                        m_LastTipTime = Time.time; m_LastDirection = null;
                    }, "gaze_target_vicinity_hold", decision.decisionId))
                { Record(decision); m_CurrentHint = category; }
            }
            else if (selectionReminderDue && !m_Voice.IsBusy)
            {
                string phrase = DirectionalHintPolicy.SelectionReminderPhrase;
                var decision = Decision("requested", "sustained_target_selection_reminder", phrase, phrase, true, inside, far, true);
                if (m_Voice.TryAreaCorrection(phrase,
                    () => { Evaluate(out bool v, out _, out _); EvaluatePointing(v); return Active() && EvaluateLooking(v); },
                    () => {
                        m_SelectionReminder.MarkSpoken();
                        Record(Decision("committed", "selection_reminder_revalidated", phrase, phrase,
                            true, inside, far, true, decision.decisionId));
                        m_LastTipTime = Time.time;
                    }, "sustained_target_selection_reminder", decision.decisionId))
                { Record(decision); m_CurrentHint = phrase; }
            }
            return; // Let the person hold gaze within the target region without nudging it away.
        }
        // Remember a qualified target visit, even though the narrow crossing
        // history is cleared by confirmation. A blink cannot manufacture a departure.
        if (revisit && desired != null && desired != DirectionalHintPolicy.AlignedPhrase && CanCorrect(stable))
        {
            string returnDirection = desired;
            string phrase = DirectionalHintPolicy.ReturnToTarget(returnDirection);
            var decision = Decision("requested", "recent_target_departure", returnDirection, phrase, true, inside, far, stable);
            if (m_Voice.TryAreaCorrection(phrase,
                () => {
                    var d = Evaluate(out bool v, out _, out _); EvaluatePointing(v);
                    return Active() && v && !EvaluateLooking(v) && d == returnDirection && m_TargetRevisit.IsPending(Time.timeAsDouble);
                },
                () => {
                    Record(Decision("committed", "recent_target_departure_revalidated", returnDirection, phrase, true, inside, far, true, decision.decisionId));
                    m_TargetRevisit.MarkSpoken(Time.timeAsDouble); m_TargetHistory.Clear(); m_ReturnCue.MarkSpoken(Time.timeAsDouble);
                    m_LastTipTime = Time.time; RememberDirection(returnDirection);
                }, "recent_target_departure", decision.decisionId))
            { Record(decision); m_CurrentHint = k_RevisitCategory; return; }
        }
        // Object-bearing overshoot has priority over the older wall-exit cue.
        // Its frozen history is attached to both request and committed records.
        string passedDirection = m_TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble);
        if (passedDirection != null && m_CurrentHint == passedDirection)
        {
            // We are already giving the required correction: do not cut "Look
            // down" off just to replace it with "Go back down a little."
            Record(Decision("suppressed", "same_direction_already_given", passedDirection, "", true, inside, far, stable));
            m_TargetHistory.MarkSpoken(Time.realtimeSinceStartupAsDouble);
            m_ReturnCue.MarkSpoken(Time.timeAsDouble);
            passedDirection = null;
        }
        if (passedDirection != null && CanCorrect(stable))
        {
            string phrase = DirectionalHintPolicy.PassedTarget(passedDirection, m_VerticalFraction);
            var evidence = m_TargetHistory.Snapshot();
            var decision = Decision("requested", "target_bearing_overshoot", passedDirection, phrase, true, inside, far, stable);
            decision.targetHistory = evidence;
            bool correctionInside = inside, correctionFar = far;
            if (m_Voice.TryAreaCorrection(phrase,
                () => {
                    Evaluate(out bool v, out correctionInside, out correctionFar);
                    UpdateTargetHistory(v);
                    return Active() && v && m_TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble) == passedDirection &&
                        DirectionalHintPolicy.PassedTarget(passedDirection, m_VerticalFraction) == phrase;
                },
                () => {
                    var committed = Decision("committed", "target_bearing_overshoot_revalidated", passedDirection, phrase,
                        true, correctionInside, correctionFar, true, decision.decisionId);
                    committed.targetHistory = evidence; Record(committed);
                    m_TargetHistory.MarkSpoken(Time.realtimeSinceStartupAsDouble);
                    // Avoid an immediate redundant "go back to that area" afterwards.
                    m_ReturnCue.MarkSpoken(Time.timeAsDouble);
                    if (correctionInside) m_ZoneEntry.MarkSpoken(Time.timeAsDouble);
                    m_LastTipTime = Time.time;
                    RememberDirection(passedDirection);
                }, "target_bearing_overshoot", decision.decisionId))
            { Record(decision); m_CurrentHint = k_TargetReturnCategory; return; }
        }
        if (entry && CanCorrect(stable))
        {
            string entryDirection = desired ?? DirectionalHintPolicy.AlignedPhrase;
            string phrase = DirectionalHintPolicy.AreaEntry(entryDirection,m_AreaVariant, m_VerticalFraction);
            var decision = Decision("requested", "stable_wall_entry", AreaCorrectionPhrase, phrase, true, inside, far, stable);
            if (m_Voice.TryAreaCorrection(phrase,
                () => { var d = Evaluate(out bool v,out bool i,out _); return Active() && v && i && d == entryDirection &&
                    DirectionalHintPolicy.AreaEntry(entryDirection, m_AreaVariant, m_VerticalFraction) == phrase; },
                () => {
                    Record(Decision("committed", "wall_entry_revalidated", AreaCorrectionPhrase, phrase,
                        true, true, false, Time.timeAsDouble-m_CandidateSince>=k_DirectionSettleSeconds, decision.decisionId));
                    m_ZoneEntry.MarkSpoken(Time.timeAsDouble); m_AreaVariant++; m_LastTipTime=Time.time;
                    RememberDirection(entryDirection);
                }, guidanceDecisionId: decision.decisionId))
            { Record(decision); m_CurrentHint = AreaCorrectionPhrase; return; }
        }
        if (returning) m_ReturnDirection = desired;
        if (returning && desired != null && CanCorrect(stable))
        {
            string phrase = DirectionalHintPolicy.ReturnToArea(desired);
            var decision = Decision("requested", "stable_wall_exit", DirectionalHintPolicy.ReturnPhrase, phrase, true, inside, far, stable);
            bool revalidatedFar = far;
            if (m_Voice.TryAreaCorrection(phrase,
                () => { var d=Evaluate(out bool v,out bool i,out revalidatedFar); return Active() && v && !i && d==m_ReturnDirection && m_ReturnCue.IsPending(Time.timeAsDouble); },
                () => {
                    Record(Decision("committed", "wall_exit_revalidated", DirectionalHintPolicy.ReturnPhrase, phrase,
                        true, false, revalidatedFar, Time.timeAsDouble-m_CandidateSince>=k_DirectionSettleSeconds, decision.decisionId));
                    m_ReturnCue.MarkSpoken(Time.timeAsDouble); m_ReturnVariant++; m_LastTipTime=Time.time;
                    m_LastDirection = null;
                },"gaze_overshoot_return",decision.decisionId))
            { Record(decision); m_ReturnDirection=desired; m_CurrentHint=DirectionalHintPolicy.ReturnPhrase; return; }
        }
        if (!stable || desired == null || m_ReturnCue.IsDebouncingExit || (inside && !m_ZoneEntry.InsideStable) ||
            !m_Voice.GuidanceReady || Time.time-m_LastTipTime<k_TipInterval) return;
        bool progress = !inside && m_Turn.ProgressDue(desired, m_TurnRemaining);
        // While turning, earn encouragement at new 45-degree milestones. If
        // stalled, Jev decides whether another explicit cue is useful; offline fallback waits three seconds.
        if (!m_Voice.UsesTransmissionAdvice && !inside && desired == m_LastDirection && !progress && Time.time - m_LastTipTime < 3) return;
        if (m_Navigation == null) m_Navigation = GetComponent<SpatialVoice.StudyNavigationVoiceBridge>();
        if (m_Navigation != null && m_Navigation.IsSelecting) return;
        // A catalog wait also suppresses the ordinary fallback during its short cooldown.
        if (!inside && !progress && !m_Voice.CanRequestGuidance("catalog_turn")) return;
        // Only ordinary coarse directions use the catalog. Milestones, return cues,
        // target acknowledgment and vertical refinement still require their stateful evidence.
        if (!inside && !progress && m_EvaluatedTarget != null && m_Navigation != null &&
            m_Navigation.TryRequest(m_EvaluatedTarget.transform, desired, selection =>
            {
                var phrase = selection.Phrase;
                var current = Evaluate(out bool stillValid, out bool nowInside, out bool nowFar);
                if (!Active() || !stillValid || nowInside || current != desired ||
                    Time.timeAsDouble - m_CandidateSince < k_DirectionSettleSeconds) return false;
                var selected = Decision("requested", "catalog_turn:" + phrase.id, desired, phrase.text, true, false, nowFar, true);
                selected.decisionId = selection.RequestId ?? selected.decisionId;
                var target = m_EvaluatedTarget.transform;
                bool accepted = m_Voice.TryGuidanceTransmission(phrase.text,
                    () => OrdinaryCueRelevant(desired, false) && m_Navigation.IsPhraseCurrent(target, phrase),
                    () => {
                        m_CurrentHint = desired; m_LastTipTime = Time.time;
                        RememberDirection(desired); m_Turn.MarkSpoken(desired, m_TurnRemaining);
                        m_Navigation.MarkSpoken(phrase);
                        Record(Decision("committed", "catalog_transmission_approved", desired, phrase.text, true, false, nowFar, true, selected.decisionId));
                    }, "catalog_turn", selected.decisionId, selection.Source == "local" ? (bool?)null : selection.Speak, selection.Source);
                if (accepted) Record(selected);
                return accepted;
            })) return;
        string wording = !inside ? (progress ? DirectionalHintPolicy.Progress(desired, m_ProgressVariant) :
            DirectionalHintPolicy.Turn(desired, m_TurnRemaining, m_Variant)) : DirectionalHintPolicy.FollowUp(desired, m_LastDirection,
            DirectionalHintPolicy.DirectionError(desired, m_YawError, m_PitchError), m_LastDirectionError, m_Variant, far,
            m_ContinuationUsed || (DirectionalHintPolicy.HasVertical(desired) && verticalBand != m_LastVerticalBand), m_VerticalFraction);
        string ordinaryReason = progress ? "turn_progress_milestone" : "stable_direction";
        var request = Decision("requested", ordinaryReason, desired, wording, true, inside, far, true);
        if (m_Voice.TryGuidanceTransmission(wording,
            () => OrdinaryCueRelevant(desired, inside) &&
                (!DirectionalHintPolicy.HasVertical(desired) || verticalBand == DirectionalHintPolicy.VerticalBand(m_VerticalFraction)),
            () => {
                m_CurrentHint = desired; m_LastTipTime = Time.time;
                if (progress) m_ProgressVariant++; else m_Variant++;
                RememberDirection(desired);
                if (!inside) m_Turn.MarkSpoken(desired, m_TurnRemaining);
                if (DirectionalHintPolicy.IsContinuation(wording)) m_ContinuationUsed = true;
                Record(Decision("committed", "transmission_approved:" + ordinaryReason, desired, wording, true, inside, far, true, request.decisionId));
            }, ordinaryReason, request.decisionId)) Record(request);
    }
    bool OrdinaryCueRelevant(string direction, bool wasInside)
    {
        string current = Evaluate(out bool valid, out bool inside, out _);
        return Active() && valid && current == direction && inside == wasInside &&
            Time.timeAsDouble - m_CandidateSince >= k_DirectionSettleSeconds &&
            !EvaluateLooking(valid) && !EvaluatePointing(valid) &&
            m_TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble) == null &&
            !m_TargetRevisit.IsPending(Time.timeAsDouble);
    }
    // Recompute the latest cue after the sentence and its audible pause finish.
    bool CanCorrect(bool stable) => stable && (m_Voice.GuidanceReady || m_Voice.CanConsiderGuidanceInterruption);
    void RememberDirection(string direction)
    {
        if (direction != m_LastDirection) m_ContinuationUsed = false;
        m_LastDirection = direction;
        m_LastVerticalBand = DirectionalHintPolicy.VerticalBand(m_VerticalFraction);
        m_LastDirectionError = DirectionalHintPolicy.DirectionError(direction, m_YawError, m_PitchError);
    }
    void InterruptGuidance(bool immediate = false)
    {
        m_Voice?.InterruptIfAbout("tip", immediate);
        m_Voice?.InterruptIfAbout("guidance_repeat", immediate);
    }
    public void RequestRepeat()
    {
        string desired = Evaluate(out bool valid, out bool inside, out bool far);
        if (EvaluatePointing(valid) && m_PointingEntry.InsideStable) desired = DirectionalHintPolicy.PointingPhrase;
        else if (EvaluateLooking(valid) && m_LookingEntry.InsideStable) desired = DirectionalHintPolicy.LookingPhrase;
        bool available = Active() && valid && desired != null && m_Voice != null && m_Voice.GuidanceReady;
        string phrase = available ? DirectionalHintPolicy.Variant(desired,m_Variant++,false,far, m_VerticalFraction) : "";
        var decision = Decision(available ? "requested" : "unavailable", "controller_repeat_request",
            desired, phrase, valid, inside, far, false);
        Record(decision);
        m_Voice?.RecordGuidanceRepeatRequest(available, decision.decisionId);
        if (!available) return;
        // Repeat the current direction, not an obsolete phrase or hidden target identity.
        m_CurrentHint = desired; m_LastTipTime = Time.time;
        RememberDirection(desired);
        m_Voice.Speak(phrase,"guidance_repeat",guidanceDecisionId:decision.decisionId);
    }
    GuidanceDecision Decision(string stage, string reason, string category, string phrase,
        bool valid, bool inside, bool far, bool stable, string id = null)
    {
        var decision = GuidanceDecision.Capture(id ?? System.Guid.NewGuid().ToString("N"), stage, reason,
            category ?? "none", phrase, m_EvaluatedSample, Time.realtimeSinceStartupAsDouble,
            valid, inside, far, stable);
        decision.targetGeometryAvailable = valid && m_GeometryAvailable;
        decision.turnRemainingDegrees = valid && !inside ? m_TurnRemaining : 0;
        decision.recentTargetVisitReceiptTime = m_LastTargetVisitTime;
        decision.recentTargetVisitObservationSequence = m_LastTargetVisitSequence;
        if (decision.targetGeometryAvailable) { decision.targetYawError = m_YawError; decision.targetPitchError = m_PitchError; }
        decision.verticalGuidanceMeasured = valid && m_VerticalMeasured;
        if (decision.verticalGuidanceMeasured)
        {
            decision.verticalErrorMetres = m_VerticalError;
            decision.guidancePlaneHeightMetres = m_GameManager.SearchPlaneHeight;
            decision.verticalErrorPlaneFraction = m_VerticalFraction;
        }
        decision.gazeOnTarget = m_GazeOnTarget;
        decision.targetVicinityMeasured = m_VicinityMeasured;
        decision.targetVicinityErrorDegrees = m_VicinityError; decision.targetVicinityRadiusDegrees = m_VicinityRadius;
        decision.gazeVicinityWinnerObjectId = m_ClosestVicinityObject != null
            ? m_ClosestVicinityObject.GetComponent<SpawnableObjectInfo>()?.objectId ?? "" : "";
        decision.gazeVicinityCandidateCount = m_VicinityCandidateCount;
        decision.gazeVicinityAmbiguous = m_VicinityAmbiguous;
        decision.gazeVicinityClosestErrorDegrees = m_ClosestVicinityError;
        decision.targetVicinityPaddingDegrees = m_Vicinity != null ? m_Vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees;
        decision.devVicinityRingEnabled = m_Vicinity != null && m_Vicinity.RingVisible;
        decision.controllerEvidenceValid = m_ControllerEvidenceValid; decision.controllerOnTarget = m_ControllerOnTarget;
        decision.targetObjectId = m_EvaluatedTarget != null ? m_EvaluatedTarget.objectId : "";
        if (m_Controller != null)
        {
            decision.controllerTelemetryFrame = m_Controller.TelemetryFrame;
            decision.controllerTelemetryTime = m_Controller.TelemetryTime;
            var controllerTarget = m_Controller.RightTelemetry.target;
            decision.controllerTargetObjectId = controllerTarget != null ? controllerTarget.GetComponent<SpawnableObjectInfo>()?.objectId ?? "" : "";
        }
        return decision;
    }
    // Retain exact-hit evidence separately. Spoken confirmation uses the padded
    // region measured by EvaluatePointing, only when the target uniquely wins
    // against all overlapping object rings, including when the dev ring is hidden.
    void UpdateSpeechEvidence(bool valid, bool inside, bool looking)
    {
        var e = m_Voice.GuidanceEvidence;
        double now = Time.realtimeSinceStartupAsDouble;
        e.snapshotTimeSeconds = now; e.phase = "search"; e.valid = valid; e.insideTargetWall = valid && inside;
        e.wallEntryQualified = valid && inside && m_ZoneEntry.InsideStable;
        e.gazeWithinTargetVicinity = valid && looking;
        e.targetGazeQualified = valid && looking && m_LookingEntry.InsideStable;
        e.gazeRayHit = valid ? m_GazeRayHit : "unavailable";
        e.controllerEvidenceValid = m_ControllerEvidenceValid;
        e.controllerOnTarget = m_ControllerEvidenceValid && m_ControllerOnTarget;
        e.controllerTargetHoldQualified = e.controllerOnTarget && m_PointingEntry.InsideStable;
        var hit = m_Controller != null ? m_Controller.RightTelemetry.target : null;
        e.controllerRayHit = !m_ControllerEvidenceValid ? "unavailable" : e.controllerOnTarget ? "target" :
            hit == null ? "none" : hit.GetComponentInParent<SpawnableObjectInfo>() != null ? "distractor" : "non_object";
        e.controllerTriggerHeld = m_ControllerEvidenceValid ? m_Controller.RightTelemetry.triggerHeld : -1;
        e.controllerAgeSeconds = m_Controller == null || m_Controller.TelemetryFrame < 0 ? -1 : System.Math.Max(0, now - m_Controller.TelemetryTime);
        e.gazeAgeSeconds = m_EvaluatedSample == null ? -1 : System.Math.Max(0, now - m_EvaluatedSample.inputEventTime);
        e.gazeReceiptAgeSeconds = m_EvaluatedSample == null ? -1 : System.Math.Max(0, now - m_EvaluatedSample.receiptTime);
        e.geometryAvailable = valid && m_GeometryAvailable;
        e.yawErrorDegrees = m_YawError; e.pitchErrorDegrees = m_PitchError;
        e.verticalMeasured = valid && m_VerticalMeasured; e.verticalPlaneFraction = m_VerticalFraction;
        e.vicinityMeasured = valid && m_VicinityMeasured;
        e.vicinityErrorDegrees = m_VicinityError; e.vicinityRadiusDegrees = m_VicinityRadius;
        e.vicinityCandidateCount = m_VicinityCandidateCount; e.vicinityAmbiguous = m_VicinityAmbiguous;
        e.targetAcknowledged = m_TargetAcknowledged;
        e.targetReturnPending = valid && m_TargetRevisit.IsPending(Time.timeAsDouble);
        e.lastTargetSeenAgeSeconds = m_LastTargetVisitTime < 0 ? -1 : System.Math.Max(0, now - m_LastTargetVisitTime);
        e.previousDirection = m_LastDirection; e.continuationUsed = m_ContinuationUsed;
    }

    bool EvaluateLooking(bool valid)
    {
        m_GazeOnTarget = false; m_GazeRayHit = "unavailable";
        if (valid && m_EvaluatedTarget != null && m_EvaluatedSample != null)
        {
            m_GazeRayHit = "none";
            if (Physics.Raycast(m_EvaluatedSample.worldOrigin, m_EvaluatedSample.worldDirection,
                out var hit, 30, 1 << 8, QueryTriggerInteraction.Ignore))
            {
                var info = hit.collider.GetComponentInParent<SpawnableObjectInfo>();
                m_GazeOnTarget = info == m_EvaluatedTarget;
                m_GazeRayHit = m_GazeOnTarget ? "target" : info != null ? "distractor" : "non_object";
            }
        }
        return valid && m_EvaluatedTarget != null && m_ClosestVicinityObject == m_EvaluatedTarget.gameObject;
    }

    public bool TryPrepareOpening(out string phrase, out string decisionId)
    {
        phrase = decisionId = null;
        string coarse = Evaluate(out bool valid, out bool inside, out bool far);
        if (!valid) return false;
        // Same-wall starts can refine height and lateral position before reveal.
        // Use fresh thresholds, without carrying the previous trial's hysteresis.
        string direction = inside ? DirectionalHintPolicy.WithinArea(m_YawError, m_PitchError) :
            coarse ?? DirectionalHintPolicy.AlignedPhrase;
        phrase = DirectionalHintPolicy.Opening(direction, inside, m_VerticalFraction);
        m_OpeningTurn = !inside ? direction : null;
        var decision = Decision("requested", "presearch_target_direction", direction, phrase, true, inside, far, true);
        decisionId = decision.decisionId; Record(decision);
        return true;
    }

    bool EvaluatePointing(bool valid)
    {
        m_VicinityMeasured = m_ControllerEvidenceValid = m_ControllerOnTarget = false;
        m_VicinityError = m_VicinityRadius = 0;
        m_ClosestVicinityObject = null; m_VicinityCandidateCount = 0;
        m_VicinityAmbiguous = false; m_ClosestVicinityError = 0;
        if (m_EvaluatedTarget != null)
        {
            if (m_Controller == null) m_Controller = GetComponent<ControllerRaySelector>();
            if (m_Controller != null)
            {
                m_ControllerEvidenceValid = GazeTargetVicinity.ControllerCurrent(m_Controller.RightTelemetry,
                    m_Controller.TelemetryFrame, m_Controller.TelemetryTime, Time.frameCount, Time.realtimeSinceStartupAsDouble);
                m_ControllerOnTarget = m_ControllerEvidenceValid && m_Controller.RightTelemetry.target == m_EvaluatedTarget.gameObject;
            }
        }
        if (!valid || m_EvaluatedTarget == null || m_EvaluatedSample == null)
        { m_Vicinity?.Hide(); return false; }
        var collider = m_EvaluatedTarget.GetComponent<Collider>();
        if (collider == null || !collider.enabled) { m_Vicinity?.Hide(); return false; }
        m_VicinityMeasured = GazeTargetVicinity.Measure(m_EvaluatedSample.worldOrigin,
            m_EvaluatedSample.worldDirection, collider.bounds,
            m_Vicinity != null ? m_Vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees,
            out m_VicinityError, out m_VicinityRadius);
        if (!m_VicinityMeasured) { m_Vicinity?.Hide(); return false; }
        bool nearby = m_VicinityError <= m_VicinityRadius;
        if (nearby)
            m_ClosestVicinityObject = GazeTargetVicinity.ClosestObject(m_EvaluatedSample.worldOrigin,
                m_EvaluatedSample.worldDirection, m_GameManager?.SpawnedObjects,
                m_Vicinity != null ? m_Vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees,
                out m_VicinityCandidateCount, out m_VicinityAmbiguous, out m_ClosestVicinityError);
        bool targetClosest = m_ClosestVicinityObject == m_EvaluatedTarget.gameObject;
        m_Vicinity?.Draw(m_EvaluatedSample.worldOrigin, collider.bounds, m_VicinityRadius, targetClosest);
        return m_ControllerOnTarget && targetClosest;
    }

    void PreviewPausedVicinity()
    {
        if (!SessionConfig.NeutralQuickTest || m_GameManager == null || !m_GameManager.ResearcherPaused ||
            m_Vicinity == null || !m_Vicinity.RingVisible)
        { m_Vicinity?.Hide(); return; }
        // Development-only visual feedback while adjusting settings; no gate/history/speech update.
        Evaluate(out bool valid, out _, out _);
        EvaluatePointing(valid);
    }

    public void SetVicinityRing(bool visible)
    {
        if (m_Vicinity == null || !SessionConfig.NeutralQuickTest) return;
        m_Vicinity.SetRingVisible(visible);
        Record(Decision("configuration", "admin_vicinity_ring", "target_vicinity", "", false, false, false, false));
    }
    public void AdjustVicinityPadding(float delta)
    {
        if (m_Vicinity == null || !SessionConfig.NeutralQuickTest) return;
        m_Vicinity.SetPadding(m_Vicinity.PaddingDegrees + delta);
        Record(Decision("configuration", "admin_vicinity_radius", "target_vicinity", "", false, false, false, false));
    }
    void UpdateTargetHistory(bool valid)
    {
        if (!valid || !m_GeometryAvailable || m_EvaluatedSample == null) { m_TargetHistory.Clear(); return; }
        m_TargetHistory.Observe(true, new GazeTargetHistory.Sample {
            time = m_EvaluatedSample.receiptTime, inputTime = m_EvaluatedSample.inputEventTime,
            observationSequence = m_EvaluatedSample.sequence, eyeRecordingId = m_EvaluatedSample.recordingId,
            deviceId = m_EvaluatedSample.deviceId,
            yawError = m_YawError, pitchError = m_PitchError
        });
    }
    void Record(GuidanceDecision decision) => m_Voice?.RecordGuidanceDecision(JsonUtility.ToJson(decision));
    bool Active() => !m_TipsSuppressed && m_GameManager != null && m_GameManager.SearchActive;
    string Evaluate(out bool valid,out bool inside,out bool far)
    {
        valid=inside=far=false; m_GazeOnTarget = false; m_GeometryAvailable = false; m_EvaluatedTarget = null;
        m_VerticalMeasured = false; m_VerticalError = 0; m_VerticalFraction = float.NaN;
        m_ClosestVicinityObject = null; m_VicinityCandidateCount = 0;
        m_VicinityAmbiguous = false; m_ClosestVicinityError = 0;
        m_VicinityMeasured = m_ControllerEvidenceValid = m_ControllerOnTarget = false;
        m_VicinityError = m_VicinityRadius = 0;
        m_EvaluatedSample = SharedGazeStream.Instance != null ? SharedGazeStream.Instance.Latest : null;
        if (m_GameManager == null || !m_GameManager.RotationalBetaEnabled || !TryGetTargetInfo(out var target)) return null;
        m_EvaluatedTarget = target;
        if (SharedGazeStream.Instance == null || !SharedGazeStream.Instance.TryGetValid(out var sample)) return null;
        m_EvaluatedSample = sample;
        Vector3 gaze=sample.worldDirection, delta=target.transform.position-sample.worldOrigin;
        // Azimuth becomes unstable looking nearly straight up/down. This is a gap, not wall departure.
        if (gaze.x*gaze.x+gaze.z*gaze.z<.04f || delta.sqrMagnitude<.001f) return null;
        inside=m_GameManager.IsGazeInSearchSector(sample.worldOrigin,gaze,target.planeId,out valid);
        if (!valid) return null;
        float gazeElevation=Mathf.Atan2(gaze.y,new Vector2(gaze.x,gaze.z).magnitude)*Mathf.Rad2Deg;
        float targetElevation=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
        // Within the correct wall, combine elevation and horizontal error for diagonal refinement.
        // Measure the tie relative to the seated layout, independent of room yaw.
        Quaternion inverse = Quaternion.Euler(0, -m_GameManager.SeatedForwardYaw, 0);
        Vector3 localGaze = inverse * gaze, localDelta = inverse * delta;
        float yaw=DirectionalHintPolicy.SignedYaw(localGaze.x,localGaze.z,localDelta.x,localDelta.z);
        m_YawError = yaw; m_PitchError = targetElevation - gazeElevation; m_GeometryAvailable = true;
        string turn = m_Turn.Resolve(yaw, inside);
        m_TurnRemaining = m_Turn.Remaining(yaw);
        if (!inside) { far = m_TurnRemaining > 45; return turn; }
        m_VerticalMeasured = m_GameManager.TryVerticalGuidanceError(sample.worldOrigin, gaze, target.planeId,
            target.transform.position.y, out m_VerticalError);
        if (!m_VerticalMeasured) { valid = false; return null; }
        m_VerticalFraction = m_VerticalError / m_GameManager.SearchPlaneHeight;
        return DirectionalHintPolicy.WithinArea(m_YawError, m_PitchError, m_Candidate);
    }
    bool TryGetTargetInfo(out SpawnableObjectInfo info)
    {
        info=null;
        var target=m_GameManager.CurrentTarget;
        foreach(var obj in m_GameManager.SpawnedObjects)
        {
            if(obj==null || !obj.activeSelf) continue;
            var candidate=obj.GetComponent<SpawnableObjectInfo>();
            if(candidate!=null && candidate.shapeName==target.shape && candidate.colorName==target.color)
            { info=candidate; return true; }
        }
        return false;
    }
    public static string[] AllPhrases() => DirectionalHintPolicy.AllPhrases();
}
