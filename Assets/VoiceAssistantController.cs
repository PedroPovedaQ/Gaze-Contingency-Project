using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Orchestrates the LLM voice assistant for the Find the Object game.
/// Auto-attaches to ObjectSpawner. Wires AgentContext, HintGenerator,
/// and VoiceSynthesizer together. Subscribes to game events for
/// welcome/congratulation messages (direct TTS) and hint triggers (LLM).
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

        // Load API keys from StreamingAssets
        string openAiKey = "";
        string elevenLabsKey = "";
        string keysPath = System.IO.Path.Combine(Application.streamingAssetsPath, k_KeysFile);
        try
        {
            string json = System.IO.File.ReadAllText(keysPath);
            var keys = JsonUtility.FromJson<ApiKeys>(json);
            openAiKey = keys.openai_key;
            elevenLabsKey = keys.elevenlabs_key;
            Debug.Log($"{k_Tag} Loaded API keys from {keysPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"{k_Tag} Could not load API keys from {keysPath}: {e.Message}. Voice assistant disabled.");
        }

        // Create sub-components
        m_VoiceSynthesizer = gameObject.AddComponent<VoiceSynthesizer>();
        m_VoiceSynthesizer.Initialize(elevenLabsKey);

        m_CoverageTracker = gameObject.AddComponent<GazeCoverageTracker>();
        m_CoverageTracker.Initialize(m_GameManager);

        m_AgentContext = gameObject.AddComponent<AgentContext>();
        var ui = GetComponent<FindObjectUI>();
        m_AgentContext.Initialize(m_GameManager, ui, m_CoverageTracker);

        m_HintGenerator = gameObject.AddComponent<HintGenerator>();
        m_HintGenerator.Initialize(openAiKey, m_AgentContext, m_VoiceSynthesizer, m_CoverageTracker);

        // Subscribe to game events
        m_GameManager.OnGameStarted += HandleGameStarted;
        m_GameManager.OnObjectFound += HandleObjectFound;
        m_GameManager.OnWrongGrab += HandleWrongGrab;
        m_GameManager.OnGameCompleted += HandleGameCompleted;

        Debug.Log($"{k_Tag} Initialized and subscribed to game events");
    }

    void OnDisable()
    {
        if (m_GameManager != null)
        {
            m_GameManager.OnGameStarted -= HandleGameStarted;
            m_GameManager.OnObjectFound -= HandleObjectFound;
            m_GameManager.OnWrongGrab -= HandleWrongGrab;
            m_GameManager.OnGameCompleted -= HandleGameCompleted;
        }
    }

    void HandleGameStarted()
    {
        m_CoverageTracker?.Reset();

        var objectives = m_GameManager.Objectives;
        if (objectives.Count == 0) return;

        var first = objectives[0];
        string welcome = $"Let's play! Find the {first.color} {first.shape}. Look around the table and shelves!";

        Debug.Log($"{k_Tag} Game started, saying welcome");
        m_VoiceSynthesizer.Speak(welcome, "welcome");
        m_HintGenerator.OnNewObjective();
    }

    void HandleObjectFound(int objectiveIndex)
    {
        // Interrupt any speech about the object we just found
        m_VoiceSynthesizer.InterruptIfAbout("hint");
        m_HintGenerator.CancelPending();

        var objectives = m_GameManager.Objectives;
        int nextIndex = objectiveIndex + 1;

        if (nextIndex < objectives.Count)
        {
            var next = objectives[nextIndex];
            string congrats = $"Great job! Now find the {next.color} {next.shape}.";
            m_VoiceSynthesizer.Speak(congrats, "congrats");
            m_HintGenerator.OnNewObjective();
        }
        // If it's the last object, HandleGameCompleted will fire
    }

    void HandleWrongGrab(string grabbedName, string wantedName)
    {
        Debug.Log($"{k_Tag} Wrong grab: {grabbedName} instead of {wantedName}");
        m_HintGenerator.OnWrongGrab();
    }

    void HandleGameCompleted(float elapsedSeconds)
    {
        m_HintGenerator.CancelPending();
        m_VoiceSynthesizer.Stop();

        int minutes = (int)(elapsedSeconds / 60f);
        float seconds = elapsedSeconds % 60f;
        string timeStr = minutes > 0
            ? $"{minutes} minutes and {seconds:F0} seconds"
            : $"{seconds:F0} seconds";

        string completion = $"Amazing! You found all the objects in {timeStr}! Great work!";
        m_VoiceSynthesizer.Speak(completion, "completion");

        Debug.Log($"{k_Tag} Game completed, said congratulations");
    }

    [System.Serializable]
    struct ApiKeys
    {
        public string openai_key;
        public string elevenlabs_key;
    }
}
