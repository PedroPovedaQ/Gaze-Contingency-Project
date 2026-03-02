using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Calls ElevenLabs TTS API and plays audio via a non-spatialized AudioSource.
/// Supports interruption for clean speech cutoff when the player finds an object.
/// Uses PCM format for direct AudioClip creation without MP3 decoding.
/// </summary>
public class VoiceSynthesizer : MonoBehaviour
{
    const string k_Tag = "[VoiceSynth]";
    const string k_BaseUrl = "https://api.elevenlabs.io/v1/text-to-speech/";
    const string k_VoiceId = "21m00Tcm4TlvDq8ikWAM"; // Rachel — warm, clear
    const string k_Model = "eleven_turbo_v2_5";

    string m_ApiKey;
    AudioSource m_AudioSource;
    Coroutine m_SpeakCoroutine;
    string m_CurrentContext; // what the current speech is about

    public bool IsSpeaking => m_AudioSource != null && m_AudioSource.isPlaying;

    public void Initialize(string apiKey)
    {
        m_ApiKey = apiKey;

        m_AudioSource = gameObject.AddComponent<AudioSource>();
        m_AudioSource.spatialBlend = 0f; // 2D — voice "in the head"
        m_AudioSource.volume = 0.7f;
        m_AudioSource.playOnAwake = false;

        Debug.Log($"{k_Tag} Initialized");
    }

    /// <summary>
    /// Speak the given text. Cancels any in-progress speech.
    /// </summary>
    /// <param name="text">Text to synthesize</param>
    /// <param name="context">Optional context tag for interruption matching</param>
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

    /// <summary>
    /// Immediately stop any playing or in-progress speech.
    /// </summary>
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

    /// <summary>
    /// Stop speech only if it's about the given context (e.g., the object just found).
    /// </summary>
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
        Debug.Log($"{k_Tag} Requesting TTS: \"{text}\"");

        // Use default mp3 format (available on all ElevenLabs tiers)
        string url = $"{k_BaseUrl}{k_VoiceId}";

        string jsonBody = JsonUtility.ToJson(new TtsRequest
        {
            text = text,
            model_id = k_Model
        });

        // First request: get MP3 bytes
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

        // Save MP3 to temp file and load via DownloadHandlerAudioClip
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "tts_temp.mp3");
        System.IO.File.WriteAllBytes(tempPath, mp3Data);

        string fileUrl = "file://" + tempPath;
        using (var audioRequest = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"{k_Tag} Failed to decode MP3: {audioRequest.error}");
                m_SpeakCoroutine = null;
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
            if (clip == null || clip.length < 0.1f)
            {
                Debug.LogWarning($"{k_Tag} Decoded clip is empty");
                m_SpeakCoroutine = null;
                yield break;
            }

            m_AudioSource.clip = clip;
            m_AudioSource.Play();

            Debug.Log($"{k_Tag} Playing TTS ({clip.length:F1}s)");

            // Wait for playback to finish
            while (m_AudioSource.isPlaying)
                yield return null;
        }

        // Cleanup temp file
        try { System.IO.File.Delete(tempPath); } catch { }

        m_SpeakCoroutine = null;
        m_CurrentContext = null;
    }

    [Serializable]
    struct TtsRequest
    {
        public string text;
        public string model_id;
    }
}
