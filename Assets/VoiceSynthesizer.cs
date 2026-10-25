using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Calls ElevenLabs TTS API and plays audio via a non-spatialized AudioSource.
/// Caches generated MP3 files on disk so repeated phrases (tips, congrats, etc.)
/// play instantly from local storage after the first synthesis.
/// </summary>
public partial class VoiceSynthesizer : MonoBehaviour
{
    const string k_Tag = "[VoiceSynth]";
    const string k_BaseUrl = "https://api.elevenlabs.io/v1/text-to-speech/";
    const string k_Model = ElevenLabsVoiceClient.Model;
    const string k_CacheFolder = "tts_cache";

    string m_ApiKey;        // ElevenLabs key for catalog voices and enrollment
    ElevenLabsVoiceClient m_CloneClient;
    string m_CacheDir;
    AudioSource m_AudioSource;
    Coroutine m_SpeakCoroutine;
    Func<float[], int, int, bool, InnerThoughtVoice.Result> m_ProcessAudio = InnerThoughtVoice.Process;
    Coroutine m_InterruptionCoroutine;
    string m_CurrentContext;

    public const string ContentVersion = "matched-voice-v3";
    public string LastError { get; private set; }
    public bool ProviderPolicyBlocked => LastError != null && LastError.Contains("guardrail_violation");
    public string PreparationStage { get; private set; }
    // Completed preparation work, not an estimate of elapsed/remaining time.
    public float PreparationProgress { get; private set; }
    public bool LibraryReady { get; private set; }
    bool m_Preparing;
    VoiceCondition[] m_LibraryVoices = { VoiceCondition.Generic, VoiceCondition.SelfSimilar };
    HashSet<string> m_OnDemandPhrases;
    float m_MatchedRms;
    Coroutine m_BackgroundCoroutine;
    bool m_BackgroundLoading;
    bool m_BackgroundEnabled;
    bool m_SetupAnnouncement;
    readonly Queue<KeyValuePair<VoiceCondition, string>> m_BackgroundQueue = new Queue<KeyValuePair<VoiceCondition, string>>();
    readonly Dictionary<string, AudioClip> m_PreparedClips = new Dictionary<string, AudioClip>();
    readonly List<ClipAudit> m_Audit = new List<ClipAudit>();
    string m_ActiveClipKey;
    string m_GuidanceDecisionId, m_PendingCorrectionDecisionId;
    public event Action<string, string, string, string> Telemetry;
    public event Action<string> PlaybackFailed;

    [Serializable] class ClipAudit
    {
        public string clip_id, voice_id, voice_condition, provider, model, text, content_version, perspective, perspective_version, effect_profile;
        public string verification_version, source_audio_sha256;
        public float duration_seconds, rms, peak, gain, achieved_rms, requested_rms;
        public int speech_samples; // leading interleaved samples holding the words; RMS is measured here
        public bool level_limited;
    }
    [Serializable] class LibraryAudit
    {
        public string preparation_mode, perspective, perspective_version;
        public string normalization_policy = "safe-best-effort-v1";
        public float matched_rms;
        public List<ClipAudit> clips;
    }

    void Fail(string reason)
    {
        if (!m_BackgroundLoading) LastError = reason;
        AudioTelemetry("audio_failure", m_CurrentContext ?? "", m_ActiveClipKey ?? "", reason);
        Debug.LogWarning($"{k_Tag} {reason}");
        if (!m_Preparing && !m_BackgroundLoading && m_CurrentContext != "voice_choice") PlaybackFailed?.Invoke(reason);
    }

    public IEnumerator PrepareLibraries(string[] phrases, Action<string> progress, Action<bool> done,
        string[] onDemandPhrases = null, bool neutralOnly = false, VoiceCondition? onlyVoice = null)
    {
        if (neutralOnly && onlyVoice.HasValue && onlyVoice.Value != VoiceCondition.Generic)
            throw new ArgumentException("Conflicting single-voice preparation options.");
        SessionConfig.LockPerspective();
        Stop();
        int preparationGeneration = m_PreparationGeneration;
        m_BackgroundQueue.Clear();
        m_BackgroundEnabled = false;
        LibraryReady = false;
        PreparationProgress = 0;
        void Report(string stage, float fraction)
        {
            if (preparationGeneration != m_PreparationGeneration) return;
            PreparationStage = stage;
            PreparationProgress = Math.Max(PreparationProgress, Math.Min(.99f, fraction));
            progress?.Invoke(stage);
        }
        Report("Loading voice audio", 0);
        if (preparationGeneration != m_PreparationGeneration) yield break;
        m_OnDemandPhrases = onDemandPhrases == null ? null : new HashSet<string>(onDemandPhrases);
        m_MatchedRms = 0;
        m_Preparing = true;
        m_LibraryVoices = onlyVoice.HasValue ? new[] { onlyVoice.Value } : neutralOnly ? new[] { VoiceCondition.Generic }
            : new[] { VoiceCondition.Generic, VoiceCondition.SelfSimilar };
        m_Audit.Clear();
        foreach (var clip in m_PreparedClips.Values) Destroy(clip);
        m_PreparedClips.Clear();
        LastError = null;
        var lines = new List<KeyValuePair<VoiceCondition, string>>();
        foreach (var voice in m_LibraryVoices)
            foreach (var phrase in phrases) lines.Add(new KeyValuePair<VoiceCondition, string>(voice, phrase));
        var downloads = DownloadJobs(lines);
        yield return DownloadLines(downloads, completed =>
            Report($"Downloading voice lines: {completed}/{downloads.Count}", .45f * completed / Math.Max(1, downloads.Count)));
        if (preparationGeneration != m_PreparationGeneration) yield break;
        bool success = true;
        int preparedLines = 0;
        foreach (var job in downloads)
            if (job.error != null) { Fail(job.error); success = false; break; }
        foreach (var voice in m_LibraryVoices)
        {
            if (!success) break;
            for (int i = 0; i < phrases.Length; i++)
            {
                Report($"Preparing {(voice == VoiceCondition.Generic ? "neutral" : "self-similar")} voice: {i + 1}/{phrases.Length}",
                    .45f + .45f * preparedLines / Math.Max(1, lines.Count));
                m_PreparingText = VoicePromptText.FormatForVoice(phrases[i], voice);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    yield return SpeakCoroutine(phrases[i], requestedVoice: voice);
                    if (preparationGeneration != m_PreparationGeneration) yield break;
                    if (string.IsNullOrEmpty(LastError) || LastError.Contains("unavailable") || ProviderPolicyBlocked) break;
                    if (attempt < 2) yield return new WaitForSeconds(1 << attempt);
                    if (preparationGeneration != m_PreparationGeneration) yield break;
                }
                if (!string.IsNullOrEmpty(LastError)) { success = false; break; }
                Report(PreparationStage, .45f + .45f * ++preparedLines / Math.Max(1, lines.Count));
            }
            if (!success) break;
        }
        if (success)
        {
            Report("Matching audio levels", .90f);
            success = MatchLibraryLevels();
        }
        m_Preparing = false;
        LibraryReady = success;
        if (success)
        {
            Report("Saving voice manifest", .92f);
            LibraryReady = SaveManifest();
        }
        if (!LibraryReady)
        {
            Debug.LogWarning($"{k_Tag} Preparation failed at {PreparationStage}: {LastError}");
            try
            {
                File.AppendAllText(Path.Combine(SessionConfig.ParticipantPath, "voice-preparation-errors.log"),
                    $"{DateTime.UtcNow:O} | {PreparationStage} | {LastError}\n");
            }
            catch (Exception) { Debug.LogWarning($"{k_Tag} Could not persist preparation error."); }
        }
        // Publish the exact matched waveforms while Loading is still visible.
        // One clip per frame avoids a whole-library serialization spike. Transfer
        // acknowledgements never gate the participant's study.
        if (LibraryReady)
        {
            var replay = GetComponent<TrialReplayRecorder>();
            if (replay != null)
            {
                int exported = 0;
                foreach (var prepared in m_PreparedClips.Values)
                {
                    replay.PrepareVoiceClip(prepared);
                    Report("Finishing audio setup", .94f + .05f * ++exported / Math.Max(1, m_PreparedClips.Count));
                    yield return null;
                    if (preparationGeneration != m_PreparationGeneration) yield break;
                }
            }
            PreparationProgress = 1;
            PreparationStage = "Audio ready";
            progress?.Invoke(PreparationStage);
            yield return null;
            if (preparationGeneration != m_PreparationGeneration) yield break;
        }
        done?.Invoke(LibraryReady);
    }

    bool MatchLibraryLevels()
    {
        float target = 0.1f;
        foreach (var item in m_Audit) target = Math.Min(target, item.rms * item.gain);
        if (target < 0.02f) { Fail("Audio library has insufficient level/headroom; regenerate it."); return false; }
        foreach (var item in m_Audit)
        {
            var clip = m_PreparedClips[Path.Combine(m_CacheDir, item.clip_id + ".mp3")];
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0)) { Fail("Cannot inspect matched audio."); return false; }
            float adjustment = target / (item.rms * item.gain);
            int speech = Math.Min(samples.Length, item.speech_samples);
            double sum = 0;
            for (int i = 0; i < samples.Length; i++) { samples[i] *= adjustment; if (i < speech) sum += samples[i] * samples[i]; }
            item.achieved_rms = (float)Math.Sqrt(sum / Math.Max(1, speech));
            if (Math.Abs(item.achieved_rms - target) > target * 0.01f || !clip.SetData(samples, 0))
            { Fail("Could not match voice library levels."); return false; }
            item.gain *= adjustment;
            item.requested_rms = target;
            item.level_limited = false;
        }
        m_MatchedRms = target;
        return true;
    }

    bool SaveManifest()
    {
        try
        {
            string json = JsonUtility.ToJson(new LibraryAudit { clips = m_Audit,
                matched_rms = m_MatchedRms,
                perspective = "shared_participant_directed_imperative", perspective_version = VoicePromptText.Version,
                preparation_mode = m_OnDemandPhrases == null ? "full_library" :
                    m_BackgroundEnabled ? "starter_then_background" : "starter_then_on_demand" }, true);
            File.WriteAllText(Path.Combine(SessionConfig.ParticipantPath, "voice-library-manifest.json"), json);
            if (!string.IsNullOrEmpty(SessionConfig.CurrentRunFolder))
                File.WriteAllText(SessionConfig.GetFilePath("voice-library-manifest.json"), json);
            return true;
        }
        catch (Exception e) { Fail("Could not save voice manifest: " + e.Message); return false; }
    }

    /// <summary>Catalog selection may only choose already-normalized, voice-scoped audio.
    /// A cache file alone is not enough: the current library must have admitted the clip.</summary>
    public bool IsPreparedPhrase(string phrase)
    {
        if (!LibraryReady || string.IsNullOrEmpty(m_CacheDir)) return false;
        bool clone = SessionConfig.Voice == VoiceCondition.SelfSimilar;
        string id = clone ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
        if (string.IsNullOrEmpty(id) || (clone && SessionConfig.SelfSimilarEnrollmentPending)) return false;
        string path = GetCachePath(VoicePromptText.FormatForVoice(phrase, SessionConfig.Voice),
            clone ? $"el-clone-{id}" : $"el-{id}");
        return m_PreparedClips.ContainsKey(path);
    }

    public bool IsSpeaking => m_AudioSource != null && m_AudioSource.isPlaying;
    // Read-only observer access to the exact normalized clip used by playback.
    public AudioClip ReplayClip => m_AudioSource != null ? m_AudioSource.clip : null;
    public bool IsBusy => m_TransmissionCoroutine != null || m_InterruptionCoroutine != null || m_SpeakCoroutine != null || IsSpeaking;
    double m_LastGuidanceEnd = -100;
    double? m_LastSpeechEnd;
    string m_LastDirectionKey;
    double m_LastDirectionEnd = -100;
    static string DirectionKey(string phrase)
    {
        var meaning = SpatialVoice.SpeechPhraseMetadata.Describe(phrase);
        if (meaning.horizontal == "none" && meaning.vertical == "none") return null;
        return meaning.horizontal + ":" + meaning.vertical + ":" + meaning.magnitude;
    }
    bool DirectionRepeatAllowed(string phrase, string reason) =>
        !GuidanceSpeechPolicy.IsOrdinaryDirection(reason) || DirectionKey(phrase) == null ||
        DirectionKey(phrase) != m_LastDirectionKey ||
        Time.realtimeSinceStartupAsDouble - m_LastDirectionEnd >= GuidanceSpeechPolicy.DuplicateDirectionGap;
    Newtonsoft.Json.Linq.JToken SpeechSilence(double now) => m_LastSpeechEnd.HasValue
        ? new Newtonsoft.Json.Linq.JValue(Math.Max(0, now - m_LastSpeechEnd.Value))
        : Newtonsoft.Json.Linq.JValue.CreateNull();
    bool m_LastGuidanceWasAcknowledgment;
    string m_CurrentPhrase;
    public bool GuidanceReady => GuidanceSpeechPolicy.CanStart(IsBusy,
        Time.realtimeSinceStartupAsDouble, m_LastGuidanceEnd, m_LastGuidanceWasAcknowledgment);
    bool GuidanceReadyFor(string phrase) => GuidanceSpeechPolicy.CanStart(IsBusy,
        Time.realtimeSinceStartupAsDouble, m_LastGuidanceEnd,
        m_LastGuidanceWasAcknowledgment || GuidanceSpeechPolicy.IsAcknowledgment(phrase));

    void MarkGuidanceEnd()
    {
        m_LastSpeechEnd = Time.realtimeSinceStartupAsDouble;
        if (m_CurrentContext != "tip" && m_CurrentContext != "guidance_repeat" && m_CurrentContext != "guidance_opening") return;
        m_LastGuidanceEnd = Time.realtimeSinceStartupAsDouble;
        m_LastDirectionKey = DirectionKey(m_CurrentPhrase);
        m_LastDirectionEnd = m_LastGuidanceEnd;
        m_LastGuidanceWasAcknowledgment = GuidanceSpeechPolicy.IsAcknowledgment(m_CurrentPhrase);
    }

    public void StartBackgroundLoading(string[] phrases)
    {
        if (!LibraryReady || m_OnDemandPhrases == null || m_BackgroundLoading || m_BackgroundQueue.Count > 0) return;
        m_BackgroundEnabled = true;
        m_BackgroundQueue.Clear();
        // Interleave voices so upcoming prompts are ready in either condition.
        foreach (string phrase in phrases)
            if (m_OnDemandPhrases.Contains(phrase))
                foreach (var voice in m_LibraryVoices)
                    m_BackgroundQueue.Enqueue(new KeyValuePair<VoiceCondition, string>(voice, phrase));
    }

    void Update()
    {
        ValidateInterruptionAdvice();
        if (!LibraryReady || m_Preparing || m_SetupAnnouncement || IsBusy ||
            m_BackgroundLoading || m_BackgroundQueue.Count == 0) return;
        m_BackgroundCoroutine = StartCoroutine(LoadNextBackgroundClip());
    }

    IEnumerator LoadNextBackgroundClip()
    {
        m_BackgroundLoading = true;
        int generation = m_PreparationGeneration;
        yield return null;
        var pending = m_BackgroundQueue.ToArray();
        var lines = new List<KeyValuePair<VoiceCondition, string>>();
        for (int i = 0; i < Math.Min(VoiceDownloadBatch.Concurrency, pending.Length); i++) lines.Add(pending[i]);
        var downloads = DownloadJobs(lines);
        yield return DownloadLines(downloads);
        if (generation != m_PreparationGeneration) yield break;
        m_CurrentContext = "prefetch";
        foreach (var job in downloads)
            if (job.error != null) Fail(job.error);
        foreach (var next in lines)
        {
            // Admit downloaded clips sequentially, preserving per-voice DSP, audit and matched levels.
            string id = next.Key == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
            string path = GetCachePath(VoicePromptText.FormatForVoice(next.Value, next.Key),
                next.Key == VoiceCondition.SelfSimilar ? $"el-clone-{id}" : $"el-{id}");
            if (File.Exists(path)) yield return SpeakCoroutine(next.Value, false, next.Key, true);
            if (generation != m_PreparationGeneration) yield break;
            m_BackgroundQueue.Dequeue();
        }
        // Background failures leave the clip available for a foreground retry.
        // They must not interrupt a running trial or poison a successful audio check.
        m_ActiveClipKey = null;
        m_BackgroundLoading = false;
        m_BackgroundCoroutine = null;
    }

    /// <summary>Shared ElevenLabs client (voice enrollment reuses this instance).</summary>
    public ElevenLabsVoiceClient CloneClient => m_CloneClient;

    public void Initialize(string elevenLabsKey)
    {
        m_ApiKey = elevenLabsKey;

        m_CloneClient = GetComponent<ElevenLabsVoiceClient>();
        if (m_CloneClient == null) m_CloneClient = gameObject.AddComponent<ElevenLabsVoiceClient>();
        m_CloneClient.Initialize(elevenLabsKey);

        m_AudioSource = gameObject.AddComponent<AudioSource>();
        m_AudioSource.spatialBlend = 0f;
        m_AudioSource.volume = 0.7f;
        m_AudioSource.playOnAwake = false;

        // Persistent cache directory for MP3 files
        m_CacheDir = Path.Combine(Application.persistentDataPath, k_CacheFolder);
        if (!Directory.Exists(m_CacheDir))
            Directory.CreateDirectory(m_CacheDir);

        Debug.Log($"{k_Tag} Initialized, cache at {m_CacheDir}");
    }

    void OnDestroy()
    {
        Stop();
        foreach (var clip in m_PreparedClips.Values) Destroy(clip);
        m_PreparedClips.Clear();
    }

    void OnDisable() => Stop();

    public GuidanceRequestScheduler GuidanceScheduler { get; } = new GuidanceRequestScheduler();

    public void Speak(string text, string context = null, VoiceCondition? requestedVoice = null,
        string guidanceDecisionId = null, Func<bool> guidanceRelevant = null, Action guidanceCommitted = null)
    {
        // Identity and key checks happen inside the coroutine so prepared audio works offline.
        Stop();
        m_CurrentContext = context;
        m_CurrentPhrase = text;
        m_GuidanceDecisionId = guidanceDecisionId;
        GuidanceScheduler.QueuePlayback(guidanceRelevant == null ? null :
            (Func<bool>)(() => StateAllowsNewGuidance && guidanceRelevant()), guidanceCommitted);
        m_SpeakCoroutine = StartCoroutine(SpeakCoroutine(text, requestedVoice: requestedVoice));
    }

    /// <summary>Changing the setup voice invalidates its prepared library and prefetch work.</summary>
    public void ResetVoicePreparation()
    {
        Stop();
        m_BackgroundQueue.Clear(); m_BackgroundEnabled = false;
        LibraryReady = false; m_OnDemandPhrases = null; m_MatchedRms = 0; LastError = null;
        PreparationProgress = 0;
        m_Audit.Clear();
        foreach (var clip in m_PreparedClips.Values) Destroy(clip);
        m_PreparedClips.Clear();
    }

    public void PreviewNeutralVoice(NeutralVoiceProfile gender, int option, Action<bool> completed)
    {
        ResetVoicePreparation();
        m_CurrentContext = "voice_choice";
        // Capture the audition identity; hovering must not mutate the study selection.
        m_SpeakCoroutine = StartCoroutine(PreviewNeutralCoroutine(SessionConfig.NeutralId(gender, option), completed));
    }

    IEnumerator PreviewNeutralCoroutine(string voiceId, Action<bool> completed)
    {
        yield return null; // Store the cancellable coroutine handle before beginning playback.
        yield return SpeakCoroutine(
            "Find the red cube. Look to your left, then keep looking in that area.",
            true, VoiceCondition.Generic, requestedVoiceId: voiceId);
        completed?.Invoke(string.IsNullOrEmpty(LastError));
    }

    public void RecordGuidanceRepeatRequest(bool available, string decisionId = null)
    {
        string identity = decisionId == null ? "" : "decision_id=" + decisionId + ";";
        Telemetry?.Invoke("guidance_repeat_requested", "guidance_repeat", "", identity);
        if (!available) Telemetry?.Invoke("guidance_repeat_unavailable", "guidance_repeat", "", identity + "no_current_valid_gaze_guidance");
    }

    // Decisions are recorded separately from speech requests and audible starts.
    // Keep serialized snapshots out of the audio cache identity and spoken text.
    public void RecordGuidanceDecision(string json) => Telemetry?.Invoke("guidance_decision", "guidance", "", json);
    public void RecordJevTrace(string json) { if (this != null) Telemetry?.Invoke("jev_trace", "jev", "", json); }
    public void RecordNavigationStatus(string json) => Telemetry?.Invoke("navigation_status", "guidance", "", json);
    public void RecordRoundEncouragement(string json) => Telemetry?.Invoke("round_encouragement_decision", "congrats", "", json);

    void AudioTelemetry(string kind, string context, string clip, string detail)
    {
        if (!string.IsNullOrEmpty(m_GuidanceDecisionId) && (context == "tip" || context == "guidance_repeat" || context == "guidance_opening" || context == "readiness_reminder"))
            detail = "decision_id=" + m_GuidanceDecisionId + ";" + detail;
        Telemetry?.Invoke(kind, context, clip, detail);
    }

    public bool TryAreaCorrection(string phrase, Func<bool> stillRelevant, Action onCommitted = null,
        string reason = "gaze_target_plane", string guidanceDecisionId = null)
    {
        // Qualified target acquisition has a bounded local fast path; other corrections use Jev.
        // Missing/late/failed advice preserves ordinary finish-phrase behavior.
        if (!LibraryReady || m_InterruptionCoroutine != null || m_SetupAnnouncement ||
            (m_CurrentContext != null && m_CurrentContext != "tip" && m_CurrentContext != "guidance_repeat" && m_CurrentContext != "prefetch")) return false;
        bool self = SessionConfig.Voice == VoiceCondition.SelfSimilar;
        string voiceId = self ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
        if (self && SessionConfig.SelfSimilarEnrollmentPending) return false;
        string path = GetCachePath(VoicePromptText.FormatForVoice(phrase, SessionConfig.Voice),
            (self ? "el-clone-" : "el-") + voiceId);
        if (!m_PreparedClips.ContainsKey(path) || !stillRelevant()) return false;
        bool ready = GuidanceReadyFor(phrase);
        if (ready && TransmissionAdvisor != null)
            return TryGuidanceTransmission(phrase, stillRelevant, onCommitted, reason, guidanceDecisionId);
        if (!ready && !ApproveInterruption(phrase, reason, stillRelevant, ref guidanceDecisionId)) return false;
        m_DropCorrection = false;
        m_PendingCorrectionDecisionId = guidanceDecisionId;
        m_InterruptionCoroutine = StartCoroutine(FadeToAreaCorrection(phrase, stillRelevant, onCommitted, reason, guidanceDecisionId));
        return true;
    }

    IEnumerator FadeToAreaCorrection(string phrase, Func<bool> stillRelevant, Action onCommitted, string reason, string decisionId)
    {
        yield return null; // Store the cancellable handle before executing any completion path.
        bool correction = phrase != null;
        if (correction && (!stillRelevant() || m_DropCorrection))
        { m_InterruptionCoroutine = null; AbandonCorrection("stale_before_fade"); yield break; }
        float volume = m_AudioSource.volume;
        bool faded = IsSpeaking;
        double start = Time.realtimeSinceStartupAsDouble;
        if (faded)
        {
            AudioTelemetry("audio_fade_start", m_CurrentContext ?? "", m_ActiveClipKey ?? "",
                "fade_seconds=0.150;reason=" + reason);
            // Once a fade starts, complete it rather than audibly pumping the gain back up.
            // The replacement is independently revalidated after the old audio has stopped.
            while (Time.realtimeSinceStartupAsDouble - start < GuidanceSpeechPolicy.FadeSeconds)
            {
                float t = (float)((Time.realtimeSinceStartupAsDouble - start) / GuidanceSpeechPolicy.FadeSeconds);
                m_AudioSource.volume = Mathf.Lerp(volume, 0f, t);
                yield return null;
            }
            m_AudioSource.volume = 0f; // Stop must observe silence, never the restored gain.
            m_LastSmoothInterruption = Time.realtimeSinceStartupAsDouble;
        }
        bool commit = correction && !m_DropCorrection && stillRelevant();
        string oldClip = m_ActiveClipKey ?? "";
        m_InterruptionCoroutine = null;
        if (commit) m_PendingCorrectionDecisionId = null;
        else AbandonCorrection("stale_after_fade");
        Stop();
        m_AudioSource.volume = volume;
        if (!commit) yield break;
        Telemetry?.Invoke("audio_area_correction", "tip", oldClip,
            (decisionId == null ? "" : "decision_id=" + decisionId + ";") + reason +
            (faded ? ";fade_seconds=0.150" : ";fade_seconds=0"));
        Speak(phrase, "tip", guidanceDecisionId: decisionId, guidanceRelevant: stillRelevant, guidanceCommitted: onCommitted);
    }

    void AbandonCorrection(string reason)
    {
        if (m_PendingCorrectionDecisionId != null)
            Telemetry?.Invoke("guidance_decision_aborted", "tip", "",
                "decision_id=" + m_PendingCorrectionDecisionId + ";reason=" + reason);
        m_PendingCorrectionDecisionId = null;
    }

    public void Stop()
    {
        m_PreparationGeneration++;
        m_DownloadBatch?.Dispose(); m_DownloadBatch = null;
        m_Preparing = false;
        CancelInterruptionAdvice();
        CancelTransmission();
        m_PlaybackGeneration++;
        m_CurrentSpeechSeconds = -1;
        if (m_InterruptionCoroutine != null)
        {
            StopCoroutine(m_InterruptionCoroutine);
            m_InterruptionCoroutine = null;
        }
        AbandonCorrection("cancelled_before_commit");
        if (m_BackgroundCoroutine != null)
        {
            StopCoroutine(m_BackgroundCoroutine);
            m_BackgroundCoroutine = null;
            m_BackgroundLoading = false;
            // Keep the interrupted item queued; foreground playback takes priority.
        }
        if (m_SpeakCoroutine != null)
        {
            StopCoroutine(m_SpeakCoroutine);
            m_SpeakCoroutine = null;
        }

        m_DecisionMemory.Ended(Time.realtimeSinceStartupAsDouble, interrupted: true);
        if (m_AudioSource != null && m_AudioSource.isPlaying)
        { MarkGuidanceEnd(); m_AudioSource.Stop(); }
        if (m_AudioSource != null) m_AudioSource.volume = 0.7f;

        if (m_ActiveClipKey != null) AudioTelemetry("audio_cancelled", m_CurrentContext ?? "", m_ActiveClipKey, "");
        m_ActiveClipKey = null;
        m_CurrentContext = null;
        m_CurrentPhrase = null;
        m_GuidanceDecisionId = null;
        GuidanceScheduler.CancelPlayback();
    }

    public void InterruptIfAbout(string context, bool immediate = false)
    {
        if (string.IsNullOrEmpty(m_CurrentContext) || !m_CurrentContext.Equals(context, StringComparison.OrdinalIgnoreCase)) return;
        if (immediate || (context != "tip" && context != "guidance_repeat") || !IsSpeaking) { Stop(); return; }
        // Sustained gaze loss uses the same fade, without waiting on a remote decision.
        // Repeated invalid ticks never restart it, and cannot preserve a queued correction.
        m_DropCorrection = true;
        CancelInterruptionAdvice();
        if (m_InterruptionCoroutine == null)
            m_InterruptionCoroutine = StartCoroutine(FadeToAreaCorrection(null, null, null, "gaze_unavailable", null));
    }

    public IEnumerator SpeakRecordingIntroduction() => SpeakSetupAnnouncement(
        "We will now connect a short sample of your voice, after the tone please read the script.");

    public IEnumerator SpeakProcessingStatus(bool complete) => SpeakSetupAnnouncement(complete
        ? "Voice setup is ready. Let's check the audio."
        : "Give us a moment while we process your voice.");

    IEnumerator SpeakSetupAnnouncement(string text)
    {
        Stop();
        var originalVoice = SessionConfig.Voice;
        m_SetupAnnouncement = true;
        try
        {
            // Setup announcements use the selected neutral voice before study playback.
            SessionConfig.Voice = VoiceCondition.Generic;
            yield return SpeakCoroutine(text, true);
        }
        finally { SessionConfig.Voice = originalVoice; m_SetupAnnouncement = false; }
    }

    IEnumerator SpeakCoroutine(string text, bool setupAnnouncement = false,
        VoiceCondition? requestedVoice = null, bool silent = false, string requestedVoiceId = null)
    {
        int generation = m_PreparationGeneration;
        long playbackGeneration = m_PlaybackGeneration;
        // Defer once so Speak has stored the coroutine handle before any early exit.
        yield return null;
        if (generation != m_PreparationGeneration || playbackGeneration != m_PlaybackGeneration) yield break;
        if (!silent) LastError = null;
        bool allowOnDemand = LibraryReady && m_OnDemandPhrases != null && m_OnDemandPhrases.Contains(text);
        bool wantSelfSimilar = (requestedVoice ?? SessionConfig.Voice) == VoiceCondition.SelfSimilar;
        var actualPerspective = VoicePromptText.PerspectiveForVoice(requestedVoice ?? SessionConfig.Voice);
        if (!setupAnnouncement) text = VoicePromptText.Format(text, actualPerspective);
        if (VoiceAudioIntegrity.HasForbiddenFiller(text))
        { Fail("Script contains a prohibited filler."); m_SpeakCoroutine = null; yield break; }
        m_PreparingText = text;
        string voiceId = requestedVoiceId ?? (wantSelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId);
        string voiceScope = wantSelfSimilar ? $"el-clone-{voiceId}" : $"el-{voiceId}";

        // Voice availability never changes the assigned condition or cache namespace.
        if (wantSelfSimilar && (SessionConfig.SelfSimilarEnrollmentPending || string.IsNullOrEmpty(voiceId)))
        {
            Fail("Self-similar voice unavailable.");
            m_SpeakCoroutine = null;
            m_CurrentContext = null;
            yield break;
        }

        // 1) Cache for the intended voice.
        string cachePath = GetCachePath(text, voiceScope);
        m_ActiveClipKey = Path.GetFileNameWithoutExtension(cachePath);
        AudioTelemetry("audio_request", m_CurrentContext ?? "", m_ActiveClipKey, voiceScope);
        if (LibraryReady && !m_Preparing && !setupAnnouncement &&
            (m_PreparedClips.ContainsKey(cachePath) || !allowOnDemand))
        {
            if (!m_PreparedClips.ContainsKey(cachePath)) Fail("Clip absent from prepared library.");
            else yield return PlayFromFile(cachePath, wantSelfSimilar, voiceId, silent);
            if (generation != m_PreparationGeneration) yield break;
            m_SpeakCoroutine = null; m_CurrentContext = null;
            yield break;
        }
        if (SessionConfig.VoiceBlocksEnabled && !LibraryReady && !m_Preparing && !setupAnnouncement)
        {
            Fail("Voice library is not ready.");
            m_SpeakCoroutine = null; m_CurrentContext = null;
            yield break;
        }
        // One admission path for setup previews, foreground speech and prepared/background libraries.
        var job = new DownloadJob { text = text, voiceId = voiceId, path = cachePath, clone = wantSelfSimilar };
        yield return DownloadLine(job);
        if (generation != m_PreparationGeneration) yield break;
        if (job.error != null || !VoiceAudioVerification.HasReceipt(cachePath, text))
        {
            Fail(job.error ?? "Audio has no valid verification receipt.");
            m_SpeakCoroutine = null; m_CurrentContext = null;
            yield break;
        }

        yield return PlayFromFile(cachePath, wantSelfSimilar, voiceId, silent);
        if (generation != m_PreparationGeneration) yield break;

        m_SpeakCoroutine = null;
        m_CurrentContext = null;
    }

    void RejectClip(AudioClip clip, string path, string reason)
    {
        if (clip != null) Destroy(clip);
        try { File.Delete(path); } catch (IOException) { }
        Fail(reason);
    }

    IEnumerator PlayFromFile(string filePath, bool wantSelfSimilar, string voiceId, bool silent)
    {
        int generation = m_PreparationGeneration;
        var actualPerspective = wantSelfSimilar ? VoicePerspective.FirstPerson : VoicePerspective.External;
        AudioClip clip;
        if (!m_PreparedClips.TryGetValue(filePath, out clip))
        {
            if (!VoiceAudioVerification.HasReceipt(filePath, m_PreparingText))
            { Fail("Unverified audio cannot enter the playable library."); yield break; }
            using (var request = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG))
            {
                yield return request.SendWebRequest();
                if (generation != m_PreparationGeneration) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Fail("Could not decode cached audio.");
                    try { File.Delete(filePath); } catch (IOException) { }
                    yield break;
                }
                clip = DownloadHandlerAudioClip.GetContent(request);
            }
            if (clip == null || clip.length < 0.1f)
            {
                Fail("Decoded audio is empty.");
                if (clip != null) Destroy(clip);
                try { File.Delete(filePath); } catch (IOException) { }
                yield break;
            }
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0)) { RejectClip(clip, filePath, "Could not read decoded audio samples."); yield break; }
            // Raw provider caches stay untouched. Replay captures this processed clip;
            // normalization below measures the actual treated waveform in both modes.
            // FFT/filter work is pure PCM processing and can take hundreds of ms per
            // phrase. Never run it on the XR frame thread. Unity object access stays
            // here; the worker owns only the copied samples and captured format.
            int channels = clip.channels, frequency = clip.frequency;
            var input = samples;
            var processor = m_ProcessAudio;
            var processing = Task.Run(() => processor(input, channels, frequency, wantSelfSimilar));
            InnerThoughtVoice.Result treated;
            bool consumed = false;
            try
            {
                while (!processing.IsCompleted)
                {
                    if (generation != m_PreparationGeneration) yield break;
                    yield return null;
                }
                if (generation != m_PreparationGeneration) yield break;
                if (processing.IsFaulted || processing.IsCanceled)
                {
                    Fail("Could not process voice audio.");
                    yield break;
                }
                treated = processing.Result;
                consumed = true;
            }
            finally
            {
                if (!consumed)
                {
                    Destroy(clip);
                    // A cancelled coroutine cannot publish a late result. Observe
                    // any later failure without touching Unity from the worker.
                    _ = processing.ContinueWith(t => { _ = t.Exception; },
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                }
            }
            if (treated.Samples != samples)
            {
                // The treatment returns stereo with a short room tail, so it needs its own clip.
                var processed = AudioClip.Create(clip.name, treated.Samples.Length / treated.Channels,
                    treated.Channels, clip.frequency, false);
                Destroy(clip);
                clip = processed;
                samples = treated.Samples;
            }
            // Level the words only; the room tail must not raise the clone's speech level.
            double sum = 0; float peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                if (i < treated.SpeechSamples) sum += samples[i] * samples[i];
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, treated.SpeechSamples));
            if (rms < 0.00001f) { RejectClip(clip, filePath, "Audio is silent."); yield break; }
            // Keep accepted starter RMS as the target, without exceeding safe gain/headroom.
            // Later phrases can have different crest factors; record the shortfall, not a trial failure.
            float requestedRms = LibraryReady && m_MatchedRms > 0 ? m_MatchedRms : 0.1f;
            float requestedGain = requestedRms / rms;
            float gain = Math.Min(requestedGain, Math.Min(4f, 0.9f / peak));
            bool levelLimited = gain < requestedGain;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            if (!clip.SetData(samples, 0)) { RejectClip(clip, filePath, "Could not normalize decoded audio."); yield break; }
            m_PreparedClips[filePath] = clip;
            m_Audit.Add(new ClipAudit { clip_id = Path.GetFileNameWithoutExtension(filePath),
                voice_id = voiceId,
                provider = "elevenlabs",
                voice_condition = wantSelfSimilar ? "selfsimilar" : "neutral",
                model = k_Model,
                text = m_PreparingText, content_version = ContentVersion,
                perspective = m_SetupAnnouncement ? "setup" : actualPerspective.ToString(),
                perspective_version = VoicePromptText.Version,
                effect_profile = InnerThoughtVoice.Profile(wantSelfSimilar),
                verification_version = VoiceAudioVerification.Version,
                source_audio_sha256 = VoiceAudioVerification.Hash(File.ReadAllBytes(filePath)),
                duration_seconds = clip.length, rms = rms, peak = peak, gain = gain, achieved_rms = rms * gain,
                requested_rms = requestedRms, speech_samples = treated.SpeechSamples, level_limited = levelLimited });
            if (levelLimited && LibraryReady)
                AudioTelemetry("audio_level_limited", m_CurrentContext ?? "", m_ActiveClipKey ?? "",
                    FormattableString.Invariant($"requested_rms={requestedRms:F6};achieved_rms={rms * gain:F6};gain={gain:F6};difference_db={20 * Math.Log10(rms * gain / requestedRms):F3}"));
        }
        // Persist before playback, including clips added after the run began.
        if (LibraryReady && !m_Preparing && !SaveManifest()) yield break;
        if (LibraryReady && !m_Preparing)
            GetComponent<TrialReplayRecorder>()?.PrepareVoiceClip(clip);
        AudioTelemetry("audio_ready", m_CurrentContext ?? "", Path.GetFileNameWithoutExtension(filePath), $"duration={clip.length:F4}");
        if (m_Preparing || silent) yield break;
        m_CurrentSpeechSeconds = -1;
        foreach (var audit in m_Audit)
            if (audit.clip_id == m_ActiveClipKey && audit.speech_samples > 0)
            { m_CurrentSpeechSeconds = (double)audit.speech_samples / (clip.channels * clip.frequency); break; }
        m_AudioSource.clip = clip;
        m_AudioSource.volume = 0.7f;
        m_AudioSource.pitch = 1f;
        m_SpeechStartStage = GuidanceEvidence.valid ? (GuidanceEvidence.insideTargetWall ? "within_target_wall" : "outside_target_wall") : "unknown";
        // The queued coroutine yielded after admission. Revalidate at the actual
        // audio start; a departed gaze or changed phase must not consume its gate.
        if (!GuidanceScheduler.TryTakePlayback(out var onGuidanceStarted))
        {
            AudioTelemetry("guidance_decision_aborted", m_CurrentContext ?? "tip", m_ActiveClipKey ?? "", "reason=stale_before_playback");
            GuidanceScheduler.CancelPlayback();
            m_ActiveClipKey = null;
            yield break;
        }
        m_LastSpokenPhrase = m_PreparingText;
        m_AudioSource.Play();
        m_DecisionMemory.Started(m_PreparingText, m_CurrentContext, Time.realtimeSinceStartupAsDouble, GuidanceEvidence.guidanceState);
        AudioTelemetry("audio_playback_start", m_CurrentContext ?? "", m_ActiveClipKey, $"dsp_time={AudioSettings.dspTime:F6};perspective={actualPerspective};perspective_version={VoicePromptText.Version};effect_profile={InnerThoughtVoice.Profile(wantSelfSimilar)};text={m_PreparingText}");
        onGuidanceStarted?.Invoke();
        while (m_AudioSource.isPlaying) yield return null;
        m_DecisionMemory.Ended(Time.realtimeSinceStartupAsDouble, interrupted: false);
        MarkGuidanceEnd();
        AudioTelemetry("audio_playback_end", m_CurrentContext ?? "", m_ActiveClipKey, $"dsp_time={AudioSettings.dspTime:F6};perspective={actualPerspective};perspective_version={VoicePromptText.Version};text={m_PreparingText}");
        m_ActiveClipKey = null;
    }

    string m_PreparingText;

    /// <summary>
    /// Pre-generates and caches audio for a list of phrases in the background.
    /// Call at game start so tips play instantly during gameplay.
    /// </summary>
    public void PreCachePhrases(string[] phrases)
    {
        SessionConfig.LockPerspective();
        StartCoroutine(PreCacheCoroutine(phrases));
    }

    IEnumerator PreCacheCoroutine(string[] phrases)
    {
        var lines = new List<KeyValuePair<VoiceCondition, string>>();
        foreach (var phrase in phrases) lines.Add(new KeyValuePair<VoiceCondition, string>(VoiceCondition.Generic, phrase));
        var jobs = DownloadJobs(lines);
        yield return DownloadLines(jobs);
    }

    /// <summary>
    /// Generates a deterministic cache file path from the text content.
    /// Uses a simple hash to avoid filesystem issues with long/special-char filenames.
    /// </summary>
    string GetCachePath(string text, string scope)
    {
        // Scope the key by voice so generic vs self-similar (and different clones)
        // never collide on identical hint text; include script mode/version even for shared lines.
        string model = k_Model;
        string keyed = ContentVersion + "|" + VoicePromptText.Version + "|" +
            SessionConfig.Perspective + "|" + model + "|" + scope + "|" + text;
        using (var sha = SHA256.Create())
        {
            string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(keyed))).Replace("-", "").ToLowerInvariant();
            return Path.Combine(m_CacheDir, $"tts_{hash}.mp3");
        }
    }

    static string Truncate(string s, int maxLen)
    {
        return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
    }

}
