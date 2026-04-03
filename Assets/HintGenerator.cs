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

    // Timing constants — adaptive ranges (5-15s based on gaze behavior)
    const float k_WrongGrabDelay = 3f;
    const float k_GazeNudgeTime = 10f;
    const float k_ZoneNeglectThreshold = 20f; // seconds without scanning a zone
    const int k_RevisitConfusionCount = 3;     // same object viewed 3+ times
    const float k_MinGap = 5f;

    const string k_SystemPrompt =
        "You are a friendly VR game assistant helping a player find colored shapes on a table and shelves. " +
        "Give SHORT hints (1-2 sentences, under 30 words). Use spatial directions (left, right, ahead, behind) " +
        "and vertical directions (table level, lower shelf, upper shelf). " +
        "Be warm and encouraging. Don't give exact answers immediately — get more specific if the player " +
        "has been struggling longer. Vary your phrasing. " +
        "You have access to the player's eye gaze data — use it to guide them. " +
        "If they're looking in the wrong area or zone, gently redirect. " +
        "If they haven't checked a shelf level, suggest it. " +
        "If they're close to the target, encourage them.";

    string m_ApiKey;
    AgentContext m_Context;
    VoiceSynthesizer m_Voice;
    GazeCoverageTracker m_CoverageTracker;

    float m_ObjectiveStartTime;
    float m_LastHintTime;
    float m_WrongGrabTime;
    float m_GazeOnTargetStart;
    bool m_GazeNudgeGiven;
    bool m_CancelRequested;
    Coroutine m_GenerateCoroutine;

    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null)
    {
        m_ApiKey = apiKey;
        m_Context = context;
        m_Voice = voice;
        m_CoverageTracker = coverageTracker;
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
        float sinceLastHint = now - m_LastHintTime;

        // Enforce minimum gap
        if (sinceLastHint < k_MinGap) return;

        // --- Priority 1: Gaze nudge (staring at target without grabbing) ---
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

        // --- Priority 2: Wrong grab redirect ---
        if (m_WrongGrabTime > 0f && (now - m_WrongGrabTime) >= k_WrongGrabDelay)
        {
            m_WrongGrabTime = 0f;
            RequestHint("The player just grabbed the wrong object. Help redirect them to the correct one.");
            return;
        }

        // --- Priority 3: Zone neglect (haven't looked at a shelf level) ---
        if (m_CoverageTracker != null)
        {
            string[] zoneNames = { "table", "lower shelf", "upper shelf" };
            for (int level = 0; level < 3; level++)
            {
                float lastScan = m_CoverageTracker.GetZoneLastScanTime(level);
                // Only trigger if the zone was previously scanned (lastScan > 0) and neglected,
                // or if enough time has passed since objective start and zone was never scanned
                float sinceObjective = now - m_ObjectiveStartTime;
                bool neverScanned = lastScan <= 0f && sinceObjective > k_ZoneNeglectThreshold;
                bool neglected = lastScan > 0f && (now - lastScan) > k_ZoneNeglectThreshold;

                if (neverScanned || neglected)
                {
                    RequestHint($"The player has not looked at the {zoneNames[level]} for a while. Suggest they check that area — the target might be there.");
                    return;
                }
            }
        }

        // --- Priority 4: Revisit confusion (same wrong object viewed repeatedly) ---
        if (m_CoverageTracker != null)
        {
            string confused = m_CoverageTracker.GetMostRevisitedNonTarget();
            if (confused != null)
            {
                int count = m_CoverageTracker.GetRevisitCount(confused);
                if (count >= k_RevisitConfusionCount)
                {
                    RequestHint($"The player keeps looking at {confused} repeatedly ({count} times). They seem confused about whether it's the target. Help redirect them clearly.");
                    return;
                }
            }
        }

        // --- Priority 5: Adaptive timed hints ---
        float adaptiveInterval = ComputeAdaptiveInterval();
        float timeSinceObjective = now - m_ObjectiveStartTime;

        // First hint after adaptive delay
        if (m_LastHintTime < m_ObjectiveStartTime && timeSinceObjective >= adaptiveInterval * 0.75f)
        {
            RequestHint(GetBehaviorSituationDescription());
            return;
        }

        // Subsequent hints at adaptive interval
        if (sinceLastHint >= adaptiveInterval)
        {
            RequestHint(GetBehaviorSituationDescription());
        }
    }

    float ComputeAdaptiveInterval()
    {
        if (m_CoverageTracker == null) return 10f; // fallback mid-range

        var behavior = m_CoverageTracker.ClassifyBehavior();
        return behavior switch
        {
            GazeCoverageTracker.GazeBehavior.Systematic => Random.Range(12f, 15f),
            GazeCoverageTracker.GazeBehavior.Normal     => Random.Range(9f, 12f),
            GazeCoverageTracker.GazeBehavior.Erratic    => Random.Range(5f, 7f),
            GazeCoverageTracker.GazeBehavior.Stuck      => Random.Range(7f, 10f),
            _                                           => 10f,
        };
    }

    string GetBehaviorSituationDescription()
    {
        if (m_CoverageTracker == null)
            return "Give a helpful spatial hint.";

        var behavior = m_CoverageTracker.ClassifyBehavior();
        float searchTime = Time.time - m_ObjectiveStartTime;

        return behavior switch
        {
            GazeCoverageTracker.GazeBehavior.Erratic =>
                "The player's gaze is erratic and unfocused — they seem lost. Give a clear, calming directional hint.",
            GazeCoverageTracker.GazeBehavior.Stuck =>
                $"The player has only been searching one area. {GetStuckZoneDescription()} Suggest they look elsewhere.",
            GazeCoverageTracker.GazeBehavior.Systematic when searchTime > 45f =>
                "The player is searching methodically but hasn't found it yet. Give a more specific hint.",
            GazeCoverageTracker.GazeBehavior.Systematic =>
                "The player is searching methodically. Give a gentle nudge without being pushy.",
            _ when searchTime > 60f =>
                "The player has been struggling for over a minute. Give a more direct, specific hint.",
            _ =>
                "Give another helpful hint. Be a bit more specific than before.",
        };
    }

    string GetStuckZoneDescription()
    {
        if (m_CoverageTracker == null) return "";

        // Find which zone has the least coverage
        string[] zoneNames = { "table", "lower shelf", "upper shelf" };
        float minFixation = float.MaxValue;
        string leastZone = "";
        for (int i = 0; i < 3; i++)
        {
            float total = m_CoverageTracker.GetZoneTotalFixation(i);
            if (total < minFixation)
            {
                minFixation = total;
                leastZone = zoneNames[i];
            }
        }
        return $"They haven't checked the {leastZone} much.";
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
