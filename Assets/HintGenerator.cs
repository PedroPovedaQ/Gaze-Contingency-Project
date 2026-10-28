using UnityEngine;

/// <summary>Shared valid gaze drives coarse wall guidance, then two-axis target refinement.</summary>
[DefaultExecutionOrder(350)] // After ControllerRaySelector's current-frame telemetry.
public partial class HintGenerator : MonoBehaviour
{
    public const string AreaCorrectionPhrase = DirectionalHintPolicy.AreaPhrase;
    const string k_TargetReturnCategory = "target_passage_return";
    const string k_RevisitCategory = "recent_target_return";
    const float k_TipInterval = GuidanceSpeechPolicy.HintIntervalSeconds; // Same pilot cadence in both voice conditions.
    readonly GuidanceStateMachine m_State = new GuidanceStateMachine();
    public GuidanceStateSnapshot CurrentStateSnapshot { get; private set; }
    long m_PublishedRevision = -1;
    int m_ProgressVariant;
    float m_TurnRemaining;
    string m_OpeningTurn;
    string m_OpeningHistoryIdentity;
    string HistoryParticipant => SessionConfig.ParticipantId + "|" + SessionConfig.CurrentRunFolder;
    int HistoryTrial => m_GameManager != null ? m_GameManager.CurrentObjectiveIndex : 0;
    string HistoryIdentity => HistoryParticipant + "|" + HistoryTrial;
    double m_InvalidGazeSince = -1;
    float m_YawError, m_PitchError;
    float m_VerticalError, m_VerticalFraction = float.NaN;
    bool m_VerticalMeasured;
    int m_LastVerticalBand;
    bool m_GeometryAvailable, m_GazeOnTarget;
    string m_GazeRayHit = "unavailable";
    string m_ReturnDirection, m_CurrentHint;
    string m_GazeHitObjectId;
    string m_LastSuspension;
    GazeObservation m_EvaluatedSample;
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
    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null)
    {
        if (m_Voice != null) m_Voice.PhrasePlaybackStarted -= OnPhrasePlaybackStarted;
        m_Voice = voice;
        if (m_Voice != null) m_Voice.PhrasePlaybackStarted += OnPhrasePlaybackStarted;
    }
    void OnPhrasePlaybackStarted(string phrase, string spokenText, string context) =>
        m_AdaptiveHistory.RecordPhrase(phrase, spokenText, context, Time.realtimeSinceStartupAsDouble);
    void OnDestroy() { if (m_Voice != null) m_Voice.PhrasePlaybackStarted -= OnPhrasePlaybackStarted; }
    void Start()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
        m_Navigation = GetComponent<SpatialVoice.StudyNavigationVoiceBridge>();
        m_Vicinity = GetComponent<GazeTargetVicinity>() ?? gameObject.AddComponent<GazeTargetVicinity>();
    }
    public void OnNewObjective(bool resumed = false)
    {
        m_Voice?.ClearGuidanceEvidence();
        m_AdaptiveHistory.BeginTrial(HistoryParticipant, HistoryTrial,
            Time.realtimeSinceStartupAsDouble, resumed || m_OpeningHistoryIdentity == HistoryIdentity);
        m_OpeningHistoryIdentity = null;
        m_AdaptiveSchedule.Reset(); m_ActionPolicyEpoch++;
        m_WallArrivalDecision=null; m_WallArrivalRelevant=null; m_NextWallArrivalRequest=0;
        if (resumed) m_State.ResumeSearch(Time.realtimeSinceStartupAsDouble);
        else m_State.BeginSearch(Time.realtimeSinceStartupAsDouble, m_OpeningTurn);
        m_OpeningTurn = null; m_ProgressVariant = 0;
        m_InvalidGazeSince = -1;
        m_CurrentHint = m_ReturnDirection = null;
        m_LastDirection = null; m_ContinuationUsed = false;
        m_LastSuspension = null;
        m_Variant = m_AreaVariant = m_ReturnVariant = m_LookingVariant =
            (ChallengeSet.ScheduleSeed % 8 + (m_GameManager != null ? m_GameManager.CurrentObjectiveIndex : 0)) % 8;
        m_LastTipTime = Time.time; m_TipsSuppressed = false;
        PublishState(false);
    }
    public void OnWrongCapture() { m_AdaptiveHistory.WrongSelection(); }
    public void CancelPending()
    {
        m_Voice?.ClearGuidanceEvidence();
        m_Navigation?.Cancel();
        m_OpeningTurn = null; m_TipsSuppressed = true; m_AdaptiveHistory.BreakObservation();
        // Correct capture/pause/stop already publish their distinct lifecycle.
        if (m_State.Phase != GuidancePhase.Completed && m_State.Phase != GuidancePhase.Paused && m_State.Phase != GuidancePhase.Stopped && m_State.Phase != GuidancePhase.TimedOut)
            SetLifecycle(GuidancePhase.Inactive, "guidance_cancelled");
        m_Vicinity?.Hide();
        m_CurrentHint = m_ReturnDirection = null; InterruptGuidance(true);
        m_LastDirection = null;
        PublishState(false);
    }
    void LateUpdate()
    {
        if (m_Voice == null || m_GameManager == null || m_TipsSuppressed) { PreviewPausedVicinity(); return; }
        if (!m_GameManager.SearchActive || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
        { InterruptGuidance(true); PreviewPausedVicinity(); m_CurrentHint = null; return; }
        string desired = Evaluate(out bool valid, out bool inside, out bool far, advanceTurn: true);
        EvaluatePointing(valid);
        bool looking = EvaluateLooking(valid);
        m_State.Step(new GuidanceStateMachine.Input {
            now = Time.realtimeSinceStartupAsDouble, deltaSeconds = Time.unscaledDeltaTime,
            receiptTime = m_EvaluatedSample?.receiptTime ?? -1, observationSequence = m_EvaluatedSample?.sequence ?? -1,
            valid = valid, inside = inside, looking = looking, controllerValid = m_ControllerEvidenceValid,
            controllerOnTarget = m_ControllerOnTarget, suppressVertical = m_VisualReferenceVerticalSuppressed, direction = desired, verticalFraction = m_VerticalFraction,
            remainingTurn = m_TurnRemaining, history = TargetHistorySample(valid)
        });
        PublishState(valid);
        long revision = m_State.Revision;
        bool Allows(GuidanceAction action)
        {
            // Async completion can precede LateUpdate: refresh controller telemetry
            // for every final relevance check, including return and wall-entry cues.
            RefreshControllerEvidence();
            return m_State.AllowsPlayback(revision, action, m_ControllerEvidenceValid, m_ControllerOnTarget);
        }
        UpdateSpeechEvidence(valid, inside, looking);
        m_AdaptiveHistory.Observe(Time.realtimeSinceStartupAsDouble, valid,
            System.Math.Sqrt(m_YawError*m_YawError + m_PitchError*m_PitchError), desired);
        if (m_ControllerEvidenceValid && m_ControllerOnTarget)
            m_Voice.SuppressSearchGuidanceForControllerTarget();
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
            m_LastDirection = null;
            return; // Tracking loss never invents head-gaze guidance or a wall exit.
        }
        m_InvalidGazeSince = -1;
        m_LastSuspension = null;
        int verticalBand = DirectionalHintPolicy.VerticalBand(m_VerticalFraction);
        bool stable = m_State.Stable;
        // Ordinary cues finish. Qualified corrections can request bounded parallel
        // Jev advice; approval still needs fresh evidence before and after the fade.
        // Exact right-controller hit + nearby valid gaze outranks search directions.
        // Recheck after the short audio fade; no announcement can survive a lost hit.
        if (m_CurrentHint == DirectionalHintPolicy.LookingPhrase && m_Voice.IsBusy) return;
        if (m_State.Allows(GuidanceAction.ConfirmPointing))
        {
            string phrase = DirectionalHintPolicy.PointingPhrase;
            var decision = Decision("requested", "controller_target_gaze_vicinity", phrase, phrase, true, inside, far, true);
            bool currentInside = inside;
            if (m_Voice.TryAreaCorrection(phrase,
                () => { Evaluate(out bool v, out currentInside, out _); return Allows(GuidanceAction.ConfirmPointing) && Active() && EvaluatePointing(v) && m_State.PointingStable; },
                () => {
                    Record(Decision("committed", "pointing_revalidated", phrase, phrase,
                        true, currentInside, false, true, decision.decisionId));
                    Commit(GuidanceAction.ConfirmPointing, currentInside);
                    m_LastTipTime = Time.time;
                    m_LastDirection = null;
                }, "controller_target_gaze_vicinity", decision.decisionId))
            { Record(decision); m_CurrentHint = phrase; }
            return; // Do not keep saying look up/down while the person is pointing at it.
        }
        if (m_CurrentHint == DirectionalHintPolicy.PointingPhrase && m_Voice.IsBusy) return;
        if (looking)
        {
            if (m_State.Allows(GuidanceAction.ConfirmGaze))
            {
                string category = DirectionalHintPolicy.LookingPhrase;
                string phrase = DirectionalHintPolicy.Variant(category, m_LookingVariant);
                var decision = Decision("requested", "gaze_target_vicinity_hold", category, phrase, true, inside, far, true);
                if (m_Voice.TryAreaCorrection(phrase,
                    () => { Evaluate(out bool v, out _, out _); EvaluatePointing(v); return Allows(GuidanceAction.ConfirmGaze) && Active() && EvaluateLooking(v) && m_State.LookingStable; },
                    () => {
                        Record(Decision("committed", "looking_revalidated", category, phrase, true, inside, far, true, decision.decisionId));
                        Commit(GuidanceAction.ConfirmGaze, inside);
                        m_LookingVariant++;
                        m_LastTipTime = Time.time; m_LastDirection = null;
                    }, "gaze_target_vicinity_hold", decision.decisionId))
                { Record(decision); m_CurrentHint = category; }
            }
            return; // Let the person hold gaze within the target region without nudging it away.
        }
        if (TryAdaptiveWallArrival(valid,inside,Allows)) return;
        if (TryAdaptiveGuidance(desired, valid, inside, far, looking, Allows)) return;
        // Remember a qualified target visit, even though the narrow crossing
        // history is cleared by confirmation. A blink cannot manufacture a departure.
        if (m_State.Allows(GuidanceAction.ReturnTarget) && CanCorrect(stable))
        {
            string returnDirection = desired;
            string phrase = DirectionalHintPolicy.ReturnToTarget(returnDirection);
            var decision = Decision("requested", "recent_target_departure", returnDirection, phrase, true, inside, far, stable);
            if (m_Voice.TryAreaCorrection(phrase,
                () => {
                    var d = Evaluate(out bool v, out _, out _); EvaluatePointing(v);
                    return Allows(GuidanceAction.ReturnTarget) && Active() && v && !EvaluateLooking(v) && d == returnDirection && m_State.RevisitPending(Time.realtimeSinceStartupAsDouble);
                },
                () => {
                    Record(Decision("committed", "recent_target_departure_revalidated", returnDirection, phrase, true, inside, far, true, decision.decisionId));
                    Commit(GuidanceAction.ReturnTarget, inside);
                    m_LastTipTime = Time.time; RememberDirection(returnDirection);
                }, "recent_target_departure", decision.decisionId))
            { Record(decision); m_CurrentHint = k_RevisitCategory; return; }
        }
        // Object-bearing overshoot has priority over the older wall-exit cue.
        // Its frozen history is attached to both request and committed records.
        string passedDirection = m_State.Allows(GuidanceAction.ReturnBearing) ? m_State.TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble) : null;
        if (passedDirection != null && m_CurrentHint == passedDirection)
        {
            // We are already giving the required correction: do not cut "Look
            // down" off just to replace it with "Go back down a little."
            Record(Decision("suppressed", "same_direction_already_given", passedDirection, "", true, inside, far, stable));
            m_State.SuppressBearing(Time.realtimeSinceStartupAsDouble);
            PublishState(valid);
            passedDirection = null;
        }
        if (passedDirection != null && CanCorrect(stable))
        {
            string phrase = DirectionalHintPolicy.PassedTarget(passedDirection, m_VerticalFraction);
            var evidence = m_State.TargetHistory.Snapshot();
            var decision = Decision("requested", "target_bearing_overshoot", passedDirection, phrase, true, inside, far, stable);
            decision.targetHistory = evidence;
            bool correctionInside = inside, correctionFar = far;
            if (m_Voice.TryAreaCorrection(phrase,
                () => {
                    Evaluate(out bool v, out correctionInside, out correctionFar);
                    return Allows(GuidanceAction.ReturnBearing) && Active() && v &&
                        !(m_VisualReferenceVerticalSuppressed && DirectionalHintPolicy.HasVertical(passedDirection)) && m_State.TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble) == passedDirection &&
                        GazeTargetHistory.StillBeyond(passedDirection, m_YawError, m_PitchError) &&
                        DirectionalHintPolicy.PassedTarget(passedDirection, m_VerticalFraction) == phrase;
                },
                () => {
                    var committed = Decision("committed", "target_bearing_overshoot_revalidated", passedDirection, phrase,
                        true, correctionInside, correctionFar, true, decision.decisionId);
                    committed.targetHistory = evidence; Record(committed);
                    Commit(GuidanceAction.ReturnBearing, correctionInside);
                    m_LastTipTime = Time.time;
                    RememberDirection(passedDirection);
                }, "target_bearing_overshoot", decision.decisionId))
            { Record(decision); m_CurrentHint = k_TargetReturnCategory; return; }
        }
        if (m_State.Allows(GuidanceAction.Arrival) && CanCorrect(stable))
        {
            string entryDirection = desired ?? DirectionalHintPolicy.AlignedPhrase;
            string phrase = DirectionalHintPolicy.AreaEntry(entryDirection,m_AreaVariant, m_VerticalFraction);
            var decision = Decision("requested", "stable_wall_entry", AreaCorrectionPhrase, phrase, true, inside, far, stable);
            if (m_Voice.TryAreaCorrection(phrase,
                () => { var d = Evaluate(out bool v,out bool i,out _); return Allows(GuidanceAction.Arrival) && Active() && v && i && d == entryDirection &&
                    DirectionalHintPolicy.AreaEntry(entryDirection, m_AreaVariant, m_VerticalFraction) == phrase; },
                () => {
                    Record(Decision("committed", "wall_entry_revalidated", AreaCorrectionPhrase, phrase,
                        true, true, false, m_State.Stable, decision.decisionId));
                    Commit(GuidanceAction.Arrival, true); m_AreaVariant++; m_LastTipTime=Time.time;
                    RememberDirection(entryDirection);
                }, guidanceDecisionId: decision.decisionId))
            { Record(decision); m_CurrentHint = AreaCorrectionPhrase; return; }
        }
        if (m_State.Allows(GuidanceAction.ReturnWall)) m_ReturnDirection = desired;
        if (m_State.Allows(GuidanceAction.ReturnWall) && CanCorrect(stable))
        {
            string phrase = DirectionalHintPolicy.ReturnToArea(desired);
            var decision = Decision("requested", "stable_wall_exit", DirectionalHintPolicy.ReturnPhrase, phrase, true, inside, far, stable);
            bool revalidatedFar = far;
            if (m_Voice.TryAreaCorrection(phrase,
                () => { var d=Evaluate(out bool v,out bool i,out revalidatedFar); return Allows(GuidanceAction.ReturnWall) && Active() && v && !i && d==m_ReturnDirection && m_State.WallReturnPending(Time.realtimeSinceStartupAsDouble); },
                () => {
                    Record(Decision("committed", "wall_exit_revalidated", DirectionalHintPolicy.ReturnPhrase, phrase,
                        true, false, revalidatedFar, m_State.Stable, decision.decisionId));
                    Commit(GuidanceAction.ReturnWall, false); m_ReturnVariant++; m_LastTipTime=Time.time;
                    m_LastDirection = null;
                },"gaze_overshoot_return",decision.decisionId))
            { Record(decision); m_ReturnDirection=desired; m_CurrentHint=DirectionalHintPolicy.ReturnPhrase; return; }
        }
        if ((!m_State.Allows(GuidanceAction.Direction) && !m_State.Allows(GuidanceAction.Progress)) ||
            !m_Voice.GuidanceReady || Time.time-m_LastTipTime<k_TipInterval) return;
        bool progress = m_State.Allows(GuidanceAction.Progress);
        var ordinaryAction = m_State.Action;
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
                if (!Allows(GuidanceAction.Direction) || !Active() || !stillValid || nowInside || current != desired ||
                    !m_State.Stable) return false;
                var selected = Decision("requested", "catalog_turn:" + phrase.id, desired, phrase.text, true, false, nowFar, true);
                selected.decisionId = selection.RequestId ?? selected.decisionId;
                var target = m_EvaluatedTarget.transform;
                bool accepted = m_Voice.TryGuidanceTransmission(phrase.text,
                    () => Allows(GuidanceAction.Direction) && OrdinaryCueRelevant(desired, false) && m_Navigation.IsPhraseCurrent(target, phrase),
                    () => {
                        m_CurrentHint = desired; m_LastTipTime = Time.time;
                        RememberDirection(desired); Commit(GuidanceAction.Direction, false);
                        m_Navigation.MarkSpoken(phrase);
                        Record(Decision("committed", "catalog_transmission_approved", desired, phrase.text, true, false, nowFar, true, selected.decisionId));
                    }, "catalog_turn", selected.decisionId, selection.Source == "local" ? (bool?)null : selection.Speak, selection.Source);
                if (accepted) Record(selected);
                return accepted;
            })) return;
        bool briefProgress = progress && !inside && m_State.Turn.BriefProgressAllowed(desired, Time.realtimeSinceStartupAsDouble);
        string wording = !inside ? (progress ? (briefProgress ? DirectionalHintPolicy.BriefProgress(m_ProgressVariant) : DirectionalHintPolicy.Progress(desired, m_ProgressVariant)) :
            DirectionalHintPolicy.Turn(desired, m_TurnRemaining, m_Variant)) : DirectionalHintPolicy.FollowUp(desired, m_LastDirection,
            DirectionalHintPolicy.DirectionError(desired, m_YawError, m_PitchError), m_LastDirectionError, m_Variant, far,
            m_ContinuationUsed || (DirectionalHintPolicy.HasVertical(desired) && verticalBand != m_LastVerticalBand), m_VerticalFraction);
        string ordinaryReason = progress ? "turn_progress_milestone" : "stable_direction";
        var request = Decision("requested", ordinaryReason, desired, wording, true, inside, far, true);
        if (m_Voice.TryGuidanceTransmission(wording,
            () => Allows(ordinaryAction) && OrdinaryCueRelevant(desired, inside) &&
                (!briefProgress || m_State.Turn.BriefProgressAllowed(desired, Time.realtimeSinceStartupAsDouble)) &&
                (!DirectionalHintPolicy.HasVertical(desired) || verticalBand == DirectionalHintPolicy.VerticalBand(m_VerticalFraction)),
            () => {
                m_CurrentHint = desired; m_LastTipTime = Time.time;
                if (progress) m_ProgressVariant++; else m_Variant++;
                RememberDirection(desired);
                Commit(ordinaryAction, inside);
                if (DirectionalHintPolicy.IsContinuation(wording)) m_ContinuationUsed = true;
                Record(Decision("committed", "transmission_approved:" + ordinaryReason, desired, wording, true, inside, far, true, request.decisionId));
            }, ordinaryReason, request.decisionId)) Record(request);
    }

    bool OrdinaryCueRelevant(string direction, bool wasInside)
    {
        string current = Evaluate(out bool valid, out bool inside, out _);
        EvaluatePointing(valid);
        bool looking = EvaluateLooking(valid);
        return Active() && valid && current == direction && inside == wasInside &&
            m_State.Stable &&
            !looking && !(m_ControllerEvidenceValid && m_ControllerOnTarget) &&
            m_State.PendingBearingDirection(Time.realtimeSinceStartupAsDouble, m_VisualReferenceVerticalSuppressed) == null &&
            !m_State.RevisitPending(Time.realtimeSinceStartupAsDouble);
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
        if (EvaluatePointing(valid) && m_State.PointingStable) desired = DirectionalHintPolicy.PointingPhrase;
        else if (EvaluateLooking(valid) && m_State.LookingStable) desired = DirectionalHintPolicy.LookingPhrase;
        else if (m_ControllerEvidenceValid && m_ControllerOnTarget) desired = null;
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
        var repeatTarget = m_EvaluatedTarget;
        m_Voice.Speak(phrase,"guidance_repeat",guidanceDecisionId:decision.decisionId, guidanceRelevant: () => {
            var current = Evaluate(out bool v, out _, out _);
            bool pointing = EvaluatePointing(v), looking = EvaluateLooking(v);
            if (!Active() || !v || m_EvaluatedTarget != repeatTarget) return false;
            if (desired == DirectionalHintPolicy.PointingPhrase) return pointing && m_State.PointingStable;
            if (desired == DirectionalHintPolicy.LookingPhrase) return looking && m_State.LookingStable;
            return current == desired && !looking && !(m_ControllerEvidenceValid && m_ControllerOnTarget);
        });
    }

    // Retain exact-hit evidence separately. Spoken confirmation uses the padded
    // region measured by EvaluatePointing, only when the target uniquely wins
    // against all overlapping object rings, including when the dev ring is hidden.



    public bool TryPrepareOpening(out string phrase, out string decisionId)
    {
        phrase = decisionId = null;
        if (m_State.Phase != GuidancePhase.Opening) SetLifecycle(GuidancePhase.Opening, "presearch_opening");
        string coarse = Evaluate(out bool valid, out bool inside, out bool far);
        if (!valid) return false;
        // Same-wall starts can refine height and lateral position before reveal.
        // Use fresh thresholds, without carrying the previous trial's hysteresis.
        string direction = inside ? DirectionalHintPolicy.WithinArea(m_YawError, m_PitchError) :
            coarse ?? DirectionalHintPolicy.AlignedPhrase;
        if (m_OpeningHistoryIdentity != HistoryIdentity)
        {
            m_AdaptiveHistory.BeginTrial(HistoryParticipant, HistoryTrial, Time.realtimeSinceStartupAsDouble, false);
            m_AdaptiveHistory.BreakObservation();
            m_OpeningHistoryIdentity = HistoryIdentity;
        }
        phrase = DirectionalHintPolicy.Opening(direction, inside, m_VerticalFraction);
        m_OpeningTurn = !inside ? direction : null;
        PublishState(valid);
        var decision = Decision("requested", "presearch_target_direction", direction, phrase, true, inside, far, true);
        decisionId = decision.decisionId; Record(decision);
        return true;
    }





    public void SetVicinityRing(bool visible)
    {
        if (m_Vicinity == null || !SessionConfig.DevelopmentQuickTest) return;
        m_Vicinity.SetRingVisible(visible);
        Record(Decision("configuration", "admin_vicinity_ring", "target_vicinity", "", false, false, false, false));
    }
    public void AdjustVicinityPadding(float delta)
    {
        if (m_Vicinity == null || !SessionConfig.DevelopmentQuickTest) return;
        m_Vicinity.SetPadding(m_Vicinity.PaddingDegrees + delta);
        Record(Decision("configuration", "admin_vicinity_radius", "target_vicinity", "", false, false, false, false));
    }


    void Record(GuidanceDecision decision) => m_Voice?.RecordGuidanceDecision(JsonUtility.ToJson(decision));
    bool Active() => !m_TipsSuppressed && m_GameManager != null && m_GameManager.SearchActive;

    public static string[] AllPhrases() => DirectionalHintPolicy.AllPhrases();




}
