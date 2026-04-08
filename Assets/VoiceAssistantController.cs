using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Orchestrates the game assistant. Loads API keys at startup (before game can start),
/// then wires VoiceSynthesizer and HintGenerator for the duration of the game.
/// </summary>
public class VoiceAssistantController : MonoBehaviour
{
    const string k_Tag = "[VoiceAssist]";
    const string k_KeysFile = "api_keys.json";

    FindObjectGameManager m_GameManager;
    AgentContext m_AgentContext;
    HintGenerator m_HintGenerator;
    VoiceSynthesizer m_VoiceSynthesizer;
    GazeCoverageTracker m_CoverageTracker;
    Coroutine m_RoundAnnounceCoroutine;

    /// <summary>True once API keys are loaded and all sub-systems are ready.</summary>
    public bool IsReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var spawner = FindObjectOfType<ObjectSpawner>();
        if (spawner != null && spawner.GetComponent<VoiceAssistantController>() == null)
        {
            spawner.gameObject.AddComponent<VoiceAssistantController>();
            Debug.Log($"{k_Tag} Auto-attached to {spawner.gameObject.name}");
        }
    }

    void OnEnable()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
        if (m_GameManager == null)
        {
            Debug.LogWarning($"{k_Tag} No FindObjectGameManager found, disabling");
            enabled = false;
            return;
        }

        // Create all sub-components immediately (not in a coroutine)
        m_CoverageTracker = gameObject.AddComponent<GazeCoverageTracker>();
        m_CoverageTracker.Initialize(m_GameManager);

        m_AgentContext = gameObject.AddComponent<AgentContext>();
        var ui = GetComponent<FindObjectUI>();
        m_AgentContext.Initialize(m_GameManager, ui, m_CoverageTracker);

        m_VoiceSynthesizer = gameObject.AddComponent<VoiceSynthesizer>();
        m_HintGenerator = gameObject.AddComponent<HintGenerator>();

        // Subscribe to game events
        m_GameManager.OnGameStarted += HandleGameStarted;
        m_GameManager.OnObjectFound += HandleObjectFound;
        m_GameManager.OnWrongCapture += HandleWrongCapture;
        m_GameManager.OnGameCompleted += HandleGameCompleted;
        m_GameManager.OnRoundTransitionStarted += HandleRoundTransitionStarted;
        m_GameManager.OnRoundReady += HandleRoundReady;

        // Load API keys — blocks game start until ready
        StartCoroutine(LoadKeys());
    }

    IEnumerator LoadKeys()
    {
        string elevenLabsKey = "";
        string keysPath = System.IO.Path.Combine(Application.streamingAssetsPath, k_KeysFile);

        string url = keysPath;
        if (!url.StartsWith("jar:") && !url.StartsWith("http"))
            url = "file://" + url;

        using (var request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var keys = JsonUtility.FromJson<ApiKeys>(request.downloadHandler.text);
                elevenLabsKey = keys.elevenlabs_key;
                Debug.Log($"{k_Tag} Loaded API keys");
            }
            else
            {
                Debug.LogWarning($"{k_Tag} Could not load API keys: {request.error}");
            }
        }

        // Initialize sub-systems with the loaded key
        m_VoiceSynthesizer.Initialize(elevenLabsKey);
        m_HintGenerator.Initialize(elevenLabsKey, m_AgentContext, m_VoiceSynthesizer, m_CoverageTracker);

        IsReady = true;
        Debug.Log($"{k_Tag} Ready. Key={(!string.IsNullOrEmpty(elevenLabsKey) ? "present" : "MISSING")}");
    }

    void OnDisable()
    {
        if (m_RoundAnnounceCoroutine != null)
        {
            StopCoroutine(m_RoundAnnounceCoroutine);
            m_RoundAnnounceCoroutine = null;
        }

        if (m_GameManager != null)
        {
            m_GameManager.OnGameStarted -= HandleGameStarted;
            m_GameManager.OnObjectFound -= HandleObjectFound;
            m_GameManager.OnWrongCapture -= HandleWrongCapture;
            m_GameManager.OnGameCompleted -= HandleGameCompleted;
            m_GameManager.OnRoundTransitionStarted -= HandleRoundTransitionStarted;
            m_GameManager.OnRoundReady -= HandleRoundReady;
        }
    }

    void HandleGameStarted()
    {
        m_CoverageTracker?.Reset();
        // First-round goal announcement is now triggered by
        // FindObjectGameManager via OnRoundTransitionStarted, after the
        // fixation cross appears.
        if (m_HintGenerator != null)
            m_HintGenerator.CancelPending();
    }

    void HandleObjectFound(int roundIndex)
    {
        // Round ended: hard-stop any ongoing speech so we don't carry audio
        // across the round transition.
        if (m_VoiceSynthesizer != null) m_VoiceSynthesizer.Stop();
        if (m_HintGenerator != null) m_HintGenerator.CancelPending();

        if (m_VoiceSynthesizer != null)
            m_VoiceSynthesizer.Speak("Nice!", "congrats");
    }

    void HandleRoundReady(int round, string color, string shape)
    {
        // Fired AFTER objects are spawned and finalized.
        // Voice for this round is already started during fixation-cross phase.
        if (m_RoundAnnounceCoroutine != null)
            StopCoroutine(m_RoundAnnounceCoroutine);
        m_RoundAnnounceCoroutine = StartCoroutine(WaitForAnnouncementAndResumeTimer(round, color, shape));
    }

    void HandleRoundTransitionStarted(int round, string color, string shape)
    {
        // Cross is visible now: start the next round instruction immediately.
        if (m_VoiceSynthesizer != null) m_VoiceSynthesizer.Stop();
        if (m_HintGenerator != null) m_HintGenerator.CancelPending();

        if (m_VoiceSynthesizer != null)
            m_VoiceSynthesizer.Speak($"Round {round + 1}. Find the {color} {shape}.", "round");
    }

    System.Collections.IEnumerator WaitForAnnouncementAndResumeTimer(int round, string color, string shape)
    {
        // Wait until the transition-phase round announcement finishes.
        while (m_VoiceSynthesizer != null && m_VoiceSynthesizer.IsBusy)
            yield return null;

        // If the round has already changed or game left Playing state while waiting,
        // this coroutine is stale and must not touch timer/hint state.
        if (m_GameManager == null ||
            m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing ||
            m_GameManager.CurrentObjectiveIndex != round)
        {
            m_RoundAnnounceCoroutine = null;
            yield break;
        }

        // Resume the timer now that the participant knows the objective
        var ui = GetComponent<FindObjectUI>();
        if (ui != null) ui.ResumeTimer();

        if (m_HintGenerator != null)
            m_HintGenerator.OnNewObjective();
        Debug.Log($"{k_Tag} Round {round + 1} ready: {color} {shape}");
        m_RoundAnnounceCoroutine = null;
    }

    void HandleWrongCapture(string capturedName, string wantedName)
    {
        Debug.Log($"{k_Tag} Wrong capture: {capturedName} instead of {wantedName}");
        if (m_HintGenerator != null) m_HintGenerator.OnWrongCapture();
    }

    void HandleGameCompleted(float elapsedSeconds)
    {
        if (m_HintGenerator != null) m_HintGenerator.CancelPending();
        if (m_VoiceSynthesizer != null)
        {
            m_VoiceSynthesizer.Stop();
            int minutes = (int)(elapsedSeconds / 60f);
            float seconds = elapsedSeconds % 60f;
            string timeStr = minutes > 0
                ? $"{minutes} minutes and {seconds:F0} seconds"
                : $"{seconds:F0} seconds";
            m_VoiceSynthesizer.Speak(
                $"Excellent! You found all the objects in {timeStr}. Great job! " +
                "Please submit the NASA T L X questionnaire now.",
                "completion");
        }
    }

    [System.Serializable]
    struct ApiKeys
    {
        public string openai_key;
        public string elevenlabs_key;
    }
}
