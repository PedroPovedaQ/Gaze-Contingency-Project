using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

public partial class VoiceSynthesizer
{
    public Func<string, CancellationToken, Task<bool>> TransmissionAdvisor { private get; set; }
    Coroutine m_TransmissionCoroutine;
    CancellationTokenSource m_TransmissionCancellation;
    string m_WaitReason;
    string m_TransmissionPhrase;
    long m_TransmissionEpoch;
    public bool UsesTransmissionAdvice => TransmissionAdvisor != null;
    double m_WaitUntil;
    double m_DeferredSince = -1;

    public bool CanRequestGuidance(string reason) => GuidanceReady &&
        !(reason == m_WaitReason && Time.realtimeSinceStartupAsDouble < m_WaitUntil);

    // Automatic guidance only. Setup, explicit repeat requests and success feedback
    // keep their own lifecycle. An accepted request is not yet an audible commitment.
    public bool TryGuidanceTransmission(string phrase, Func<bool> relevant, Action committed,
        string reason, string decisionId, bool? selectedSpeak = null, string selectionSource = null)
    {
        if (!CanRequestGuidance(reason) || !IsPreparedPhrase(phrase) || !DirectionRepeatAllowed(phrase, reason) || !relevant()) return false;
        m_TransmissionPhrase = phrase;
        m_TransmissionCoroutine = StartCoroutine(ConsiderTransmission(phrase, relevant, committed, reason, decisionId, selectedSpeak, selectionSource));
        return true;
    }

    // Captured on Unity's thread; reuse the same playback and evidence schema in the
    // joint catalog request. Deferral time remains owned by this synthesizer.
    public string CaptureTransmissionContext()
    {
        var state = JObject.Parse(CaptureDecisionContext());
        double now = Time.realtimeSinceStartupAsDouble;
        state["seconds_since_speech_ended"] = SpeechSilence(now);
        state["maximum_deferral_seconds"] = GuidanceSpeechPolicy.MaximumTransmissionDeferralSeconds;
        state["deferred_seconds"] = m_DeferredSince < 0 ? 0 : now - m_DeferredSince;
        state["locally_qualified"] = true;
        return state.ToString(Newtonsoft.Json.Formatting.None);
    }

    IEnumerator ConsiderTransmission(string phrase, Func<bool> relevant, Action committed, string reason, string id,
        bool? selectedSpeak, string selectionSource)
    {
        long generation = m_PlaybackGeneration;
        long epoch = m_TransmissionEpoch;
        var voice = SessionConfig.Voice;
        string identity = voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
        bool Current() => epoch == m_TransmissionEpoch && generation == m_PlaybackGeneration && voice == SessionConfig.Voice && identity == (voice == VoiceCondition.SelfSimilar ?
            SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId) && LibraryReady && relevant();
        yield return null; // Publish the handle before a synchronous response can complete.
        if (!Current()) { m_TransmissionCoroutine = null; m_TransmissionPhrase = null; yield break; }
        var cancellation = GuidanceScheduler.Begin(GuidanceRequestKind.Transmission);
        m_TransmissionCancellation = cancellation;
        double started = Time.realtimeSinceStartupAsDouble;
        Task<bool> advice = null;
        bool speak = true; // Provider outage falls back to current, locally qualified guidance.
        string outcome = "local_fallback";
        try
        {
            string state = new JObject { ["schema"] = "speech-transmission-v1", ["decision_id"] = id,
                ["runtime"] = JObject.Parse(CaptureDecisionContext()), ["proposed_phrase"] = phrase,
                ["proposed_semantics"] = JObject.FromObject(SpatialVoice.SpeechPhraseMetadata.Describe(phrase)),
                ["reason"] = reason, ["seconds_since_speech_ended"] = SpeechSilence(started),
                ["maximum_deferral_seconds"] = GuidanceSpeechPolicy.MaximumTransmissionDeferralSeconds,
                ["deferred_seconds"] = m_DeferredSince < 0 ? 0 : started - m_DeferredSince,
                ["locally_qualified"] = true }.ToString(Newtonsoft.Json.Formatting.None);
            Telemetry?.Invoke("transmission_state", "tip", "", state);
            cancellation.CancelAfter(TimeSpan.FromSeconds(GuidanceSpeechPolicy.AdviceDeadline));
            if (GuidanceSpeechPolicy.IsPromptEvent(reason))
            {
                // Use the already qualified local event; still revalidate below and
                // preserve the ordinary finish-phrase and post-playback gap rules.
                outcome = "local_priority_event";
            }
            else if (selectedSpeak.HasValue)
            {
                speak = selectedSpeak.Value;
                outcome = selectionSource == "jev" ? (speak ? "speak" : "wait") : "local_fallback_catalog";
            }
            else try { if (TransmissionAdvisor != null) advice = TransmissionAdvisor(state, cancellation.Token);
                else outcome = "local_no_advisor"; }
            catch (Exception) { outcome = "local_fallback_error"; }
            if (advice != null)
            {
                // Observe even a late fault from a provider that ignores cancellation.
                _ = advice.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                while (!advice.IsCompleted && Current() &&
                    Time.realtimeSinceStartupAsDouble - started < GuidanceSpeechPolicy.AdviceDeadline) yield return null;
                if (advice.IsCompleted && Time.realtimeSinceStartupAsDouble - started < GuidanceSpeechPolicy.AdviceDeadline)
                {
                    try { speak = advice.GetAwaiter().GetResult(); outcome = speak ? "speak" : "wait"; }
                    catch (Exception) { outcome = "local_fallback_error"; }
                }
                else outcome = "local_fallback_timeout";
            }
            if (!Current()) { speak = false; outcome = "discarded_stale"; m_DeferredSince = -1; }
            else if (outcome == "wait")
            {
                if (m_DeferredSince < 0) m_DeferredSince = Time.realtimeSinceStartupAsDouble;
                // Jev may defer a cue briefly, but cannot starve a hidden-target search.
                // This still requires current local geometry/qualification and prepared audio.
                if (Time.realtimeSinceStartupAsDouble - m_DeferredSince >= GuidanceSpeechPolicy.MaximumTransmissionDeferralSeconds)
                { speak = true; outcome = "local_fallback_deferral_limit"; }
            }
            // Applies even to provider failure, joint selection and deferral-limit fallback.
            if (speak && !DirectionRepeatAllowed(phrase, reason))
            { speak = false; outcome = "local_duplicate_suppressed"; }
            if (speak) m_DeferredSince = -1;
            Telemetry?.Invoke("transmission_decision", "tip", "", new JObject {
                ["decision_id"] = id, ["phrase"] = phrase, ["outcome"] = outcome,
                ["advice_path"] = selectedSpeak.HasValue ? "joint_catalog" : "transmission",
                ["catalog_source"] = selectionSource,
                ["elapsed_ms"] = (Time.realtimeSinceStartupAsDouble - started) * 1000 }.ToString(Newtonsoft.Json.Formatting.None));
            if (outcome == "wait") { m_WaitReason = reason; m_WaitUntil = Time.realtimeSinceStartupAsDouble + GuidanceSpeechPolicy.TransmissionRetrySeconds; }
        }
        finally
        {
            if (ReferenceEquals(m_TransmissionCancellation, cancellation))
            {
                m_TransmissionCancellation = null;
                GuidanceScheduler.Complete(GuidanceRequestKind.Transmission, cancellation);
                m_TransmissionCoroutine = null; m_TransmissionPhrase = null;
            }
        }
        if (!speak || !Current() || !GuidanceReady || !IsPreparedPhrase(phrase)) yield break;
        Speak(phrase, "tip", guidanceDecisionId: id,
            guidanceRelevant: () => relevant() && voice == SessionConfig.Voice && identity ==
                (voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId),
            guidanceCommitted: committed);
    }

    void CancelTransmission()
    {
        m_TransmissionEpoch++;
        m_TransmissionCancellation = null;
        GuidanceScheduler.Cancel(GuidanceRequestKind.Transmission);
        if (m_TransmissionCoroutine != null) StopCoroutine(m_TransmissionCoroutine);
        m_TransmissionCoroutine = null; m_TransmissionPhrase = null;
        m_WaitReason = null; m_WaitUntil = 0; m_DeferredSince = -1;
    }
}
