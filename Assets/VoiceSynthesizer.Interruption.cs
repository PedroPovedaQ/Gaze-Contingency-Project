using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class VoiceSynthesizer
{
    [SerializeField] bool m_UseJevInterruptions = true;
    public Func<string, CancellationToken, Task<bool>> InterruptionAdvisor { private get; set; }
    public SpatialVoice.SpeechGuidanceEvidence GuidanceEvidence { get; } = new SpatialVoice.SpeechGuidanceEvidence();
    string m_SpeechStartStage = "unknown";
    long m_PlaybackGeneration, m_AdviceAttemptGeneration = -1;
    double m_LastSmoothInterruption = -100, m_AdviceStarted;
    bool m_DropCorrection;
    double m_CurrentSpeechSeconds = -1; // Excludes the clone's effect tail, matching the normalization speech window.
    double SpeechRemaining => (m_CurrentSpeechSeconds > 0 ? m_CurrentSpeechSeconds : m_AudioSource.clip.length) - m_AudioSource.time;
    string m_AdvicePhrase, m_AdviceReason;
    Func<bool> m_AdviceRelevant;
    Task<bool> m_Advice;
    CancellationTokenSource m_AdviceCancellation;

    /// <summary>Local limits are authoritative. Only actual guidance playback can be cut;
    /// short endings, confirmations, setup, success and lifecycle changes never go to Jev.</summary>
    public bool CanConsiderGuidanceInterruption => m_UseJevInterruptions && InterruptionAdvisor != null &&
        m_InterruptionCoroutine == null && IsSpeaking && m_AudioSource.clip != null &&
        (m_CurrentContext == "tip" || m_CurrentContext == "guidance_repeat") &&
        SpatialVoice.SpeechPhraseMetadata.Describe(m_CurrentPhrase).interruptPolicy == "replace_if_misleading" &&
        GuidanceSpeechPolicy.CanConsiderInterruption(GuidanceSpeechPolicy.IsAcknowledgment(m_CurrentPhrase) ||
            m_CurrentPhrase == DirectionalHintPolicy.SelectionReminderPhrase,
            m_AudioSource.time, SpeechRemaining,
            Time.realtimeSinceStartupAsDouble, m_LastSmoothInterruption);

    bool ApproveInterruption(string phrase, string reason, Func<bool> stillRelevant)
    {
        if (!CanConsiderGuidanceInterruption || !GuidanceSpeechPolicy.IsCorrectionEvent(reason) ||
            GuidanceSpeechPolicy.IsRedundantArrival(reason, m_SpeechStartStage) || phrase == m_CurrentPhrase ||
            !SpatialVoice.SpeechPhraseMetadata.CanReplace(m_CurrentPhrase, phrase)) return false;
        if (m_Advice != null)
        {
            if (phrase != m_AdvicePhrase || reason != m_AdviceReason || !ValidateInterruptionAdvice())
            { CancelInterruptionAdvice(); return false; }
            if (!m_Advice.IsCompleted) return false; // Playback continues; never block Unity's tick.
            bool approved = false;
            try { approved = m_Advice.GetAwaiter().GetResult(); }
            catch (Exception e) { AdviceTelemetry("finish_on_" + e.GetType().Name); }
            AdviceTelemetry(approved ? "fade_approved" : "finish_phrase");
            CancelInterruptionAdvice();
            return approved && CanConsiderGuidanceInterruption && stillRelevant();
        }
        if (m_AdviceAttemptGeneration == m_PlaybackGeneration) return false;
        // At most one request for this utterance, even if the gaze changes every frame.
        m_AdviceAttemptGeneration = m_PlaybackGeneration;
        m_AdvicePhrase = phrase; m_AdviceReason = reason; m_AdviceRelevant = stillRelevant;
        m_AdviceStarted = Time.realtimeSinceStartupAsDouble;
        m_AdviceCancellation = new CancellationTokenSource();
        m_AdviceCancellation.CancelAfter(TimeSpan.FromSeconds(GuidanceSpeechPolicy.AdviceDeadline));
        string state = SpatialVoice.SpeechInterruptionState.Create(m_CurrentPhrase, phrase, reason,
            m_AudioSource.time, SpeechRemaining, GuidanceEvidence, m_CurrentContext, m_SpeechStartStage);
        AdviceTelemetry("requested");
        try { m_Advice = InterruptionAdvisor(state, m_AdviceCancellation.Token); }
        catch (Exception e) { AdviceTelemetry("finish_on_" + e.GetType().Name); CancelInterruptionAdvice(); }
        return false;
    }

    bool ValidateInterruptionAdvice()
    {
        if (m_Advice == null) return false;
        if (!CanConsiderGuidanceInterruption || m_AdviceAttemptGeneration != m_PlaybackGeneration ||
            Time.realtimeSinceStartupAsDouble - m_AdviceStarted > GuidanceSpeechPolicy.AdviceDeadline ||
            m_AdviceRelevant == null || !m_AdviceRelevant())
        {
            AdviceTelemetry("discarded_stale_or_expired");
            CancelInterruptionAdvice(); return false;
        }
        return true;
    }

    void AdviceTelemetry(string result) => Telemetry?.Invoke("interruption_advice", m_CurrentContext ?? "tip",
        m_ActiveClipKey ?? "", "generation=" + m_PlaybackGeneration + ";result=" + result +
        ";reason=" + m_AdviceReason + ";proposal=" + m_AdvicePhrase + ";elapsed_ms=" +
        ((Time.realtimeSinceStartupAsDouble - m_AdviceStarted) * 1000).ToString("F0", CultureInfo.InvariantCulture));

    void CancelInterruptionAdvice()
    {
        m_AdviceCancellation?.Cancel(); m_AdviceCancellation?.Dispose(); m_AdviceCancellation = null;
        // Observe eventual faults even when a provider ignores cancellation. No Unity work here.
        if (m_Advice != null) _ = m_Advice.ContinueWith(t => { var ignored = t.Exception; },
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        m_Advice = null; m_AdviceRelevant = null; m_AdvicePhrase = m_AdviceReason = null;
    }
}
