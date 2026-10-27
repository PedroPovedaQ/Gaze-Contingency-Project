using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SpatialVoice;
using UnityEngine;

public partial class HintGenerator
{
    readonly AdaptiveGuidanceHistory m_AdaptiveHistory = new AdaptiveGuidanceHistory();
    readonly AdaptiveGuidanceSchedule m_AdaptiveSchedule = new AdaptiveGuidanceSchedule();
    long m_ActionPolicyEpoch;

    public void ToggleDevelopmentGuidancePolicy()
    {
        if (m_GameManager != null && m_GameManager.SearchActive && !m_GameManager.ResearcherPaused) return;
        if (!SessionConfig.TrySetJevLedGuidance(!SessionConfig.JevLedGuidance)) return;
        m_ActionPolicyEpoch++; m_AdaptiveSchedule.Reset(); m_AdaptiveHistory.BreakObservation();
        m_Navigation?.Cancel(); m_Navigation?.ResetAssistanceStatus(); m_Voice?.CancelPendingGuidance();
        PublishState(false);
        m_Voice?.RecordGuidanceState(CurrentStateSnapshot);
        m_Voice?.RecordNavigationStatus(new JObject { ["schema"] = "guidance-policy-change-v1",
            ["policy"] = SessionConfig.GuidancePolicy, ["study_data"] = false,
            ["time"] = Time.realtimeSinceStartupAsDouble }.ToString(Newtonsoft.Json.Formatting.None));
    }

    public void ToggleDevelopmentAssistanceProvider()
    {
        if (m_GameManager != null && m_GameManager.SearchActive && !m_GameManager.ResearcherPaused) return;
        if (!SessionConfig.JevLedGuidance || !SessionConfig.TrySetLocalAdaptiveModel(!SessionConfig.LocalAdaptiveModel)) return;
        m_ActionPolicyEpoch++; m_AdaptiveSchedule.Reset(); m_AdaptiveHistory.BreakObservation();
        m_Navigation?.Cancel(); m_Navigation?.ResetAssistanceStatus(); m_Voice?.CancelPendingGuidance();
        PublishState(false);
        m_Voice?.RecordGuidanceState(CurrentStateSnapshot);
        m_Voice?.RecordNavigationStatus(new JObject { ["schema"] = "assistance-provider-change-v1",
            ["provider"] = SessionConfig.AssistanceProvider, ["policy"] = SessionConfig.GuidancePolicy,
            ["model_version"] = SessionConfig.LocalAdaptiveModel ? JevDecisionClient.LocalModelVersion : "typesafe-ai/jev",
            ["study_data"] = false, ["time"] = Time.realtimeSinceStartupAsDouble }.ToString(Newtonsoft.Json.Formatting.None));
    }

    string AdaptiveDirection(string strategy, GuidanceAction action, string direction) =>
        strategy == "correct" && action == GuidanceAction.ReturnBearing
            ? m_State.TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble) : direction;

    // Resolve a strategy into grounded audio locally. No phrase text or preferred action is sent to the policy.
    string AdaptivePhrase(string strategy, GuidanceAction action, string direction, bool inside)
    {
        direction = AdaptiveDirection(strategy, action, direction);
        if (strategy == "correct" && action == GuidanceAction.ReturnBearing)
            return AdaptiveGuidanceGrounding.BearingCorrection(direction, m_YawError, m_PitchError,
                m_VerticalFraction, m_VisualReferenceVerticalSuppressed);
        if (string.IsNullOrEmpty(direction) || direction == DirectionalHintPolicy.AlignedPhrase) return null;
        switch (strategy)
        {
            case "orient_brief": return !inside ? DirectionalHintPolicy.Variant(direction,0) : null;
            case "orient_explicit": return !inside ? AdaptiveGuidanceGrounding.ExplicitOrientation(direction,m_TurnRemaining) : null;
            case "refine_brief": return inside ? DirectionalHintPolicy.Variant(direction,0,verticalFraction:m_VerticalFraction) : null;
            case "refine_explicit": return inside ? DirectionalHintPolicy.AreaEntry(direction,0,m_VerticalFraction) : null;
            case "correct":
                return action == GuidanceAction.ReturnTarget ? DirectionalHintPolicy.ReturnToTarget(direction) :
                    action == GuidanceAction.ReturnWall ? DirectionalHintPolicy.ReturnToArea(direction) : null;
            case "encourage": return m_AdaptiveHistory.HasRecentProgress(direction,Time.realtimeSinceStartupAsDouble)
                ? DirectionalHintPolicy.Progress(direction,0) : null;
            default: return null;
        }
    }

    bool TryAdaptiveGuidance(string direction, bool valid, bool inside, bool far, bool looking, Func<GuidanceAction,bool> allows)
    {
        if (!SessionConfig.JevLedGuidance) return false;
        // Own the search path even during observation or outages: never fall through to the legacy cue scheduler.
        var action = m_State.Action;
        if (valid && m_State.Stable && m_Voice.IsSpeaking &&
            AdaptiveGuidanceGrounding.Opposed(m_CurrentHint, direction))
        {
            m_Voice.InterruptIfAbout("tip", reason: "adaptive_direction_reversed");
            m_Voice.InterruptIfAbout("guidance_repeat", reason: "adaptive_direction_reversed");
        }
        if (!valid || looking || m_ControllerOnTarget || !m_State.Stable || !GuidanceStateMachine.IsSearchAction(action) || !m_Voice.GuidanceReady) return true;
        double now = Time.realtimeSinceStartupAsDouble;
        string evidenceEvent = direction + ":" + inside + ":" + action + ":" + m_AdaptiveHistory.WrongSelections + ":" +
            m_AdaptiveHistory.Trend(now);
        if (!m_AdaptiveSchedule.Due(now,evidenceEvent)) return true;
        if (m_Navigation == null) m_Navigation = GetComponent<StudyNavigationVoiceBridge>();
        if (m_Navigation != null && m_Navigation.IsSelecting) return true;
        m_AdaptiveSchedule.Requested(now,evidenceEvent);
        var target = m_EvaluatedTarget;
        long epoch = m_ActionPolicyEpoch;
        var capabilities = new List<string>();
        var resolved = new Dictionary<string,string>();
        foreach (string strategy in new[] { "orient_brief","orient_explicit","refine_brief","refine_explicit","correct","encourage" })
        {
            string phrase = AdaptivePhrase(strategy,action,direction,inside);
            if (phrase == null || !m_Voice.IsPreparedPhrase(phrase)) continue;
            capabilities.Add(strategy); resolved[strategy] = phrase;
        }
        bool Relevant()
        {
            string current = Evaluate(out bool v,out bool i,out _);
            EvaluatePointing(v);
            return epoch == m_ActionPolicyEpoch && SessionConfig.JevLedGuidance && Active() && v &&
                m_EvaluatedTarget == target && i == inside && current == direction && allows(action) && m_State.Stable &&
                !EvaluateLooking(v) && !(m_ControllerEvidenceValid && m_ControllerOnTarget);
        }
        string id = Guid.NewGuid().ToString("N");
        var runtime = JObject.Parse(m_Voice.CaptureTransmissionContext());
        runtime.Remove("maximum_deferral_seconds"); runtime.Remove("deferred_seconds");
        var state = new JObject { ["schema"] = "adaptive-assistance-v1", ["policy"] = JevDecisionClient.AdaptivePolicy,
            ["decision_id"] = id, ["runtime"] = runtime, ["behavior"] = m_AdaptiveHistory.Snapshot(now) };
        bool requested = m_Navigation != null && m_Navigation.TryAdaptiveRequest(state,capabilities,Relevant, decision => {
            if (!Relevant()) return false;
            if (!decision.Speak) { m_AdaptiveSchedule.Decided(Time.realtimeSinceStartupAsDouble,decision); return true; }
            if (!resolved.TryGetValue(decision.Strategy,out string phrase)) return false;
            bool StillCurrent() => Relevant() && AdaptivePhrase(decision.Strategy,action,direction,inside) == phrase;
            if (!StillCurrent()) return false;
            string spokenDirection = AdaptiveDirection(decision.Strategy,action,direction);
            var recorded = Decision("requested","adaptive:" + decision.Strategy,spokenDirection,phrase,true,inside,far,true,id);
            bool accepted = m_Voice.TryGuidanceTransmission(phrase, StillCurrent, () => {
                m_CurrentHint = spokenDirection; m_LastTipTime = Time.time; RememberDirection(spokenDirection); Commit(action,inside);
                m_AdaptiveHistory.Spoken(id,decision.Strategy,Time.realtimeSinceStartupAsDouble,
                    Math.Sqrt(m_YawError*m_YawError + m_PitchError*m_PitchError));
                Record(Decision("committed","adaptive:" + decision.Strategy,spokenDirection,phrase,true,inside,far,true,id));
            }, "adaptive_assistance", id, true, SessionConfig.AssistanceProvider + "_adaptive");
            if (accepted) { m_AdaptiveSchedule.Decided(Time.realtimeSinceStartupAsDouble,decision); Record(recorded); }
            return accepted;
        });
        if (!requested) m_Voice.RecordNavigationStatus(new JObject { ["schema"] = "adaptive-assistance-status-v1",
            ["policy"] = JevDecisionClient.AdaptivePolicy, ["status"] = "provider_unavailable_observing", ["provider"] = SessionConfig.AssistanceProvider,
            ["retry_after_seconds"] = 2, ["actual_playback"] = false }.ToString(Newtonsoft.Json.Formatting.None));
        return true;
    }
}
