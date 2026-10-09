using UnityEngine;

/// <summary>Shared valid gaze drives coarse horizontal, then fine vertical guidance.</summary>
[DefaultExecutionOrder(350)] // After ControllerRaySelector's current-frame telemetry.
public class HintGenerator : MonoBehaviour
{
    public const string AreaCorrectionPhrase = DirectionalHintPolicy.AreaPhrase;
    const string k_TargetReturnCategory = "target_passage_return";
    string m_TargetReturnDirection;
    const double k_DirectionSettleSeconds = .35;
    const float k_TipInterval = 1.25f; // Pilot cadence; same policy in both voice conditions.
    readonly GazeTargetHistory m_TargetHistory = new GazeTargetHistory();
    float m_YawError, m_PitchError;
    bool m_GeometryAvailable, m_OpeningPending;
    string m_OpeningTrialKey;
    readonly GazeZoneEntryGate m_ZoneEntry = new GazeZoneEntryGate(6, k_DirectionSettleSeconds);
    readonly GazeZoneEntryGate m_PointingEntry = new GazeZoneEntryGate(0, 1.5);
    readonly GazeReturnCueGate m_ReturnCue = new GazeReturnCueGate();
    string m_ReturnDirection, m_CurrentHint, m_Candidate;
    string m_LastSuspension;
    GazeObservation m_EvaluatedSample;
    double m_CandidateSince;
    float m_LastTipTime;
    bool m_TipsSuppressed = true;
    int m_Variant, m_AreaVariant, m_ReturnVariant;
    VoiceSynthesizer m_Voice;
    FindObjectGameManager m_GameManager;
    ControllerRaySelector m_Controller;
    GazeTargetVicinity m_Vicinity;
    SpawnableObjectInfo m_EvaluatedTarget;
    string m_AreaDirection;
    string m_LastDirection;
    float m_LastDirectionError;
    bool m_VicinityMeasured, m_ControllerEvidenceValid, m_ControllerOnTarget;
    float m_VicinityError, m_VicinityRadius;
    public GazeTargetVicinity Vicinity => m_Vicinity;
    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null) => m_Voice = voice;
    void Start()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
        m_Vicinity = GetComponent<GazeTargetVicinity>() ?? gameObject.AddComponent<GazeTargetVicinity>();
    }
    public void OnNewObjective()
    {
        m_ZoneEntry.Reset(); m_PointingEntry.Reset(); m_ReturnCue.Reset(); m_TargetHistory.Reset();
        string trialKey = $"{SessionConfig.ParticipantId}|{SessionConfig.CurrentRunFolder}|{m_GameManager?.PresentedRoundIndex}|{m_GameManager?.PresentedRoundIsPractice}";
        if (trialKey != m_OpeningTrialKey) { m_OpeningPending = true; m_OpeningTrialKey = trialKey; }
        m_CurrentHint = m_Candidate = m_ReturnDirection = m_AreaDirection = null;
        m_LastDirection = null;
        m_LastSuspension = null;
        m_Variant = m_AreaVariant = m_ReturnVariant =
            (ChallengeSet.ScheduleSeed % 8 + (m_GameManager != null ? m_GameManager.CurrentObjectiveIndex : 0)) % 8;
        m_LastTipTime = Time.time - k_TipInterval; m_TipsSuppressed = false;
    }
    public void OnWrongCapture() { }
    public void CancelPending()
    {
        m_TipsSuppressed = true; m_ZoneEntry.Reset(); m_PointingEntry.Reset(); m_ReturnCue.Reset(); m_TargetHistory.Clear();
        m_Vicinity?.Hide();
        m_CurrentHint = m_Candidate = m_ReturnDirection = null; InterruptGuidance();
        m_LastDirection = null;
    }
    void LateUpdate()
    {
        if (m_Voice == null || m_GameManager == null || m_TipsSuppressed) { PreviewPausedVicinity(); return; }
        if (!m_GameManager.SearchActive || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
        { InterruptGuidance(); m_TargetHistory.Clear(); PreviewPausedVicinity(); m_CurrentHint = null; return; }
        string desired = Evaluate(out bool valid, out bool inside, out bool far);
        bool pointing = EvaluatePointing(valid);
        // Hold duration belongs to the controller. Gaze must be valid/nearby at
        // announcement, but a blink must not erase a continuously tracked ray hit.
        bool pointingEntry = m_PointingEntry.Update(m_ControllerEvidenceValid, m_ControllerOnTarget, Time.timeAsDouble);
        UpdateTargetHistory(valid);
        bool entry = m_ZoneEntry.Update(valid, inside, Time.timeAsDouble);
        bool returning = m_ReturnCue.Update(valid, inside, Time.timeAsDouble);
        if (!valid)
        {
            string reason = m_EvaluatedSample == null ? "no_gaze_observation" :
                string.IsNullOrEmpty(m_EvaluatedSample.invalidReason) ? "unusable_gaze_or_target_geometry" : m_EvaluatedSample.invalidReason;
            if (m_LastSuspension != reason)
                Record(Decision("suspended", reason, null, "", false, inside, far, false));
            m_LastSuspension = reason;
            InterruptGuidance(); m_CurrentHint = m_Candidate = null;
            m_LastDirection = null;
            return; // Tracking loss never invents head-gaze guidance or a wall exit.
        }
        m_LastSuspension = null;
        if (desired != m_Candidate) { m_Candidate = desired; m_CandidateSince = Time.timeAsDouble; }
        bool stable = Time.timeAsDouble - m_CandidateSince >= k_DirectionSettleSeconds;
        // Confirmation describes the qualified hold at playback onset. A tiny
        // subsequent pointer departure should not chop "You're on it!" in half.
        bool stale = m_CurrentHint == DirectionalHintPolicy.PointingPhrase ? false :
            m_CurrentHint == k_TargetReturnCategory ?
            !GazeTargetHistory.StillBeyond(m_TargetReturnDirection, m_YawError, m_PitchError) :
            m_CurrentHint == DirectionalHintPolicy.ReturnPhrase ? inside || desired != m_ReturnDirection :
            m_CurrentHint == AreaCorrectionPhrase ? !inside || DirectionalHintPolicy.RequiresInterruption(m_AreaDirection, desired) :
            DirectionalHintPolicy.RequiresInterruption(m_CurrentHint, desired);
        if (m_CurrentHint != null && stale && stable && !m_ReturnCue.IsDebouncingExit)
        {
            Record(Decision("interruption", "direction_changed", desired, "", true, inside, far, true));
            InterruptGuidance(); m_CurrentHint = null;
        }
        // Exact right-controller hit + nearby valid gaze outranks search directions.
        // Recheck after the short audio fade; no announcement can survive a lost hit.
        if (pointing)
        {
            if (pointingEntry)
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
                        if (currentInside) m_ZoneEntry.MarkSpoken(Time.timeAsDouble);
                        m_TargetHistory.Clear(); m_ReturnCue.MarkSpoken(Time.timeAsDouble);
                        m_OpeningPending = false; m_LastTipTime = Time.time;
                        m_LastDirection = null;
                    }, "controller_target_gaze_vicinity", decision.decisionId))
                { Record(decision); m_CurrentHint = phrase; }
            }
            return; // Do not keep saying look up/down while the person is pointing at it.
        }
        if (m_CurrentHint == DirectionalHintPolicy.PointingPhrase && m_Voice.IsBusy) return;
        // The first directional message uses the participant's live gaze, not a
        // scheduled wall angle. Same-trial resume does not replay the opening.
        if (m_OpeningPending && !entry)
        {
            if (!stable || desired == null || m_Voice.IsBusy) return;
            string phrase = DirectionalHintPolicy.Opening(desired);
            var opening = Decision("requested", "initial_target_direction", desired, phrase, true, inside, far, true);
            m_OpeningPending = false;
            m_CurrentHint = desired; m_LastTipTime = Time.time;
            Record(opening); m_Voice.Speak(phrase, "tip", guidanceDecisionId: opening.decisionId);
            return;
        }
        // Arrival/pointing corrections may interrupt the opening just like other hints.
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
        if (passedDirection != null)
        {
            string phrase = DirectionalHintPolicy.PassedTarget(passedDirection);
            var evidence = m_TargetHistory.Snapshot();
            var decision = Decision("requested", "target_bearing_overshoot", passedDirection, phrase, true, inside, far, stable);
            decision.targetHistory = evidence;
            bool correctionInside = inside, correctionFar = far;
            if (m_Voice.TryAreaCorrection(phrase,
                () => {
                    Evaluate(out bool v, out correctionInside, out correctionFar);
                    UpdateTargetHistory(v);
                    return Active() && v && m_TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble) == passedDirection;
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
            { Record(decision); m_CurrentHint = k_TargetReturnCategory; m_TargetReturnDirection = passedDirection; return; }
        }
        if (entry && stable)
        {
            string entryDirection = desired ?? DirectionalHintPolicy.AlignedPhrase;
            string phrase = DirectionalHintPolicy.AreaEntry(entryDirection,m_AreaVariant);
            var decision = Decision("requested", "stable_wall_entry", AreaCorrectionPhrase, phrase, true, inside, far, stable);
            if (m_Voice.TryAreaCorrection(phrase,
                () => { var d = Evaluate(out bool v,out bool i,out _); return Active() && v && i && d == entryDirection; },
                () => {
                    Record(Decision("committed", "wall_entry_revalidated", AreaCorrectionPhrase, phrase,
                        true, true, false, Time.timeAsDouble-m_CandidateSince>=k_DirectionSettleSeconds, decision.decisionId));
                    m_ZoneEntry.MarkSpoken(Time.timeAsDouble); m_AreaVariant++; m_LastTipTime=Time.time;
                    m_OpeningPending = false;
                    RememberDirection(entryDirection);
                }, guidanceDecisionId: decision.decisionId))
            { Record(decision); m_CurrentHint = AreaCorrectionPhrase; m_AreaDirection = entryDirection; return; }
        }
        if (returning) m_ReturnDirection = desired;
        if (returning && desired != null && stable)
        {
            string phrase = DirectionalHintPolicy.Variant(DirectionalHintPolicy.ReturnPhrase,m_ReturnVariant);
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
            m_Voice.IsBusy || Time.time-m_LastTipTime<k_TipInterval) return;
        m_CurrentHint=desired; m_LastTipTime=Time.time;
        string wording = DirectionalHintPolicy.FollowUp(desired, m_LastDirection,
            DirectionalHintPolicy.DirectionError(desired, m_YawError, m_PitchError), m_LastDirectionError, m_Variant++, far);
        RememberDirection(desired);
        var request = Decision("requested", "stable_direction", desired, wording, true, inside, far, true);
        Record(request);
        m_Voice.Speak(wording,"tip",guidanceDecisionId:request.decisionId);
    }
    void RememberDirection(string direction)
    {
        m_LastDirection = direction;
        m_LastDirectionError = DirectionalHintPolicy.DirectionError(direction, m_YawError, m_PitchError);
    }
    void InterruptGuidance()
    {
        m_Voice?.InterruptIfAbout("tip");
        m_Voice?.InterruptIfAbout("guidance_repeat");
    }
    public void RequestRepeat()
    {
        string desired = Evaluate(out bool valid, out bool inside, out bool far);
        if (EvaluatePointing(valid) && m_PointingEntry.InsideStable) desired = DirectionalHintPolicy.PointingPhrase;
        bool available = Active() && valid && desired != null && m_Voice != null;
        string phrase = available ? (desired == DirectionalHintPolicy.PointingPhrase ? desired : m_OpeningPending ? DirectionalHintPolicy.Opening(desired) :
            DirectionalHintPolicy.Variant(desired,m_Variant++,false,far)) : "";
        var decision = Decision(available ? "requested" : "unavailable", "controller_repeat_request",
            desired, phrase, valid, inside, far, false);
        Record(decision);
        m_Voice?.RecordGuidanceRepeatRequest(available, decision.decisionId);
        if (!available) return;
        if (m_OpeningPending) { m_OpeningPending = false; }
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
        if (decision.targetGeometryAvailable) { decision.targetYawError = m_YawError; decision.targetPitchError = m_PitchError; }
        decision.targetVicinityMeasured = m_VicinityMeasured;
        decision.targetVicinityErrorDegrees = m_VicinityError; decision.targetVicinityRadiusDegrees = m_VicinityRadius;
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
    bool EvaluatePointing(bool valid)
    {
        m_VicinityMeasured = m_ControllerEvidenceValid = m_ControllerOnTarget = false;
        m_VicinityError = m_VicinityRadius = 0;
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
        m_Vicinity?.Draw(m_EvaluatedSample.worldOrigin, collider.bounds, m_VicinityRadius, nearby);
        return m_ControllerOnTarget && nearby;
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
        valid=inside=far=false; m_GeometryAvailable = false; m_EvaluatedTarget = null;
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
        // Once in the correct wall, guide height before final horizontal refinement.
        float yaw=Vector3.SignedAngle(new Vector3(gaze.x,0,gaze.z),new Vector3(delta.x,0,delta.z),Vector3.up);
        m_YawError = yaw; m_PitchError = targetElevation - gazeElevation; m_GeometryAvailable = true;
        if (!inside) return DirectionalHintPolicy.Direction(gaze.x,gaze.z,delta.x,delta.z,out far);
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
