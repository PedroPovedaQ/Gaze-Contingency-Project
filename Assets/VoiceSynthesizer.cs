using System;
using System.Collections;
using System.IO;
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
    const string k_VoiceId = "21m00Tcm4TlvDq8ikWAM"; // Rachel — warm, clear
    const string k_Model = "eleven_turbo_v2_5";
    const string k_CacheFolder = "tts_cache";

    string m_ApiKey;
    string m_CacheDir;
    AudioSource m_AudioSource;
    Coroutine m_SpeakCoroutine;
    string m_CurrentContext;

    public bool IsSpeaking => m_AudioSource != null && m_AudioSource.isPlaying;

    public void Initialize(string apiKey)
    {
        m_ApiKey = apiKey;

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

    public void Speak(string text, string context = null)
    {
        if (string.IsNullOrEmpty(m_ApiKey))
        {
            Debug.LogWarning($"{k_Tag} No API key, skipping TTS");
            return;
        }

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
        string cachePath = GetCachePath(text);

        // Check cache first
        if (File.Exists(cachePath))
        {
            Debug.Log($"{k_Tag} Cache hit: \"{Truncate(text, 40)}\"");
            yield return PlayFromFile(cachePath);
            m_SpeakCoroutine = null;
            m_CurrentContext = null;
            yield break;
        }

        // Cache miss — download from ElevenLabs
        Debug.Log($"{k_Tag} Cache miss, requesting TTS: \"{Truncate(text, 40)}\"");

        string url = $"{k_BaseUrl}{k_VoiceId}";
        string jsonBody = JsonUtility.ToJson(new TtsRequest
        {
            text = text,
            model_id = k_Model
        });

        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("xi-api-key", m_ApiKey);
        request.timeout = 15;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string body = request.downloadHandler?.text ?? "";
            Debug.LogWarning($"{k_Tag} TTS failed: {request.error} (HTTP {request.responseCode}) {body}");
            m_SpeakCoroutine = null;
            yield break;
        }

        byte[] mp3Data = request.downloadHandler.data;
        if (mp3Data == null || mp3Data.Length < 100)
        {
            Debug.LogWarning($"{k_Tag} TTS returned empty audio");
            m_SpeakCoroutine = null;
            yield break;
        }

        // Save to cache
        try
        {
            File.WriteAllBytes(cachePath, mp3Data);
            Debug.Log($"{k_Tag} Cached: \"{Truncate(text, 40)}\" ({mp3Data.Length} bytes)");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{k_Tag} Failed to cache: {e.Message}");
        }

        // Play from cached file
        yield return PlayFromFile(cachePath);

        m_SpeakCoroutine = null;
        m_CurrentContext = null;
    }

    IEnumerator PlayFromFile(string filePath)
    {
        string fileUrl = "file://" + filePath;
        using (var audioRequest = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"{k_Tag} Failed to decode MP3: {audioRequest.error}");
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
            if (clip == null || clip.length < 0.1f)
            {
                Debug.LogWarning($"{k_Tag} Decoded clip is empty");
                yield break;
            }

            m_AudioSource.clip = clip;
            m_AudioSource.Play();

            Debug.Log($"{k_Tag} Playing ({clip.length:F1}s)");

            while (m_AudioSource.isPlaying)
                yield return null;
        }
    }

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
        int cached = 0;
        int skipped = 0;

        foreach (string phrase in phrases)
        {
            string path = GetCachePath(phrase);
            if (File.Exists(path))
            {
                skipped++;
                continue;
            }

            // Throttle: one request at a time, small delay between
            string url = $"{k_BaseUrl}{k_VoiceId}";
            string jsonBody = JsonUtility.ToJson(new TtsRequest
            {
                text = phrase,
                model_id = k_Model
            });

            var request = new UnityWebRequest(url, "POST");
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
    string GetCachePath(string text)
    {
        // Stable hash: FNV-1a
        uint hash = 2166136261;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return Path.Combine(m_CacheDir, $"tts_{hash:X8}.mp3");
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
