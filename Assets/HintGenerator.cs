using UnityEngine;

/// <summary>Shared valid gaze drives coarse horizontal, then fine vertical guidance.</summary>
public class HintGenerator : MonoBehaviour
{
    public const string AreaCorrectionPhrase = DirectionalHintPolicy.AreaPhrase;
    const float k_TipInterval = 1.25f; // Pilot cadence; same policy in both voice conditions.
    readonly GazeZoneEntryGate m_ZoneEntry = new GazeZoneEntryGate();
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
    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null) => m_Voice = voice;
    void Start() => m_GameManager = GetComponent<FindObjectGameManager>();
    public void OnNewObjective()
    {
        m_ZoneEntry.Reset(); m_ReturnCue.Reset();
        m_CurrentHint = m_Candidate = m_ReturnDirection = null;
        m_LastSuspension = null;
        m_Variant = m_AreaVariant = m_ReturnVariant =
            (ChallengeSet.ScheduleSeed % 8 + (m_GameManager != null ? m_GameManager.CurrentObjectiveIndex : 0)) % 8;
        m_LastTipTime = Time.time - k_TipInterval; m_TipsSuppressed = false;
    }
    public void OnWrongCapture() { }
    public void CancelPending()
    {
        m_TipsSuppressed = true; m_ZoneEntry.Reset(); m_ReturnCue.Reset();
        m_CurrentHint = m_Candidate = m_ReturnDirection = null; InterruptGuidance();
    }
    void Update()
    {
        if (m_Voice == null || m_GameManager == null || m_TipsSuppressed) return;
        if (!m_GameManager.SearchActive || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
        { InterruptGuidance(); m_CurrentHint = null; return; }
        string desired = Evaluate(out bool valid, out bool inside, out bool far);
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
            return; // Tracking loss never invents head-gaze guidance or a wall exit.
        }
        m_LastSuspension = null;
        if (desired != m_Candidate) { m_Candidate = desired; m_CandidateSince = Time.timeAsDouble; }
        bool stable = Time.timeAsDouble - m_CandidateSince >= .15;
        bool stale = m_CurrentHint == DirectionalHintPolicy.ReturnPhrase ? inside || desired != m_ReturnDirection :
            m_CurrentHint == AreaCorrectionPhrase ? !inside : desired != m_CurrentHint;
        if (m_CurrentHint != null && stale && stable)
        {
            Record(Decision("interruption", "direction_changed", desired, "", true, inside, far, true));
            InterruptGuidance(); m_CurrentHint = null;
        }
        if (entry)
        {
            string phrase = DirectionalHintPolicy.Variant(AreaCorrectionPhrase,m_AreaVariant);
            var decision = Decision("requested", "stable_wall_entry", AreaCorrectionPhrase, phrase, true, inside, far, stable);
            if (m_Voice.TryAreaCorrection(phrase,
                () => { Evaluate(out bool v,out bool i,out _); return Active() && v && i; },
                () => {
                    Record(Decision("committed", "wall_entry_revalidated", AreaCorrectionPhrase, phrase,
                        true, true, false, Time.timeAsDouble-m_CandidateSince>=.15, decision.decisionId));
                    m_ZoneEntry.MarkSpoken(Time.timeAsDouble); m_AreaVariant++; m_LastTipTime=Time.time;
                }, guidanceDecisionId: decision.decisionId))
            { Record(decision); m_CurrentHint = AreaCorrectionPhrase; return; }
        }
        if (returning) m_ReturnDirection = desired;
        if (returning && desired != null)
        {
            string phrase = DirectionalHintPolicy.Variant(DirectionalHintPolicy.ReturnPhrase,m_ReturnVariant);
            var decision = Decision("requested", "stable_wall_exit", DirectionalHintPolicy.ReturnPhrase, phrase, true, inside, far, stable);
            bool revalidatedFar = far;
            if (m_Voice.TryAreaCorrection(phrase,
                () => { var d=Evaluate(out bool v,out bool i,out revalidatedFar); return Active() && v && !i && d==m_ReturnDirection && m_ReturnCue.IsPending(Time.timeAsDouble); },
                () => {
                    Record(Decision("committed", "wall_exit_revalidated", DirectionalHintPolicy.ReturnPhrase, phrase,
                        true, false, revalidatedFar, Time.timeAsDouble-m_CandidateSince>=.15, decision.decisionId));
                    m_ReturnCue.MarkSpoken(Time.timeAsDouble); m_ReturnVariant++; m_LastTipTime=Time.time;
                },"gaze_overshoot_return",decision.decisionId))
            { Record(decision); m_ReturnDirection=desired; m_CurrentHint=DirectionalHintPolicy.ReturnPhrase; return; }
        }
        if (!stable || desired == null || m_ReturnCue.IsDebouncingExit || (inside && !m_ZoneEntry.InsideStable) ||
            m_Voice.IsBusy || Time.time-m_LastTipTime<k_TipInterval) return;
        m_CurrentHint=desired; m_LastTipTime=Time.time;
        string wording = DirectionalHintPolicy.Variant(desired,m_Variant++,false,far);
        var request = Decision("requested", "stable_direction", desired, wording, true, inside, far, true);
        Record(request);
        m_Voice.Speak(wording,"tip",guidanceDecisionId:request.decisionId);
    }
    void InterruptGuidance()
    {
        m_Voice?.InterruptIfAbout("tip");
        m_Voice?.InterruptIfAbout("guidance_repeat");
    }
    public void RequestRepeat()
    {
        string desired = Evaluate(out bool valid, out bool inside, out bool far);
        bool available = Active() && valid && desired != null && m_Voice != null;
        string phrase = available ? DirectionalHintPolicy.Variant(desired,m_Variant++,false,far) : "";
        var decision = Decision(available ? "requested" : "unavailable", "controller_repeat_request",
            desired, phrase, valid, inside, far, false);
        Record(decision);
        m_Voice?.RecordGuidanceRepeatRequest(available, decision.decisionId);
        if (!available) return;
        // Repeat the current direction, not an obsolete phrase or hidden target identity.
        m_CurrentHint = desired; m_LastTipTime = Time.time;
        m_Voice.Speak(phrase,"guidance_repeat",guidanceDecisionId:decision.decisionId);
    }
    GuidanceDecision Decision(string stage, string reason, string category, string phrase,
        bool valid, bool inside, bool far, bool stable, string id = null) =>
        GuidanceDecision.Capture(id ?? System.Guid.NewGuid().ToString("N"), stage, reason,
            category ?? "none", phrase, m_EvaluatedSample, Time.realtimeSinceStartupAsDouble,
            valid, inside, far, stable);
    void Record(GuidanceDecision decision) => m_Voice?.RecordGuidanceDecision(JsonUtility.ToJson(decision));
    bool Active() => !m_TipsSuppressed && m_GameManager != null && m_GameManager.SearchActive;
    string Evaluate(out bool valid,out bool inside,out bool far)
    {
        valid=inside=far=false;
        m_EvaluatedSample = SharedGazeStream.Instance != null ? SharedGazeStream.Instance.Latest : null;
        if (m_GameManager == null || !m_GameManager.RotationalBetaEnabled || SharedGazeStream.Instance == null ||
            !SharedGazeStream.Instance.TryGetValid(out var sample) || !TryGetTargetInfo(out var target)) return null;
        m_EvaluatedSample = sample;
        Vector3 gaze=sample.worldDirection, delta=target.transform.position-sample.worldOrigin;
        // Azimuth becomes unstable looking nearly straight up/down. This is a gap, not wall departure.
        if (gaze.x*gaze.x+gaze.z*gaze.z<.04f || delta.sqrMagnitude<.001f) return null;
        inside=m_GameManager.IsGazeInSearchSector(sample.worldOrigin,gaze,target.planeId,out valid);
        if (!valid) return null;
        if (!inside) return DirectionalHintPolicy.Direction(gaze.x,gaze.z,delta.x,delta.z,out far);
        float gazeElevation=Mathf.Atan2(gaze.y,new Vector2(gaze.x,gaze.z).magnitude)*Mathf.Rad2Deg;
        float targetElevation=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
        // Within-wall horizontal fine correction precedes an aligned/arrival statement.
        float yaw=Vector3.SignedAngle(new Vector3(gaze.x,0,gaze.z),new Vector3(delta.x,0,delta.z),Vector3.up);
        if (Mathf.Abs(yaw)>5) return yaw>0 ? DirectionalHintPolicy.RightPhrase : DirectionalHintPolicy.LeftPhrase;
        return GazeQualityPolicy.Vertical(gazeElevation,targetElevation,3);
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
