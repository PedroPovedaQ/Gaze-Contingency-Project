using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Thin client for Mistral Voxtral: clone a voice from an audio sample and
/// synthesize speech from a cloned voice id. Ported from the speedreadify
/// implementation (voice-clone/route.ts, voxtral-tts.ts).
///
/// NOTE: This is a cloud service. Using it with real participant voices sends
/// identifiable audio to api.mistral.ai and stores the clone on Mistral's
/// servers — that requires an approved IRB modification. For dev/testing with
/// the researcher's own voice it is fine.
/// </summary>
public class VoxtralClient : MonoBehaviour
{
    const string k_Tag = "[Voxtral]";
    const string k_CloneUrl = "https://api.mistral.ai/v1/audio/voices";
    const string k_SpeechUrl = "https://api.mistral.ai/v1/audio/speech";
    const string k_TtsModel = "voxtral-mini-tts-2603";

    string m_ApiKey;

    public bool HasKey => !string.IsNullOrEmpty(m_ApiKey);

    public void Initialize(string apiKey)
    {
        m_ApiKey = apiKey;
        Debug.Log($"{k_Tag} Initialized. Key={(HasKey ? "present" : "MISSING")}");
    }

    /// <summary>
    /// Clone a voice from a WAV byte buffer. Calls onVoiceId with the Mistral
    /// voice id on success, or onError with a message on failure.
    /// </summary>
    public IEnumerator CloneVoice(byte[] wavBytes, string displayName,
        Action<string> onVoiceId, Action<string> onError)
    {
        if (!HasKey) { onError?.Invoke("no Mistral API key"); yield break; }
        if (wavBytes == null || wavBytes.Length < 1000) { onError?.Invoke("sample too small"); yield break; }

        string sampleB64 = Convert.ToBase64String(wavBytes);
        string body = JsonUtility.ToJson(new CloneRequest
        {
            name = string.IsNullOrEmpty(displayName) ? "study_participant" : displayName,
            sample_audio = sampleB64,
            sample_filename = "sample.wav"
        });

        using (var req = new UnityWebRequest(k_CloneUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {m_ApiKey}");
            req.timeout = 60;

            Debug.Log($"{k_Tag} Cloning voice ({wavBytes.Length} bytes)...");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"clone HTTP {req.responseCode}: {req.error} {req.downloadHandler?.text}");
                yield break;
            }

            string id = null;
            try { id = JsonUtility.FromJson<IdResponse>(req.downloadHandler.text).id; }
            catch (Exception e) { onError?.Invoke($"clone parse failed: {e.Message}"); yield break; }

            if (string.IsNullOrEmpty(id)) { onError?.Invoke("clone returned no id"); yield break; }
            Debug.Log($"{k_Tag} Cloned voice id={id}");
            onVoiceId?.Invoke(id);
        }
    }

    /// <summary>
    /// Synthesize <paramref name="text"/> in the cloned voice. Returns audio
    /// bytes (MP3) via onAudio, or onError on failure.
    /// </summary>
    public IEnumerator Synthesize(string text, string voiceId,
        Action<byte[]> onAudio, Action<string> onError)
    {
        if (!HasKey) { onError?.Invoke("no Mistral API key"); yield break; }
        if (string.IsNullOrEmpty(voiceId)) { onError?.Invoke("no voice id"); yield break; }

        string body = JsonUtility.ToJson(new SpeechRequest
        {
            model = k_TtsModel,
            input = text,
            voice_id = voiceId,
            response_format = "mp3"
        });

        using (var req = new UnityWebRequest(k_SpeechUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {m_ApiKey}");
            req.timeout = 30;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"tts HTTP {req.responseCode}: {req.error} {req.downloadHandler?.text}");
                yield break;
            }

            // Voxtral returns JSON with base64 in `audio_data`. Only treat the body
            // as raw audio when it is clearly NOT JSON — otherwise a JSON status
            // payload would be written to the permanent cache as bogus "audio".
            byte[] audio = null;
            string raw = req.downloadHandler.text;
            string ctype = req.GetResponseHeader("Content-Type") ?? "";
            bool looksJson = ctype.ToLowerInvariant().Contains("json") ||
                             (!string.IsNullOrEmpty(raw) && raw.TrimStart().StartsWith("{"));

            if (looksJson)
            {
                string b64 = null;
                try { b64 = JsonUtility.FromJson<SpeechResponse>(raw).audio_data; }
                catch (Exception e) { onError?.Invoke($"tts parse failed: {e.Message}"); yield break; }
                if (string.IsNullOrEmpty(b64)) { onError?.Invoke($"tts JSON had no audio_data: {Truncate(raw)}"); yield break; }
                try { audio = Convert.FromBase64String(b64); }
                catch (Exception e) { onError?.Invoke($"tts base64 decode failed: {e.Message}"); yield break; }
            }
            else
            {
                audio = req.downloadHandler.data; // genuine binary audio response
            }

            if (audio == null || audio.Length < 100) { onError?.Invoke("tts returned empty audio"); yield break; }
            onAudio?.Invoke(audio);
        }
    }

    static string Truncate(string s, int max = 200)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "...");

    [Serializable] struct CloneRequest { public string name; public string sample_audio; public string sample_filename; }
    [Serializable] struct IdResponse { public string id; }
    [Serializable] struct SpeechRequest { public string model; public string input; public string voice_id; public string response_format; }
    [Serializable] struct SpeechResponse { public string audio_data; }
}
