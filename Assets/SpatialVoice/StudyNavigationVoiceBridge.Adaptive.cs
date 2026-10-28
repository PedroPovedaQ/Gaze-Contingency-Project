using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpatialVoice
{
    public sealed partial class StudyNavigationVoiceBridge
    {
        JevDecisionClient m_LocalDecisions;
        string m_LastDecisionContext, m_LastDecisionId, m_LastDecisionProvider, m_LastStrategy, m_LastOutcome;
        double m_LastDecisionAt;
        string AssistanceContext => SessionConfig.ParticipantId + ":" + SessionConfig.RunNumber + ":" +
            (m_Game != null ? m_Game.CurrentObjectiveIndex : -1) + ":" + SessionConfig.AssistanceProvider + ":" +
            SessionConfig.Voice + ":" + (SessionConfig.Voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId);
        public void ResetAssistanceStatus() { m_LastDecisionContext = null; }
        public AssistanceRuntimeSnapshot CaptureAssistanceStatus()
        {
            var state = SessionConfig.CaptureAssistanceSelection();
            state.capturedAtSeconds = Time.realtimeSinceStartupAsDouble;
            if (SessionConfig.JevLedGuidance)
                state.connection = SessionConfig.LocalAdaptiveModel
                    ? (m_LocalReady ? "ready" : m_LocalChecking ? "checking" : m_LocalDecisions == null ? "not_checked" : "unavailable")
                    : (m_Decisions != null && m_Decisions.Available ? "configured" : "not_configured");
            if (SessionConfig.JevLedGuidance && m_LastDecisionContext == AssistanceContext)
            {
                state.lastDecisionId = m_LastDecisionId; state.lastDecisionProvider = m_LastDecisionProvider;
                state.lastStrategy = m_LastStrategy; state.lastOutcome = m_LastOutcome;
                state.lastDecisionAtSeconds = m_LastDecisionAt;
            }
            return state;
        }
        bool m_LocalReady, m_LocalChecking;
        double m_NextLocalCheck;
        public string LocalModelStatus { get; private set; } = "Connect Mac";
        void UpdateLocalConnection()
        {
            if (!SessionConfig.LocalAdaptiveModel || !SessionConfig.JevLedGuidance || m_LocalChecking ||
                IsSelecting || Time.realtimeSinceStartupAsDouble < m_NextLocalCheck) return;
            if (m_LocalDecisions == null)
            {
                m_LocalDecisions = JevDecisionClient.CreateLocalAdaptive();
                m_LocalDecisions.Trace += m_Voice.RecordJevTrace;
            }
            CheckLocalConnection();
        }
        async void CheckLocalConnection()
        {
            m_LocalChecking = true;
            if (!m_LocalReady) LocalModelStatus = "Connecting…";
            try
            {
                using (var cancellation = new CancellationTokenSource())
                    await m_LocalDecisions.CheckLocalConnectionAsync(cancellation.Token);
                m_LocalReady = true; LocalModelStatus = "Ready";
            }
            catch (Exception) { m_LocalReady = false; LocalModelStatus = "Unavailable · check Mac"; }
            finally { m_LocalChecking = false; m_NextLocalCheck = Time.realtimeSinceStartupAsDouble + 5; }
        }
        public bool TryAdaptiveRequest(JObject state, IReadOnlyDictionary<string, IReadOnlyList<string>> options, Func<bool> relevant,
            Func<AdaptiveGuidanceDecision, bool> apply, bool wallArrival = false)
        {
            var decisions = SessionConfig.LocalAdaptiveModel ? (INavigationDecisionProvider)m_LocalDecisions : m_Decisions;
            if (IsSelecting || (SessionConfig.LocalAdaptiveModel && !m_LocalReady) ||
                !(decisions is IAdaptiveGuidanceProvider provider) || !decisions.Available || !relevant()) return false;
            StartAdaptiveRequest(provider, state, options, relevant, apply, wallArrival);
            return true;
        }
        async void StartAdaptiveRequest(IAdaptiveGuidanceProvider provider, JObject state, IReadOnlyDictionary<string, IReadOnlyList<string>> options,
            Func<bool> relevant, Func<AdaptiveGuidanceDecision,bool> apply, bool wallArrival)
        {
            using (var cancellation = m_Voice.GuidanceScheduler.Begin(GuidanceRequestKind.Catalog))
            {
                m_Pending = cancellation;
                var token = cancellation.Token;
                string providerName = SessionConfig.AssistanceProvider;
                var voice = SessionConfig.Voice;
                string identity = voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
                string participant = SessionConfig.ParticipantId;
                int round = m_Game.CurrentObjectiveIndex;
                double started = Time.realtimeSinceStartupAsDouble;
                bool Current() => this != null && isActiveAndEnabled && !token.IsCancellationRequested &&
                    SessionConfig.JevLedGuidance && providerName == SessionConfig.AssistanceProvider && m_Voice != null &&
                    (wallArrival ? m_Voice.LibraryReady : m_Voice.GuidanceReady) &&
                    voice == SessionConfig.Voice && identity == (voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId) &&
                    participant == SessionConfig.ParticipantId && round == m_Game.CurrentObjectiveIndex &&
                    Time.realtimeSinceStartupAsDouble - started <= 3.25 && relevant();
                m_Relevant = Current;
                string id = (string)state["decision_id"];
                try
                {
                    await Task.Yield();
                    if (!Current()) { RecordDiscard(id); return; }
                    cancellation.CancelAfter(2 * JevDecisionClient.AdaptiveDeadlineMilliseconds);
                    var decision = wallArrival
                        ? await AdaptiveGuidancePipeline.ChooseWallArrivalAsync(provider,state,options["refine_explicit"],Current,token)
                        : await AdaptiveGuidancePipeline.ChooseAsync(provider, state, options, Current, token);
                    token.ThrowIfCancellationRequested();
                    if (!Current()) { RecordDiscard(id); return; }
                    bool accepted = apply(decision);
                    if (accepted)
                    {
                        m_LastDecisionContext = AssistanceContext; m_LastDecisionId = id; m_LastDecisionProvider = providerName;
                        m_LastStrategy = decision.Strategy; m_LastOutcome = decision.Speak ? "queued" : "observing";
                        m_LastDecisionAt = Time.realtimeSinceStartupAsDouble;
                    }
                    m_Voice.RecordJevTrace(new JObject { ["schema"] = "jev-trace-v1", ["request_id"] = id,
                        ["stage"] = "application", ["outcome"] = accepted ? (decision.Speak ? "scheduled" : "observing") : "discarded_stale",
                        ["policy"] = JevDecisionClient.AdaptivePolicy, ["provider"] = providerName, ["strategy"] = decision.Strategy,
                        ["phrase_request_id"] = decision.PhraseRequestId, ["phrase"] = decision.Phrase,
                        ["override_reason"] = wallArrival ? AdaptiveGuidancePipeline.WallArrivalOverride : null,
                        ["reassess_after_seconds"] = decision.ReassessSeconds, ["actual_playback"] = false }.ToString(Formatting.None));
                }
                catch (OperationCanceledException) { RecordDiscard(id); }
                catch (Exception e)
                {
                    if (providerName == "local_minilm") { m_LocalReady = false; LocalModelStatus = "Unavailable · check Mac"; }
                    m_Voice?.RecordJevTrace(new JObject { ["schema"] = "jev-trace-v1", ["request_id"] = id,
                        ["stage"] = "application", ["outcome"] = "unavailable_observe_retry", ["error_type"] = e.GetType().Name,
                        ["policy"] = JevDecisionClient.AdaptivePolicy }.ToString(Formatting.None));
                }
                finally
                {
                    m_Voice?.GuidanceScheduler.Complete(GuidanceRequestKind.Catalog,cancellation);
                    if (ReferenceEquals(m_Pending,cancellation)) { m_Pending = null; m_Relevant = null; }
                }
            }
        }
    }
}
