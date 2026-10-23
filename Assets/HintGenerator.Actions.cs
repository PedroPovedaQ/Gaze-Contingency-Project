using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SpatialVoice;
using UnityEngine;

public partial class HintGenerator
{
    readonly GuidanceTurnState m_ActionProgress = new GuidanceTurnState();
    long m_ActionPolicyEpoch;

    public void ToggleDevelopmentGuidancePolicy()
    {
        if (m_GameManager != null && m_GameManager.SearchActive && !m_GameManager.ResearcherPaused) return;
        if (!SessionConfig.TrySetJevLedGuidance(!SessionConfig.JevLedGuidance)) return;
        m_ActionPolicyEpoch++; m_ActionProgress.Reset();
        m_Navigation?.Cancel(); m_Voice?.CancelPendingGuidance();
        PublishState(false);
        m_Voice?.RecordNavigationStatus(new JObject { ["schema"] = "guidance-policy-change-v1",
            ["policy"] = SessionConfig.GuidancePolicy, ["study_data"] = false,
            ["time"] = Time.realtimeSinceStartupAsDouble }.ToString(Newtonsoft.Json.Formatting.None));
    }

    List<NavigationPhrase> ActionMenu(GuidanceAction action, string direction, bool inside, bool far, bool includeBlockedRepeats = false)
    {
        string direct = inside ? DirectionalHintPolicy.Variant(direction, m_Variant, far: far, verticalFraction: m_VerticalFraction) :
            DirectionalHintPolicy.Turn(direction, m_TurnRemaining, m_Variant);
        string primary = action == GuidanceAction.ReturnTarget ? DirectionalHintPolicy.ReturnToTarget(direction) :
            action == GuidanceAction.ReturnBearing ? DirectionalHintPolicy.PassedTarget(direction, m_VerticalFraction) :
            action == GuidanceAction.ReturnWall ? DirectionalHintPolicy.ReturnToArea(direction) : direct;
        return NavigationActionMenu.Build(action.ToString(), primary, direct, DirectionalHintPolicy.Progress(direction, 0),
            inside, m_ActionProgress.BriefProgressAllowed(direction, Time.realtimeSinceStartupAsDouble),
            phrase => includeBlockedRepeats ? m_Voice.IsPreparedPhrase(phrase) :
                m_Voice.CanOfferGuidancePhrase(phrase, action == GuidanceAction.Direction ? "jev_action" : "jev_action_correction"));
    }

    // Local acknowledgments, arrival/milestones and busy-audio interruption paths run unchanged.
    // Only idle, state-qualified search actions are offered to the experimental adviser.
    bool TryDevelopmentAction(string direction, bool valid, bool inside, bool far, bool looking, Func<GuidanceAction,bool> allows)
    {
        var action = m_State.Action;
        if (direction == null || direction == DirectionalHintPolicy.AlignedPhrase) return false;
        if (!NavigationActionMenu.CanUse(SessionConfig.JevLedGuidance, action.ToString(), valid, looking,
            m_ControllerEvidenceValid && m_ControllerOnTarget, m_State.Stable, m_Voice.GuidanceReady)) return false;
        if (m_Navigation == null) m_Navigation = GetComponent<StudyNavigationVoiceBridge>();
        if (m_Navigation == null) return false;
        if (m_Navigation.IsSelecting) return true;
        string reason = action == GuidanceAction.Direction ? "jev_action" : "jev_action_correction";
        if (Time.time - m_LastTipTime < k_TipInterval || !m_Voice.CanRequestGuidance(reason)) return true;
        var target = m_EvaluatedTarget;
        long epoch = m_ActionPolicyEpoch;
        var choices = ActionMenu(action, direction, inside, far);
        // No prepared action: allow the existing local/catalog path to recover.
        // Prepared but repeat-blocked actions: wait without spending a request.
        if (choices.Count == 0) return ActionMenu(action, direction, inside, far, includeBlockedRepeats: true).Count > 0;
        bool Relevant()
        {
            string current = Evaluate(out bool v, out bool i, out _);
            EvaluatePointing(v);
            if (epoch != m_ActionPolicyEpoch || !SessionConfig.JevLedGuidance || !Active() || !v || i != inside ||
                m_EvaluatedTarget != target || !allows(action) || !m_State.Stable || EvaluateLooking(v) ||
                (m_ControllerEvidenceValid && m_ControllerOnTarget) || current != direction) return false;
            if (action == GuidanceAction.ReturnTarget && !m_State.RevisitPending(Time.realtimeSinceStartupAsDouble)) return false;
            if (action == GuidanceAction.ReturnWall && !m_State.WallReturnPending(Time.realtimeSinceStartupAsDouble)) return false;
            return action != GuidanceAction.ReturnBearing ||
                (m_State.PendingBearingDirection(Time.realtimeSinceStartupAsDouble, m_VisualReferenceVerticalSuppressed) == direction &&
                GazeTargetHistory.StillBeyond(direction, m_YawError, m_PitchError));
        }
        bool StillOffered(NavigationPhrase selected)
        {
            if (!Relevant()) return false;
            Evaluate(out _, out _, out bool currentFar);
            foreach (var candidate in ActionMenu(action, direction, inside, currentFar))
                if (candidate.id == selected.id && candidate.text == selected.text) return true;
            return false;
        }
        return m_Navigation.TryActionRequest(choices, Relevant, selection => {
            if (!StillOffered(selection.Phrase)) return false;
            string text = selection.Phrase.text;
            var decision = Decision("requested", reason + ":" + selection.Phrase.id, direction, text, true, inside, far, true);
            decision.decisionId = selection.RequestId ?? decision.decisionId;
            bool accepted = m_Voice.TryGuidanceTransmission(text, () => StillOffered(selection.Phrase), () => {
                m_CurrentHint = direction; m_LastTipTime = Time.time; m_Variant++;
                RememberDirection(direction); Commit(action, inside);
                Record(Decision("committed", reason + ":" + selection.Phrase.id, direction, text, true, inside, far, true, decision.decisionId));
            }, reason, decision.decisionId, selection.Speak, selection.Source);
            if (accepted) Record(decision);
            return accepted;
        });
    }
}
