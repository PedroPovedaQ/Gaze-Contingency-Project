using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class VoiceSynthesizer
{
    [SerializeField] bool m_UseJevInterruptions = true;
    public Func<string, CancellationToken, Task<bool>> InterruptionAdvisor { private get; set; }
    public SpatialVoice.SpeechGuidanceEvidence GuidanceEvidence { get; private set; } = new SpatialVoice.SpeechGuidanceEvidence();
    public void ClearGuidanceEvidence() { CancelPendingGuidance(); m_LastDirectionKey = null; m_LastSpokenPhrase = null;
        m_DecisionMemory.Clear(); GuidanceEvidence = new SpatialVoice.SpeechGuidanceEvidence(); }
    // Phase transitions invalidate requests, not a sentence already playing.
    public void CancelPendingGuidance()
    {
        bool queued = GuidanceScheduler.PlaybackQueued;
        GuidanceScheduler.CancelAll();
        CancelTransmission(); CancelInterruptionAdvice();
        if (queued && m_SpeakCoroutine != null && !IsSpeaking)
        {
            AudioTelemetry("guidance_decision_aborted", m_CurrentContext ?? "tip", m_ActiveClipKey ?? "", "reason=phase_changed_before_playback");
            Stop(); // Only queued guidance: never stop an already playing sentence.
        }
    }
    // Fresh controller evidence makes search directions misleading immediately,
    // before the pointing hold qualifies. Fade only search speech, never turn an
    // unqualified hit into an acknowledgment or cut setup/success/confirmation.
    public void SuppressSearchGuidanceForControllerTarget()
    {
        var semantics = SpatialVoice.SpeechPhraseMetadata.Describe(m_CurrentPhrase);
        if (!IsSpeaking || (m_CurrentContext != "tip" && m_CurrentContext != "guidance_repeat") ||
            (semantics.interruptPolicy != "replace_if_misleading" && semantics.intent != "progress_encouragement")) return;
        m_DropCorrection = true;
        CancelInterruptionAdvice();
        if (m_InterruptionCoroutine == null)
            m_InterruptionCoroutine = StartCoroutine(FadeToAreaCorrection(null, null, null, "controller_target_search_suppressed", null));
    }
    public void RecordGuidanceState(GuidanceStateSnapshot state)
    {
        m_DecisionMemory.Observe(state);
        Telemetry?.Invoke("guidance_state", "tip", "", JsonUtility.ToJson(state));
    }
    readonly SpatialVoice.SpeechDecisionMemory m_DecisionMemory = new SpatialVoice.SpeechDecisionMemory();
    string m_SpeechStartStage = "unknown";
    string m_LastSpokenPhrase;

    Newtonsoft.Json.Linq.JObject CapturePlaybackState()
    {
        bool speaking = IsSpeaking && m_AudioSource != null && m_AudioSource.clip != null;
        return new Newtonsoft.Json.Linq.JObject {
            ["captured_at_seconds"] = Time.realtimeSinceStartupAsDouble,
            ["is_speaking"] = speaking, ["is_busy"] = IsBusy && m_TransmissionCoroutine == null,
            ["transmission_decision_pending"] = m_TransmissionCoroutine != null,
            ["current_phrase"] = speaking ? m_CurrentPhrase : null,
            ["pending_phrase"] = m_TransmissionPhrase ?? (!speaking && IsBusy ? m_CurrentPhrase : null),
            ["last_spoken_phrase"] = m_LastSpokenPhrase,
            ["recent_history"] = m_DecisionMemory.Snapshot(Time.realtimeSinceStartupAsDouble),
            ["context"] = m_CurrentContext, ["started_in_stage"] = speaking ? m_SpeechStartStage : null,
            ["elapsed_seconds"] = speaking ? (double?)m_AudioSource.time : null,
            ["speech_remaining_seconds"] = speaking ? (double?)Math.Max(0, SpeechRemaining) : null,
            ["word_boundary_known"] = false,
            ["fade_in_progress"] = m_InterruptionCoroutine != null,
            ["semantics"] = Newtonsoft.Json.Linq.JObject.FromObject(SpatialVoice.SpeechPhraseMetadata.Describe(speaking ? m_CurrentPhrase : null))
        };
    }

    // Freeze values on Unity's thread before an asynchronous provider call.
    public string CaptureDecisionContext() => new Newtonsoft.Json.Linq.JObject {
        ["captured_at_seconds"] = Time.realtimeSinceStartupAsDouble,
        ["guidance_policy"] = SessionConfig.GuidancePolicy,
        ["evidence"] = GuidanceEvidence.Snapshot(), ["playback"] = CapturePlaybackState()
    }.ToString(Newtonsoft.Json.Formatting.None);
    long m_PlaybackGeneration, m_AdviceAttemptGeneration = -1;
    double m_LastSmoothInterruption = -100, m_AdviceStarted;
    bool m_DropCorrection;
    double m_CurrentSpeechSeconds = -1; // Excludes the clone's effect tail, matching the normalization speech window.
    double SpeechRemaining => (m_CurrentSpeechSeconds > 0 ? m_CurrentSpeechSeconds : m_AudioSource.clip.length) - m_AudioSource.time;
    string m_AdvicePhrase, m_AdviceReason, m_AdviceDecisionId;
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
            DirectionalHintPolicy.IsSelectionReminder(m_CurrentPhrase),
            m_AudioSource.time, SpeechRemaining,
            Time.realtimeSinceStartupAsDouble, m_LastSmoothInterruption);

    bool ApproveInterruption(string phrase, string reason, Func<bool> stillRelevant, ref string decisionId)
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
            decisionId = m_AdviceDecisionId;
            CancelInterruptionAdvice();
            return approved && CanConsiderGuidanceInterruption && stillRelevant();
        }
        if (m_AdviceAttemptGeneration == m_PlaybackGeneration) return false;
        // At most one request for this utterance, even if the gaze changes every frame.
        m_AdviceAttemptGeneration = m_PlaybackGeneration;
        m_AdvicePhrase = phrase; m_AdviceReason = reason; m_AdviceRelevant = stillRelevant;
        m_AdviceStarted = Time.realtimeSinceStartupAsDouble;
        m_AdviceCancellation = GuidanceScheduler.Begin(GuidanceRequestKind.Interruption);
        m_AdviceCancellation.CancelAfter(TimeSpan.FromSeconds(GuidanceSpeechPolicy.AdviceDeadline));
        string state = SpatialVoice.SpeechInterruptionState.Create(m_CurrentPhrase, phrase, reason,
            m_AudioSource.time, SpeechRemaining, GuidanceEvidence, m_CurrentContext, m_SpeechStartStage, CapturePlaybackState());
        var traceState = Newtonsoft.Json.Linq.JObject.Parse(state);
        traceState["decision_id"] = decisionId;
        state = traceState.ToString(Newtonsoft.Json.Formatting.None);
        m_AdviceDecisionId = decisionId;
        Telemetry?.Invoke("interruption_state", m_CurrentContext ?? "tip", m_ActiveClipKey ?? "", state);
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
        m_ActiveClipKey ?? "", "decision_id=" + m_AdviceDecisionId + ";generation=" + m_PlaybackGeneration + ";result=" + result +
        ";reason=" + m_AdviceReason + ";proposal=" + m_AdvicePhrase + ";elapsed_ms=" +
        ((Time.realtimeSinceStartupAsDouble - m_AdviceStarted) * 1000).ToString("F0", CultureInfo.InvariantCulture));

    void CancelInterruptionAdvice()
    {
        GuidanceScheduler.Cancel(GuidanceRequestKind.Interruption); m_AdviceCancellation = null;
        // Observe eventual faults even when a provider ignores cancellation. No Unity work here.
        if (m_Advice != null) _ = m_Advice.ContinueWith(t => { var ignored = t.Exception; },
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        m_Advice = null; m_AdviceRelevant = null; m_AdvicePhrase = m_AdviceReason = null;
    }
}
