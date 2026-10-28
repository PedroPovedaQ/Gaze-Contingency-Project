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
    AdaptiveGuidanceDecision m_WallArrivalDecision;
    Func<bool> m_WallArrivalRelevant;
    double m_NextWallArrivalRequest;

    bool TryAdaptiveWallArrival(bool valid, bool inside, Func<GuidanceAction,bool> allows)
    {
        bool Eligible() => SessionConfig.JevLedGuidance && !m_AdaptiveHistory.WallArrivalSpoken && !m_State.TargetAcknowledged &&
            m_State.Allows(GuidanceAction.Arrival) && m_State.WallStable;
        if (!Eligible() || !valid || !inside)
        { m_WallArrivalDecision=null; m_WallArrivalRelevant=null; return false; }
        if (m_WallArrivalDecision != null && !m_WallArrivalRelevant())
        { m_WallArrivalDecision=null; m_WallArrivalRelevant=null; }
        if (m_WallArrivalDecision != null)
        {
            var decision=m_WallArrivalDecision;
            var relevant=m_WallArrivalRelevant;
            m_Voice.TryAreaCorrection(decision.Phrase,relevant,() => {
                m_AdaptiveHistory.MarkWallArrivalSpoken();
                m_AdaptiveHistory.Spoken(decision.RequestId,"wall_arrival_override",Time.realtimeSinceStartupAsDouble,
                    Math.Sqrt(m_YawError*m_YawError+m_PitchError*m_PitchError),decision.Phrase);
                Commit(GuidanceAction.Arrival,true); m_LastTipTime=Time.time; m_CurrentHint=AreaCorrectionPhrase;
                Record(Decision("committed",AdaptiveGuidancePipeline.WallArrivalOverride,AreaCorrectionPhrase,
                    decision.Phrase,true,true,false,true,decision.RequestId));
                m_WallArrivalDecision=null; m_WallArrivalRelevant=null;
            },AdaptiveGuidancePipeline.WallArrivalOverride,decision.RequestId);
            return true;
        }
        double now=Time.realtimeSinceStartupAsDouble;
        if (now<m_NextWallArrivalRequest || !m_Voice.LibraryReady) return true;
        if (m_Navigation==null)m_Navigation=GetComponent<StudyNavigationVoiceBridge>();
        if (m_Navigation==null || m_Navigation.IsSelecting) return true;
        var phrases=new List<string>();
        foreach(var phrase in AdaptiveGuidancePipeline.WallArrivalPhrases())
            if(m_Voice.IsPreparedPhrase(phrase))phrases.Add(phrase);
        if(phrases.Count==0)return true;
        var target=m_EvaluatedTarget; long epoch=m_ActionPolicyEpoch;
        string context=HistoryIdentity+"|"+SessionConfig.AssistanceProvider+"|"+SessionConfig.Voice+"|"+
            (SessionConfig.Voice==VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId);
        bool Relevant() {
            Evaluate(out bool v,out bool i,out _); EvaluatePointing(v);
            string current=HistoryIdentity+"|"+SessionConfig.AssistanceProvider+"|"+SessionConfig.Voice+"|"+
                (SessionConfig.Voice==VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId);
            return context==current && epoch==m_ActionPolicyEpoch && Active() && Eligible() && v && i && m_EvaluatedTarget==target &&
                allows(GuidanceAction.Arrival) && !EvaluateLooking(v) && !(m_ControllerEvidenceValid && m_ControllerOnTarget);
        }
        string id=Guid.NewGuid().ToString("N");
        var state=new JObject { ["schema"]="adaptive-assistance-v1",["policy"]=JevDecisionClient.AdaptivePolicy,
            ["decision_id"]=id,["override_reason"]=AdaptiveGuidancePipeline.WallArrivalOverride,
            ["runtime"]=JObject.Parse(m_Voice.CaptureTransmissionContext()),["behavior"]=m_AdaptiveHistory.Snapshot(now) };
        m_NextWallArrivalRequest=now+1;
        m_Navigation.TryAdaptiveRequest(state,new Dictionary<string,IReadOnlyList<string>>{{"refine_explicit",phrases}},Relevant,decision => {
            if(!Relevant())return false;
            m_WallArrivalDecision=decision; m_WallArrivalRelevant=Relevant;
            Record(Decision("requested",AdaptiveGuidancePipeline.WallArrivalOverride,AreaCorrectionPhrase,
                decision.Phrase,true,true,false,true,id));
            return true;
        },wallArrival:true);
        return true;
    }

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

    public void ToggleAssistanceProvider()
    {
        if (m_GameManager != null && m_GameManager.SearchActive && !m_GameManager.ResearcherPaused) return;
        if (!SessionConfig.JevLedGuidance || !SessionConfig.TrySetLocalAdaptiveModel(!SessionConfig.LocalAdaptiveModel)) return;
        m_ActionPolicyEpoch++; m_AdaptiveSchedule.Reset(); m_AdaptiveHistory.BreakObservation();
        m_Navigation?.Cancel(); m_Navigation?.ResetAssistanceStatus(); m_Voice?.CancelPendingGuidance();
        m_Navigation?.RefreshDecisionProvider();
        PublishState(false);
        m_Voice?.RecordGuidanceState(CurrentStateSnapshot);
        m_Voice?.RecordNavigationStatus(new JObject { ["schema"] = "assistance-provider-change-v1",
            ["provider"] = SessionConfig.AssistanceProvider, ["policy"] = SessionConfig.GuidancePolicy,
            ["model_version"] = SessionConfig.LocalAdaptiveModel ? JevDecisionClient.LocalModelVersion : "typesafe-ai/jev",
            ["study_data"] = !SessionConfig.DevelopmentQuickTest, ["time"] = Time.realtimeSinceStartupAsDouble }.ToString(Newtonsoft.Json.Formatting.None));
    }

    string AdaptiveDirection(string strategy, GuidanceAction action, string direction) =>
        strategy == "correct" && action == GuidanceAction.ReturnBearing
            ? m_State.TargetHistory.PendingDirection(Time.realtimeSinceStartupAsDouble) : direction;

    // Unity supplies truthful, prepared alternatives; the second model decision selects wording.
    IReadOnlyList<string> AdaptivePhrases(string strategy, GuidanceAction action, string direction, bool inside)
    {
        direction = AdaptiveDirection(strategy, action, direction);
        string correction = null;
        if (strategy == "correct")
        {
            if (action == GuidanceAction.ReturnBearing)
                correction = AdaptiveGuidanceGrounding.BearingCorrection(direction, m_YawError, m_PitchError,
                    m_VerticalFraction, m_VisualReferenceVerticalSuppressed);
            else if (!string.IsNullOrEmpty(direction) && direction != DirectionalHintPolicy.AlignedPhrase)
                correction = action == GuidanceAction.ReturnTarget ? DirectionalHintPolicy.ReturnToTarget(direction) :
                    action == GuidanceAction.ReturnWall ? DirectionalHintPolicy.ReturnToArea(direction) : null;
        }
        var result = new List<string>();
        foreach (string phrase in AdaptiveGuidanceGrounding.Phrases(strategy, direction, inside, m_TurnRemaining,
            m_VerticalFraction, correction, m_AdaptiveHistory.HasRecentProgress(direction, Time.realtimeSinceStartupAsDouble)))
            if (m_Voice.IsPreparedPhrase(phrase)) result.Add(phrase);
        return result;
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
        var options = new Dictionary<string, IReadOnlyList<string>>();
        foreach (string strategy in new[] { "orient_brief","orient_explicit","refine_brief","refine_explicit","correct","encourage" })
        {
            var phrases = AdaptivePhrases(strategy, action, direction, inside);
            if (phrases.Count > 0) options.Add(strategy, phrases);
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
        bool requested = m_Navigation != null && m_Navigation.TryAdaptiveRequest(state,options,Relevant, decision => {
            if (!Relevant()) return false;
            if (!decision.Speak) { m_AdaptiveSchedule.Decided(Time.realtimeSinceStartupAsDouble,decision); return true; }
            string phrase = decision.Phrase;
            if (string.IsNullOrEmpty(phrase)) return false;
            bool StillCurrent() => Relevant() && new List<string>(AdaptivePhrases(decision.Strategy,action,direction,inside)).Contains(phrase);
            if (!StillCurrent()) return false;
            string spokenDirection = AdaptiveDirection(decision.Strategy,action,direction);
            var recorded = Decision("requested","adaptive:" + decision.Strategy,spokenDirection,phrase,true,inside,far,true,id);
            bool accepted = m_Voice.TryGuidanceTransmission(phrase, StillCurrent, () => {
                m_CurrentHint = spokenDirection; m_LastTipTime = Time.time; RememberDirection(spokenDirection); Commit(action,inside);
                m_AdaptiveHistory.Spoken(id,decision.Strategy,Time.realtimeSinceStartupAsDouble,
                    Math.Sqrt(m_YawError*m_YawError + m_PitchError*m_PitchError), phrase);
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
