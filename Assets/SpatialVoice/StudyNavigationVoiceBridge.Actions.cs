using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpatialVoice
{
    public sealed partial class StudyNavigationVoiceBridge
    {
        public bool TryActionRequest(IReadOnlyList<NavigationPhrase> choices, Func<bool> relevant,
            Func<GuidanceSelection, bool> commit)
        {
            if (!SessionConfig.JevLedGuidance || IsSelecting || choices.Count == 0 ||
                !(m_Decisions is INavigationActionDecisionProvider provider) || !m_Decisions.Available || !relevant()) return false;
            StartActionRequest(provider, choices, relevant, commit);
            return true;
        }
        async void StartActionRequest(INavigationActionDecisionProvider provider, IReadOnlyList<NavigationPhrase> choices,
            Func<bool> relevant, Func<GuidanceSelection, bool> commit)
        {
            using (var cancellation = m_Voice.GuidanceScheduler.Begin(GuidanceRequestKind.Catalog))
            {
                var token = cancellation.Token;
                m_Pending = cancellation;
                var condition = SessionConfig.Voice;
                string voiceId = condition == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
                int round = m_Game.CurrentObjectiveIndex;
                m_Relevant = () => this != null && isActiveAndEnabled && SessionConfig.JevLedGuidance &&
                    m_Voice != null && m_Voice.GuidanceReady && condition == SessionConfig.Voice &&
                    voiceId == (condition == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId) &&
                    round == m_Game.CurrentObjectiveIndex && relevant();
                string id = Guid.NewGuid().ToString("N");
                var selected = new GuidanceSelection { Phrase = choices[0], Speak = true, Source = "local_fallback", RequestId = id };
                try
                {
                    await System.Threading.Tasks.Task.Yield();
                    if (!m_Relevant()) return;
                    string state = new JObject { ["schema"] = "development-action-v1", ["decision_id"] = id,
                        ["policy"] = NavigationActionMenu.Policy,
                        ["runtime"] = JObject.Parse(m_Voice.CaptureTransmissionContext()) }.ToString(Formatting.None);
                    try { selected = await provider.ChooseActionAsync(state, choices, token); }
                    catch (Exception e) when (!cancellation.IsCancellationRequested)
                    { selected.Source = "local_fallback_" + e.GetType().Name; }
                    token.ThrowIfCancellationRequested();
                    bool offered = false;
                    foreach (var choice in choices)
                        if (selected != null && selected.Phrase.id == choice.id && selected.Phrase.text == choice.text) offered = true;
                    bool accepted = offered && m_Relevant() && commit(selected);
                    m_Voice.RecordNavigationStatus(new JObject { ["schema"] = "development-action-result-v1",
                        ["policy"] = NavigationActionMenu.Policy, ["decision_id"] = id,
                        ["action"] = selected?.Speak == true ? selected.Phrase.id : "wait",
                        ["phrase"] = selected?.Phrase.text, ["source"] = selected?.Source,
                        ["accepted_for_scheduling"] = accepted, ["actual_playback"] = false }.ToString(Formatting.None));
                    if (!accepted) RecordDiscard(id);
                }
                catch (OperationCanceledException) { RecordDiscard(id); }
                catch (Exception e) { Debug.LogWarning("[SpatialVoice] Action selection failed: " + e.GetType().Name); RecordDiscard(id); }
                finally
                {
                    m_Voice.GuidanceScheduler.Complete(GuidanceRequestKind.Catalog, cancellation);
                    m_Pending = null; m_Relevant = null;
                }
            }
        }
    }
}
