using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SpatialVoice;

public partial class VoiceAssistantController
{
    JevDecisionClient m_EncouragementClient;
    CancellationTokenSource m_SuccessCancellation;
    RoundPerformance m_RoundPerformance;
    int m_PerformanceRound;
    bool m_PerformancePractice;
    string m_LastEncouragement, m_EncouragementDecisionId;
    bool m_EncouragementSessionStopped;

    void BeginRoundPerformance(int round)
    {
        CancelSuccessFeedback();
        m_PerformanceRound = m_GameManager.PresentedRoundIndex;
        m_PerformancePractice = m_GameManager.PresentedRoundIsPractice;
        m_RoundPerformance = new RoundPerformance { measurementsAvailable = true, practice = m_PerformancePractice };
        if (!m_PerformancePractice && m_GameManager.RotationalBetaEnabled && round >= 0 && round < ChallengeSet.Rounds.Length)
            m_RoundPerformance.plannedAbsoluteTurnDegrees = ChallengeSet.Rounds[round].AbsoluteTheta;
    }

    void TrackRoundSpeech(string kind, string context, string clip, string detail)
    {
        if (m_RoundPerformance == null || m_GameManager == null || !m_GameManager.SearchActive) return;
        if (kind == "guidance_repeat_requested") m_RoundPerformance.repeatRequests++;
        if (kind == "audio_playback_start" && (context == "tip" || context == "guidance_repeat"))
            m_RoundPerformance.guidanceStarts++;
    }

    RoundPerformance CaptureRoundPerformance()
    {
        bool available = m_RoundPerformance != null && m_GameManager != null &&
            m_PerformanceRound == m_GameManager.PresentedRoundIndex &&
            m_PerformancePractice == m_GameManager.PresentedRoundIsPractice;
        int trial = m_GameManager != null ? m_GameManager.PresentedRoundIndex : -1;
        bool practice = m_GameManager != null && m_GameManager.PresentedRoundIsPractice;
        return new RoundPerformance {
            measurementsAvailable = available, practice = m_GameManager != null && m_GameManager.PresentedRoundIsPractice,
            searchSeconds = available ? m_GameManager.CurrentSearchSeconds : 0,
            incorrectSelections = available ? m_RoundPerformance.incorrectSelections : 0,
            guidanceStarts = available ? m_RoundPerformance.guidanceStarts : 0,
            repeatRequests = available ? m_RoundPerformance.repeatRequests : 0,
            plannedAbsoluteTurnDegrees = available ? m_RoundPerformance.plannedAbsoluteTurnDegrees : null,
            trialNumber = trial >= 0 ? (int?)(trial + 1) : null,
            blockNumber = !practice && trial >= 0 && SessionConfig.VoiceBlocksEnabled ? (int?)(trial / ChallengeSet.RoundsPerBlock + 1) : null,
            trialInBlock = !practice && trial >= 0 && SessionConfig.VoiceBlocksEnabled ? (int?)(trial % ChallengeSet.RoundsPerBlock + 1) : null,
            previousFeedback = m_LastEncouragement
        };
    }

    Task<EncouragementPhrase> RequestRoundEncouragement(RoundPerformance facts,
        IReadOnlyList<EncouragementPhrase> candidates, CancellationToken token)
    {
        m_EncouragementDecisionId = Guid.NewGuid().ToString("N");
        if (m_EncouragementClient == null || !m_EncouragementClient.Available || candidates.Count < 2)
            return Task.FromResult<EncouragementPhrase>(null);
        var task = m_EncouragementClient.ChooseEncouragementAsync(facts, candidates, token, m_EncouragementDecisionId);
        // Observe faults even if disable/session-stop disposes the coroutine while advice is pending.
        _ = task.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    void RecordEncouragement(RoundPerformance facts, EncouragementPhrase selected, string status)
    {
        m_VoiceSynthesizer?.RecordRoundEncouragement(new JObject {
            ["schema"] = RoundEncouragement.Version, ["decision_id"] = m_EncouragementDecisionId, ["status"] = selected == null ? "no_prepared_phrase" : status,
            ["round_performance"] = JObject.FromObject(facts),
            ["phrase_id"] = selected?.id, ["text"] = selected?.text,
            ["time"] = UnityEngine.Time.realtimeSinceStartupAsDouble
        }.ToString(Newtonsoft.Json.Formatting.None));
    }

    void StopRoundEncouragement(string reason)
    {
        m_EncouragementSessionStopped = true; m_RoundPerformance = null;
        CancelSuccessFeedback();
    }
    void CancelSuccessFeedback()
    {
        m_SuccessCancellation?.Cancel(); m_SuccessCancellation?.Dispose(); m_SuccessCancellation = null;
        if (m_SuccessRoutine != null) StopCoroutine(m_SuccessRoutine);
        if (m_EffectAudioSource != null) m_EffectAudioSource.Stop();
        m_SuccessRoutine = null; SuccessFeedbackBusy = false;
    }
}
