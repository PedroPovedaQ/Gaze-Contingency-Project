using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Calls ElevenLabs TTS API and plays audio via a non-spatialized AudioSource.
/// Caches generated MP3 files on disk so repeated phrases (tips, congrats, etc.)
/// play instantly from local storage after the first synthesis.
/// </summary>
public class VoiceSynthesizer : MonoBehaviour
{
    const string k_Tag = "[VoiceSynth]";
    const string k_BaseUrl = "https://api.elevenlabs.io/v1/text-to-speech/";
    const string k_Model = "eleven_turbo_v2_5";
    const string k_CacheFolder = "tts_cache";

    string m_ApiKey;        // ElevenLabs key (generic voice)
    string m_MistralKey;    // Voxtral key (self-similar voice)
    VoxtralClient m_Voxtral;
    string m_CacheDir;
    AudioSource m_AudioSource;
    Coroutine m_SpeakCoroutine;
    string m_CurrentContext;

    public const string ContentVersion = "matched-voice-v2";
    public string LastError { get; private set; }
    public bool ProviderPolicyBlocked => LastError != null && LastError.Contains("guardrail_violation");
    public string PreparationStage { get; private set; }
    public bool LibraryReady { get; private set; }
    bool m_Preparing;
    bool m_GeneratedAudio;
    readonly Dictionary<string, AudioClip> m_PreparedClips = new Dictionary<string, AudioClip>();
    readonly List<ClipAudit> m_Audit = new List<ClipAudit>();
    string m_ActiveClipKey;
    public event Action<string, string, string, string> Telemetry;
    public event Action<string> PlaybackFailed;

    [Serializable] class ClipAudit
    {
        public string clip_id, voice_id, provider, model, text, content_version;
        public float duration_seconds, rms, peak, gain, achieved_rms;
    }
    [Serializable] class LibraryAudit { public List<ClipAudit> clips; }

    void Fail(string reason)
    {
        LastError = reason;
        Telemetry?.Invoke("audio_failure", m_CurrentContext ?? "", m_ActiveClipKey ?? "", reason);
        Debug.LogWarning($"{k_Tag} {reason}");
        if (!m_Preparing) PlaybackFailed?.Invoke(reason);
    }

    public IEnumerator PrepareLibraries(string[] phrases, Action<string> progress, Action<bool> done)
    {
        Stop();
        LibraryReady = false;
        m_Preparing = true;
        m_Audit.Clear();
        foreach (var clip in m_PreparedClips.Values) Destroy(clip);
        m_PreparedClips.Clear();
        var originalVoice = SessionConfig.Voice;
        bool success = true;
        foreach (var voice in new[] { VoiceCondition.Generic, VoiceCondition.SelfSimilar })
        {
            SessionConfig.Voice = voice;
            for (int i = 0; i < phrases.Length; i++)
            {
                PreparationStage = $"Preparing {(voice == VoiceCondition.Generic ? "neutral" : "self-similar")} voice: {i + 1}/{phrases.Length}";
                progress?.Invoke(PreparationStage);
                m_PreparingText = phrases[i];
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    yield return SpeakCoroutine(phrases[i]);
                    if (string.IsNullOrEmpty(LastError) || LastError.Contains("unavailable") || ProviderPolicyBlocked) break;
                    if (attempt < 2) yield return new WaitForSeconds(1 << attempt);
                }
                if (!string.IsNullOrEmpty(LastError)) { success = false; break; }
                // Local cache reads need no provider pacing delay.
                if (m_GeneratedAudio) yield return new WaitForSeconds(0.1f);
            }
            if (!success) break;
        }
        if (success)
        {
            PreparationStage = "Matching audio levels";
            progress?.Invoke(PreparationStage);
            success = MatchLibraryLevels();
        }
        SessionConfig.Voice = originalVoice;
        m_Preparing = false;
        LibraryReady = success;
        if (success)
        {
            PreparationStage = "Saving voice manifest";
            try { File.WriteAllText(Path.Combine(SessionConfig.ParticipantPath, "voice-library-manifest.json"),
                JsonUtility.ToJson(new LibraryAudit { clips = m_Audit }, true)); }
            catch (Exception e) { LibraryReady = false; Fail("Could not save voice manifest: " + e.Message); }
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
            double sum = 0;
            for (int i = 0; i < samples.Length; i++) { samples[i] *= adjustment; sum += samples[i] * samples[i]; }
            item.achieved_rms = (float)Math.Sqrt(sum / Math.Max(1, samples.Length));
            if (Math.Abs(item.achieved_rms - target) > target * 0.01f || !clip.SetData(samples, 0))
            { Fail("Could not match voice library levels."); return false; }
            item.gain *= adjustment;
        }
        return true;
    }

    public bool IsSpeaking => m_AudioSource != null && m_AudioSource.isPlaying;
    public bool IsBusy => m_SpeakCoroutine != null || IsSpeaking;

    /// <summary>Shared Voxtral client (voice enrollment reuses this instance).</summary>
    public VoxtralClient Voxtral => m_Voxtral;

    public void Initialize(string elevenLabsKey, string mistralKey = null)
    {
        m_ApiKey = elevenLabsKey;
        m_MistralKey = mistralKey;

        m_Voxtral = GetComponent<VoxtralClient>();
        if (m_Voxtral == null) m_Voxtral = gameObject.AddComponent<VoxtralClient>();
        m_Voxtral.Initialize(mistralKey);

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

    public void Speak(string text, string context = null)
    {
        // Per-voice key checks happen inside the coroutine (self-similar uses
        // Voxtral, generic uses ElevenLabs), so we don't hard-block here.
        Stop();
        m_CurrentContext = context;
        m_SpeakCoroutine = StartCoroutine(SpeakCoroutine(text));
    }

    public void Stop()
    {
        if (m_SpeakCoroutine != null)
        {
            StopCoroutine(m_SpeakCoroutine);
            m_SpeakCoroutine = null;
        }

        if (m_AudioSource != null && m_AudioSource.isPlaying)
            m_AudioSource.Stop();

        if (m_ActiveClipKey != null) Telemetry?.Invoke("audio_cancelled", m_CurrentContext ?? "", m_ActiveClipKey, "");
        m_ActiveClipKey = null;
        m_CurrentContext = null;
    }

    public void InterruptIfAbout(string context)
    {
        if (!string.IsNullOrEmpty(m_CurrentContext) &&
            m_CurrentContext.Equals(context, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"{k_Tag} Interrupting speech about '{context}'");
            Stop();
        }
    }

    IEnumerator SpeakCoroutine(string text)
    {
        // Defer once so Speak has stored the coroutine handle before any early exit.
        yield return null;
        LastError = null;
        m_GeneratedAudio = false;
        bool wantSelfSimilar = SessionConfig.Voice == VoiceCondition.SelfSimilar;
        string voiceId = wantSelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
        string voiceScope = wantSelfSimilar ? $"vx-{voiceId}" : $"el-{voiceId}";

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
        Telemetry?.Invoke("audio_request", m_CurrentContext ?? "", m_ActiveClipKey, voiceScope);
        if (LibraryReady && !m_Preparing)
        {
            if (!m_PreparedClips.ContainsKey(cachePath)) Fail("Clip absent from prepared library.");
            else yield return PlayFromFile(cachePath);
            m_SpeakCoroutine = null; m_CurrentContext = null;
            yield break;
        }
        if (SessionConfig.VoiceBlocksEnabled && !m_Preparing)
        {
            Fail("Voice library is not ready.");
            m_SpeakCoroutine = null; m_CurrentContext = null;
            yield break;
        }
        if (File.Exists(cachePath))
        {
            Debug.Log($"{k_Tag} Cache hit ({(wantSelfSimilar ? "self" : "generic")}): \"{Truncate(text, 40)}\"");
            yield return PlayFromFile(cachePath);
            m_SpeakCoroutine = null;
            m_CurrentContext = null;
            yield break;
        }

        byte[] audioData = null;

        // 2) Synthesize the intended voice (Voxtral for self-similar).
        if (wantSelfSimilar)
        {
            Debug.Log($"{k_Tag} Voxtral TTS: \"{Truncate(text, 40)}\"");
            if (m_Voxtral == null || !m_Voxtral.HasKey)
            {
                Fail("Self-similar provider unavailable.");
                m_SpeakCoroutine = null;
                m_CurrentContext = null;
                yield break;
            }
            string vxErr = null;
            yield return m_Voxtral.Synthesize(text, voiceId,
                b => audioData = b, e => vxErr = e);
            if (audioData == null)
            {
                Fail($"Self-similar synthesis failed: {vxErr}");
                m_SpeakCoroutine = null;
                m_CurrentContext = null;
                yield break;
            }
        }

        // Neutral synthesis is reachable only for an explicitly neutral run.
        else
        {
            if (string.IsNullOrEmpty(m_ApiKey))
            {
                Fail("Neutral voice provider unavailable.");
                m_SpeakCoroutine = null;
                m_CurrentContext = null;
                yield break;
            }

            Debug.Log($"{k_Tag} ElevenLabs TTS: \"{Truncate(text, 40)}\"");
            string url = $"{k_BaseUrl}{voiceId}";
            string jsonBody = JsonUtility.ToJson(new TtsRequest { text = text, model_id = k_Model });

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", m_ApiKey);
            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string body = request.downloadHandler?.text ?? "";
                Fail($"Neutral synthesis failed: HTTP {request.responseCode}");
                m_SpeakCoroutine = null;
                m_CurrentContext = null;
                yield break;
            }
            audioData = request.downloadHandler.data;
        }

        if (audioData == null || audioData.Length < 100)
        {
            Fail("Synthesis returned empty or truncated audio.");
            m_SpeakCoroutine = null;
            m_CurrentContext = null;
            yield break;
        }

        // Save to cache (path matches whichever voice actually produced the audio).
        try
        {
            File.WriteAllBytes(cachePath, audioData);
            m_GeneratedAudio = true;
            Debug.Log($"{k_Tag} Cached: \"{Truncate(text, 40)}\" ({audioData.Length} bytes)");
        }
        catch (Exception e)
        {
            Fail("Could not save audio: " + e.Message);
            m_SpeakCoroutine = null; m_CurrentContext = null;
            yield break;
        }

        yield return PlayFromFile(cachePath);

        m_SpeakCoroutine = null;
        m_CurrentContext = null;
    }

    void RejectClip(AudioClip clip, string path, string reason)
    {
        if (clip != null) Destroy(clip);
        try { File.Delete(path); } catch (IOException) { }
        Fail(reason);
    }

    IEnumerator PlayFromFile(string filePath)
    {
        AudioClip clip;
        if (!m_PreparedClips.TryGetValue(filePath, out clip))
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG))
            {
                yield return request.SendWebRequest();
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
            double sum = 0; float peak = 0;
            foreach (float sample in samples) { sum += sample * sample; peak = Math.Max(peak, Math.Abs(sample)); }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, samples.Length));
            if (rms < 0.00001f) { RejectClip(clip, filePath, "Audio is silent."); yield break; }
            // Same RMS target (-20 dBFS), peak cap and playback rate for both providers.
            float gain = Math.Min(4f, Math.Min(0.1f / rms, 0.9f / peak));
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            if (!clip.SetData(samples, 0)) { RejectClip(clip, filePath, "Could not normalize decoded audio."); yield break; }
            m_PreparedClips[filePath] = clip;
            m_Audit.Add(new ClipAudit { clip_id = Path.GetFileNameWithoutExtension(filePath),
                voice_id = SessionConfig.Voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId,
                provider = SessionConfig.Voice == VoiceCondition.SelfSimilar ? "mistral" : "elevenlabs",
                model = SessionConfig.Voice == VoiceCondition.SelfSimilar ? "voxtral-mini-tts-2603" : k_Model,
                text = m_Preparing ? m_PreparingText : "", content_version = ContentVersion,
                duration_seconds = clip.length, rms = rms, peak = peak, gain = gain });
        }
        Telemetry?.Invoke("audio_ready", m_CurrentContext ?? "", Path.GetFileNameWithoutExtension(filePath), $"duration={clip.length:F4}");
        if (m_Preparing) yield break;
        m_AudioSource.clip = clip;
        m_AudioSource.volume = 0.7f;
        m_AudioSource.pitch = 1f;
        m_AudioSource.Play();
        Telemetry?.Invoke("audio_playback_start", m_CurrentContext ?? "", m_ActiveClipKey, $"dsp_time={AudioSettings.dspTime:F6}");
        while (m_AudioSource.isPlaying) yield return null;
        Telemetry?.Invoke("audio_playback_end", m_CurrentContext ?? "", m_ActiveClipKey, $"dsp_time={AudioSettings.dspTime:F6}");
        m_ActiveClipKey = null;
    }

    string m_PreparingText;

    /// <summary>
    /// Pre-generates and caches audio for a list of phrases in the background.
    /// Call at game start so tips play instantly during gameplay.
    /// </summary>
    public void PreCachePhrases(string[] phrases)
    {
        StartCoroutine(PreCacheCoroutine(phrases));
    }

    IEnumerator PreCacheCoroutine(string[] phrases)
    {
        string neutralVoiceId = SessionConfig.NeutralVoiceId;
        int cached = 0;
        int skipped = 0;

        foreach (string phrase in phrases)
        {
            string path = GetCachePath(phrase, $"el-{neutralVoiceId}"); // pre-cache targets the generic voice
            if (File.Exists(path))
            {
                skipped++;
                continue;
            }

            // Throttle: one request at a time, small delay between
            string url = $"{k_BaseUrl}{neutralVoiceId}";
            string jsonBody = JsonUtility.ToJson(new TtsRequest
            {
                text = phrase,
                model_id = k_Model
            });

            using var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", m_ApiKey);
            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success &&
                request.downloadHandler.data != null &&
                request.downloadHandler.data.Length >= 100)
            {
                try
                {
                    File.WriteAllBytes(path, request.downloadHandler.data);
                    cached++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{k_Tag} PreCache write failed: {e.Message}");
                }
            }

            // Small yield to avoid blocking
            yield return null;
        }

        Debug.Log($"{k_Tag} PreCache complete: {cached} new, {skipped} already cached, {phrases.Length} total");
    }

    /// <summary>
    /// Generates a deterministic cache file path from the text content.
    /// Uses a simple hash to avoid filesystem issues with long/special-char filenames.
    /// </summary>
    string GetCachePath(string text, string scope)
    {
        // Scope the key by voice so generic vs self-similar (and different clones)
        // never collide on identical hint text. FNV-1a over "scope|text".
        string model = scope.StartsWith("vx-") ? "voxtral-mini-tts-2603" : k_Model;
        string keyed = ContentVersion + "|" + model + "|" + scope + "|" + text;
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

    [Serializable]
    struct TtsRequest
    {
        public string text;
        public string model_id;
    }
}
