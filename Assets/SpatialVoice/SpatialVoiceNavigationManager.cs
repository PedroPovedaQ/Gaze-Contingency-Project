using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SpatialVoice
{
    /// <summary>Standalone navigation pipeline. Unity objects are only read on the main thread;
    /// HTTP/socket continuations process bytes off-thread. Study bridge supplies valid eye-gaze
    /// geometry and calibrated playback; standalone Trigger uses the player's horizontal forward.</summary>
    public sealed class SpatialVoiceNavigationManager : MonoBehaviour
    {
        [SerializeField] NavigationPhraseDatabase m_Database;
        [SerializeField] Transform m_Player;
        [SerializeField] NavigationTone m_PreferredTone = NavigationTone.Casual;
        [SerializeField, Range(20, 200)] int m_PrebufferMilliseconds = 60;
        AudioSource m_Source;
        AudioClip m_Clip;
        JevDecisionClient m_Decisions;
        ElevenLabsPcmClient m_Speech;
        Func<string> m_VoiceId;
        CancellationTokenSource m_Lifetime, m_Request;
        bool m_Busy;
        bool m_StreamingAllowed = true;
        int m_Variation;
        string m_LastPhrase;
        double m_LastEnded = -100;
        public bool IsBusy => m_Busy;
        public NavigationPhraseDatabase Database => m_Database;
        public string LastStatus { get; private set; }
        public event Action<string> Diagnostic;
        public event Action<string> DecisionTrace;
        public event Action<NavigationPhrase> PhraseSelected;

        /// <summary>Keys are injected at runtime, never serialized into a scene or catalog.
        /// Deploy public clients through an authenticated credential broker.</summary>
        public void Configure(string gatewayKey, string elevenLabsKey, Func<string> voiceId,
            NavigationPhraseDatabase database = null, bool allowStreaming = true)
        {
            Cancel();
            m_StreamingAllowed = allowStreaming;
            m_Decisions = new JevDecisionClient(gatewayKey);
            m_Decisions.Trace += json => DecisionTrace?.Invoke(json);
            m_Speech = new ElevenLabsPcmClient(elevenLabsKey);
            m_VoiceId = voiceId;
            if (database != null) m_Database = database;
            if (m_Database == null) m_Database = Resources.Load<NavigationPhraseDatabase>("SpatialVoice/NavigationPhrases");
            if (m_Database == null) throw new InvalidOperationException("Navigation catalog is missing.");
            m_Database.Validate();
            foreach (var phrase in m_Database.phrases) SpeechPhraseMetadata.Register(phrase.text, phrase.metadata);
        }
        void OnEnable() { m_Lifetime = new CancellationTokenSource(); }
        void OnDisable() { Cancel(); m_Lifetime?.Cancel(); m_Lifetime?.Dispose(); m_Lifetime = null; }
        void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        public void Cancel() { m_Request?.Cancel(); if (m_Source != null) m_Source.Stop(); }
        void Report(string status) { LastStatus = status; Diagnostic?.Invoke(status); }

        /// <summary>Public Unity-event entry point; all exceptions are handled. Repeated calls
        /// during a sentence are dropped, not queued. Next call measures fresh geometry.</summary>
        public async void TriggerNavigationVoiceHint(Transform target)
        {
            try { await TriggerAsync(target); }
            catch (Exception e) { Report("navigation_failed:" + e.GetType().Name); }
        }

        public async Task TriggerAsync(Transform target)
        {
            if (!m_StreamingAllowed) { Report("stream_preview_disabled_in_study"); return; }
            if (!isActiveAndEnabled || m_Busy || Time.realtimeSinceStartupAsDouble - m_LastEnded < .55) return;
            var player = m_Player != null ? m_Player : Camera.main != null ? Camera.main.transform : null;
            if (!NavigationGeometry.TryMeasure(player, target, out var initial)) return;
            if (m_Decisions == null || m_Speech == null || m_VoiceId == null) { Report("navigation_not_configured"); return; }
            var voiceId = m_VoiceId();
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(m_Lifetime.Token))
            {
                cancellation.CancelAfter(TimeSpan.FromSeconds(35)); // Also bounds a stalled audio device after network completion.
                m_Request = cancellation; m_Busy = true;
                Task receive = null;
                var latency = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var phrase = await ChooseAsync(initial, "Standalone transform navigation.", null, cancellation.Token);
                    if (!phrase.HasValue || !Relevant()) { Report("stale_before_synthesis"); return; }
                    var pcm = new PcmStreamBuffer(ElevenLabsPcmClient.SampleRate * 30);
                    receive = m_Speech.StreamAsync(phrase.Value.text, voiceId, pcm, cancellation.Token);
                    int threshold = ElevenLabsPcmClient.SampleRate * m_PrebufferMilliseconds / 1000;
                    while (pcm.Count < threshold && !pcm.Complete)
                    {
                        if (receive.IsCompleted) await receive;
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (!Relevant()) { Report("stale_during_synthesis"); return; }
                        await Task.Delay(10, cancellation.Token);
                    }
                    if (!Relevant()) { Report("stale_before_playback"); return; }
                    if (m_Source == null)
                    {
                        m_Source = gameObject.AddComponent<AudioSource>();
                        m_Source.playOnAwake = false; m_Source.spatialBlend = 0; m_Source.volume = .7f;
                    }
                    m_Clip = AudioClip.Create("Navigation PCM", ElevenLabsPcmClient.SampleRate, 1,
                        ElevenLabsPcmClient.SampleRate, true, pcm.Read);
                    m_Source.clip = m_Clip; m_Source.loop = true; m_Source.Play();
                    PhraseSelected?.Invoke(phrase.Value); Report("stream_playback_started;request_to_play_ms=" + latency.ElapsedMilliseconds + ";prebuffer_ms=" + m_PrebufferMilliseconds);
                    // Finish the short sentence even if direction changes after audible onset.
                    while (!pcm.Drained)
                    {
                        if (receive.IsCompleted) await receive;
                        await Task.Delay(10, cancellation.Token);
                    }
                    await receive;
                    // PCM callback feeds ahead of the audible device. Drain DSP buffers before Stop.
                    AudioSettings.GetDSPBufferSize(out int size, out int buffers);
                    await Task.Delay((int)Math.Ceiling(1000d * size * buffers / AudioSettings.outputSampleRate) + 30, cancellation.Token);
                    m_LastPhrase = phrase.Value.text;
                    Report("stream_playback_complete;underruns=" + pcm.Underruns);

                    bool Relevant() => this != null && isActiveAndEnabled && player != null && target != null &&
                        target.gameObject.activeInHierarchy && m_VoiceId() == voiceId &&
                        NavigationGeometry.TryMeasure(player, target, out var current) && phrase.HasValue &&
                        phrase.Value.Matches(current.Angle, current.Distance);
                }
                catch (OperationCanceledException) { Report("navigation_cancelled_or_timed_out"); }
                catch (Exception e) { Report("navigation_failed:" + e.GetType().Name); }
                finally
                {
                    cancellation.Cancel();
                    if (receive != null) { try { await receive; } catch (Exception) { } }
                    if (m_Source != null) { m_Source.Stop(); m_Source.clip = null; }
                    if (m_Clip != null) { Destroy(m_Clip); m_Clip = null; }
                    m_LastEnded = Time.realtimeSinceStartupAsDouble; m_Busy = false; m_Request = null;
                }
            }
        }

        /// <summary>Choice-only entry used by the existing study playback path. Filters first,
        /// asks Jev only about safe wording, falls back locally on missing key/timeout/error.</summary>
        public async Task<NavigationPhrase?> ChooseAsync(NavigationGeometry geometry, string behavior,
            Predicate<NavigationPhrase> eligible, CancellationToken token, string runtimeContext = null)
        {
            var result = await SelectAsync(geometry, behavior, eligible, token, runtimeContext, false);
            return result == null ? (NavigationPhrase?)null : result.Phrase;
        }

        public Task<GuidanceSelection> ChooseGuidanceAsync(NavigationGeometry geometry, string behavior,
            Predicate<NavigationPhrase> eligible, CancellationToken token, string runtimeContext) =>
            SelectAsync(geometry, behavior, eligible, token, runtimeContext, true);

        async Task<GuidanceSelection> SelectAsync(NavigationGeometry geometry, string behavior,
            Predicate<NavigationPhrase> eligible, CancellationToken token, string runtimeContext, bool decideTransmission)
        {
            if (m_Database == null) return null;
            var latency = System.Diagnostics.Stopwatch.StartNew();
            var buffer = new NavigationPhrase[5];
            int count = NavigationGeometry.Filter(m_Database.phrases, geometry, buffer, m_Variation++, eligible);
            if (count == 0) { Report("no_matching_phrase"); return null; }
            var candidates = new List<NavigationPhrase>(count);
            for (int i = 0; i < count; i++) candidates.Add(buffer[i]);
            var chosen = candidates[0];
            foreach (var p in candidates) if (p.tone == m_PreferredTone && p.text != m_LastPhrase) chosen = p;
            string source = "local";
            string requestId = Guid.NewGuid().ToString("N");
            bool speak = true;
            if (m_Decisions != null && m_Decisions.Available && count > 1)
            {
                string state = SpeechNavigationState.Create(geometry.Angle, geometry.Distance,
                    m_PreferredTone.ToString(), m_LastPhrase, behavior, runtimeContext);
                var stateJson = Newtonsoft.Json.Linq.JObject.Parse(state);
                stateJson["decision_id"] = requestId;
                state = stateJson.ToString(Newtonsoft.Json.Formatting.None);
                try {
                    if (decideTransmission)
                    {
                        var decision = await m_Decisions.ChooseGuidanceAsync(state, candidates, token);
                        chosen = decision.Phrase; speak = decision.Speak;
                    }
                    else chosen = await m_Decisions.ChooseAsync(state, candidates, token);
                    source = "jev";
                }
                catch (Exception e) when (!token.IsCancellationRequested)
                { source = "local_fallback_" + e.GetType().Name; }
            }
            token.ThrowIfCancellationRequested();
            Report("phrase_selected;source=" + source + ";id=" + chosen.id + ";tone=" + chosen.tone + ";selection_ms=" + latency.ElapsedMilliseconds +
                ";advice_path=" + (decideTransmission ? "joint_catalog" : "phrase_only") + ";transmit=" + (speak ? "speak" : "wait"));
            return new GuidanceSelection { Phrase = chosen, Speak = speak, Source = source, RequestId = requestId };
        }
        public void MarkSpoken(NavigationPhrase phrase) { m_LastPhrase = phrase.text; }
    }
}
