using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

/// <summary>
/// Decides when to request a hint and calls OpenAI GPT-4o-mini.
/// Uses timing/debounce logic to avoid spammy hints.
/// Incorporates eye gaze context for spatially aware suggestions.
/// </summary>
public class HintGenerator : MonoBehaviour
{
    const string k_Tag = "[HintGen]";
    const string k_ApiUrl = "https://api.openai.com/v1/chat/completions";
    const string k_Model = "gpt-4o-mini";
    const int k_MaxTokens = 80;
    const float k_Temperature = 0.8f;

    // Timing constants
    const float k_FirstHintDelay = 15f;    // seconds after objective starts
    const float k_SubsequentInterval = 20f; // between automatic hints
    const float k_WrongGrabDelay = 3f;      // hint after wrong grab
    const float k_GazeNudgeTime = 10f;      // gazing at target without grabbing
    const float k_MinGap = 8f;              // minimum gap between any hints

    const string k_SystemPrompt =
        "You are a friendly VR game assistant helping a player find colored shapes on a table. " +
        "Give SHORT hints (1-2 sentences, under 30 words). Use spatial directions (left, right, ahead, behind). " +
        "Be warm and encouraging. Don't give exact answers immediately — get more specific if the player " +
        "has been struggling longer. Vary your phrasing. " +
        "You have access to the player's eye gaze data — use it to guide them. " +
        "If they're looking in the wrong direction, gently redirect. " +
        "If they're close to the target, encourage them.";

    string m_ApiKey;
    AgentContext m_Context;
    VoiceSynthesizer m_Voice;

    float m_ObjectiveStartTime;
    float m_LastHintTime;
    float m_WrongGrabTime;
    float m_GazeOnTargetStart;
    bool m_GazeNudgeGiven;
    bool m_CancelRequested;
    Coroutine m_GenerateCoroutine;

    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice)
    {
        m_ApiKey = apiKey;
        m_Context = context;
        m_Voice = voice;
        Debug.Log($"{k_Tag} Initialized");
    }

    /// <summary>
    /// Call when a new objective starts (game start or object found).
    /// Resets all hint timers.
    /// </summary>
    public void OnNewObjective()
    {
        CancelPending();
        m_ObjectiveStartTime = Time.time;
        m_WrongGrabTime = 0f;
        m_GazeOnTargetStart = 0f;
        m_GazeNudgeGiven = false;
    }

    /// <summary>
    /// Call when the player grabs the wrong object.
    /// </summary>
    public void OnWrongGrab()
    {
        m_WrongGrabTime = Time.time;
    }

    /// <summary>
    /// Cancel any in-flight hint generation.
    /// </summary>
    public void CancelPending()
    {
        m_CancelRequested = true;
        if (m_GenerateCoroutine != null)
        {
            StopCoroutine(m_GenerateCoroutine);
            m_GenerateCoroutine = null;
        }
        m_CancelRequested = false;
    }

    void Update()
    {
        if (m_Context == null || m_Voice == null) return;
        if (string.IsNullOrEmpty(m_ApiKey)) return;

        // Don't queue if already generating or voice is playing
        if (m_GenerateCoroutine != null || m_Voice.IsSpeaking) return;

        float now = Time.time;
        float sinceObjective = now - m_ObjectiveStartTime;
        float sinceLastHint = now - m_LastHintTime;

        // Enforce minimum gap
        if (sinceLastHint < k_MinGap) return;

        // Track gaze on target for nudge
        if (m_Context.IsGazingAtTarget())
        {
            if (m_GazeOnTargetStart <= 0f)
                m_GazeOnTargetStart = now;

            if (!m_GazeNudgeGiven && (now - m_GazeOnTargetStart) >= k_GazeNudgeTime)
            {
                m_GazeNudgeGiven = true;
                RequestHint("The player has been looking at the target object for a while but hasn't grabbed it. Give an encouraging nudge to pick it up.");
                return;
            }
        }
        else
        {
            m_GazeOnTargetStart = 0f;
        }

        // After wrong grab — hint after short delay
        if (m_WrongGrabTime > 0f && (now - m_WrongGrabTime) >= k_WrongGrabDelay)
        {
            m_WrongGrabTime = 0f;
            RequestHint("The player just grabbed the wrong object. Help redirect them to the correct one.");
            return;
        }

        // First hint after delay
        if (m_LastHintTime < m_ObjectiveStartTime && sinceObjective >= k_FirstHintDelay)
        {
            RequestHint("The player has been searching for a while. Give a helpful spatial hint.");
            return;
        }

        // Subsequent hints at interval
        if (sinceLastHint >= k_SubsequentInterval && sinceObjective >= k_FirstHintDelay)
        {
            float totalSearchTime = sinceObjective;
            string urgency = totalSearchTime > 60f
                ? "The player has been struggling for over a minute. Give a more specific, direct hint."
                : "Give another helpful hint. Be a bit more specific than before.";
            RequestHint(urgency);
        }
    }

    void RequestHint(string situationDescription)
    {
        m_GenerateCoroutine = StartCoroutine(GenerateHint(situationDescription));
    }

    IEnumerator GenerateHint(string situationDescription)
    {
        string contextPrompt = m_Context.BuildContextPrompt();
        if (string.IsNullOrEmpty(contextPrompt))
        {
            m_GenerateCoroutine = null;
            yield break;
        }

        string userMessage = $"{situationDescription}\n\nCurrent scene state:\n{contextPrompt}";

        // Build JSON manually to avoid serialization issues with nested arrays
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"model\":\"{k_Model}\",");
        sb.Append($"\"max_tokens\":{k_MaxTokens},");
        sb.Append($"\"temperature\":{k_Temperature},");
        sb.Append("\"messages\":[");
        sb.Append($"{{\"role\":\"system\",\"content\":{EscapeJson(k_SystemPrompt)}}},");
        sb.Append($"{{\"role\":\"user\",\"content\":{EscapeJson(userMessage)}}}");
        sb.Append("]}");

        string jsonBody = sb.ToString();

        var request = new UnityWebRequest(k_ApiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {m_ApiKey}");
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (m_CancelRequested)
        {
            m_GenerateCoroutine = null;
            yield break;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"{k_Tag} OpenAI request failed: {request.error} (HTTP {request.responseCode})");
            m_GenerateCoroutine = null;
            yield break;
        }

        // Parse response using Newtonsoft.Json
        string hintText = null;
        try
        {
            var json = JObject.Parse(request.downloadHandler.text);
            hintText = json["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"{k_Tag} Failed to parse OpenAI response: {e.Message}");
        }

        if (string.IsNullOrEmpty(hintText))
        {
            Debug.LogWarning($"{k_Tag} Empty hint from OpenAI");
            m_GenerateCoroutine = null;
            yield break;
        }

        Debug.Log($"{k_Tag} Generated hint: \"{hintText}\"");

        m_LastHintTime = Time.time;
        m_Voice.Speak(hintText, "hint");

        m_GenerateCoroutine = null;
    }

    static string EscapeJson(string s)
    {
        return "\"" + s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            + "\"";
    }
}
