using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>ElevenLabs instant voice cloning and speech. Enrollment uploads the
/// new microphone sample to ElevenLabs; existing Voxtral clones are not migrated.</summary>
public class ElevenLabsVoiceClient : MonoBehaviour
{
    public const string Model = "eleven_turbo_v2_5";
    const string CloneUrl = "https://api.elevenlabs.io/v1/voices/add";
    const string SpeechUrl = "https://api.elevenlabs.io/v1/text-to-speech/";
    string m_ApiKey;
    public bool HasKey => !string.IsNullOrWhiteSpace(m_ApiKey);
    public void Initialize(string apiKey) => m_ApiKey = apiKey;

    /// <summary>Submit a WAV as multipart files, then accept only a usable clone ID.
    /// Verification-required voices must be verified in ElevenLabs before enrollment is retried.</summary>
    public IEnumerator CloneVoice(byte[] wavBytes, string displayName,
        Action<string> onVoiceId, Action<string> onError)
    {
        if (!HasKey) { onError?.Invoke("No ElevenLabs API key."); yield break; }
        if (wavBytes == null || wavBytes.Length < 1000) { onError?.Invoke("Recording is too short."); yield break; }
        var form = new WWWForm();
        form.AddField("name", string.IsNullOrEmpty(displayName) ? "study_participant" : displayName);
        form.AddBinaryData("files", wavBytes, "sample.wav", "audio/wav");
        form.AddField("remove_background_noise", "false");
        using (var request = UnityWebRequest.Post(CloneUrl, form))
        {
            request.SetRequestHeader("xi-api-key", m_ApiKey);
            request.timeout = 60;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(Failure("Cloning", request));
                yield break;
            }
            CloneResponse response;
            try { response = JsonUtility.FromJson<CloneResponse>(request.downloadHandler.text); }
            catch (Exception) { onError?.Invoke("ElevenLabs returned an invalid clone response."); yield break; }
            if (response.requires_verification)
            {
                onError?.Invoke("ElevenLabs requires voice verification. Ask the researcher to resolve verification in ElevenLabs before recording again.");
                yield break;
            }
            if (string.IsNullOrWhiteSpace(response.voice_id))
            { onError?.Invoke("ElevenLabs returned no clone ID."); yield break; }
            onVoiceId?.Invoke(response.voice_id);
        }
    }

    /// <summary>Generate MP3 with the same model as the neutral voices. Reject JSON
    /// status bodies even on HTTP 200 so they never become cached audio.</summary>
    public IEnumerator Synthesize(string text, string voiceId, Action<byte[]> onAudio, Action<string> onError)
    {
        if (!HasKey) { onError?.Invoke("No ElevenLabs API key."); yield break; }
        if (string.IsNullOrWhiteSpace(voiceId)) { onError?.Invoke("No cloned voice ID."); yield break; }
        using (var request = new UnityWebRequest(SpeechUrl + Uri.EscapeDataString(voiceId), "POST"))
        {
            var body = JsonUtility.ToJson(new SpeechRequest { text = text, model_id = Model });
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "audio/mpeg");
            request.SetRequestHeader("xi-api-key", m_ApiKey);
            request.timeout = 30;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            { onError?.Invoke(Failure("Speech", request)); yield break; }
            string contentType = request.GetResponseHeader("Content-Type") ?? "";
            if (!contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            { onError?.Invoke("ElevenLabs returned a non-audio response."); yield break; }
            var bytes = request.downloadHandler.data;
            if (bytes == null || bytes.Length < 100)
            { onError?.Invoke("ElevenLabs returned empty audio."); yield break; }
            onAudio?.Invoke(bytes);
        }
    }

    // Deliberately omit raw provider bodies: they may contain account or recording details.
    static string Failure(string operation, UnityWebRequest request)
    {
        string action = request.responseCode == 401 || request.responseCode == 403
            ? " Check the API key permissions and cloning access."
            : request.responseCode == 429 ? " Check quota or wait before retrying." : " Please retry or check the ElevenLabs account.";
        return $"ElevenLabs {operation} failed (HTTP {request.responseCode}).{action}";
    }

    [Serializable] struct CloneResponse { public string voice_id; public bool requires_verification; }
    [Serializable] struct SpeechRequest { public string text; public string model_id; }
}
