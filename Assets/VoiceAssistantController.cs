using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.MR;

/// <summary>
/// Orchestrates the game assistant. Loads API keys at startup (before game can start),
/// then wires VoiceSynthesizer and HintGenerator for the duration of the game.
/// </summary>
public class VoiceAssistantController : MonoBehaviour
{
    const string k_Tag = "[VoiceAssist]";
    const string k_KeysFile = "api_keys.json";
    const string k_IntroLine =
        "Hi, my name is Ava and I will guide you through this task. " +
        "Your goal in this experiment is to find the target object by its color and shape as quickly and accurately as you can. " +
        "By staring at an object for an extended period of time, you can select it. " +
        "To begin, tap a nearby surface with your controller. " +
        "Your goal will be displayed in the center of your view each round.";
    const string k_ClosingLine =
        "Thank you for your participation in this experiment, please remove the headset now and have a great day";

    FindObjectGameManager m_GameManager;
    AgentContext m_AgentContext;
    HintGenerator m_HintGenerator;
    VoiceSynthesizer m_VoiceSynthesizer;
    GazeCoverageTracker m_CoverageTracker;
    Coroutine m_RoundAnnounceCoroutine;
    bool m_IntroRequested;
    bool m_IntroPlayed;
    bool m_IntroPlayingOrQueued;
    int m_PendingRound = -1;
    string m_PendingRoundColor;
    string m_PendingRoundShape;

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
        GoalManager.InitialStartPressed += HandleInitialStartPressed;
        GoalManager.TutorialPlayerVisibilityChanged += HandleTutorialPlayerVisibilityChanged;

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

        if (m_IntroRequested && !m_IntroPlayed)
            PlayIntroIfPossible();
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

        GoalManager.InitialStartPressed -= HandleInitialStartPressed;
        GoalManager.TutorialPlayerVisibilityChanged -= HandleTutorialPlayerVisibilityChanged;
    }

    void HandleInitialStartPressed()
    {
        if (m_IntroPlayed)
            return;

        m_IntroRequested = true;
        PlayIntroIfPossible();
    }

    void HandleTutorialPlayerVisibilityChanged(bool visible)
    {
        if (!visible || m_IntroPlayed)
            return;

        m_IntroRequested = true;
        PlayIntroIfPossible();
    }

    void PlayIntroIfPossible()
    {
        if (m_IntroPlayed)
            return;

        m_IntroPlayingOrQueued = true;

        if (!IsReady || m_VoiceSynthesizer == null)
            return;

        m_VoiceSynthesizer.Stop();
        m_VoiceSynthesizer.Speak(k_IntroLine, "intro");
        m_IntroPlayed = true;
        m_IntroRequested = false;
        if (m_RoundAnnounceCoroutine != null)
            StopCoroutine(m_RoundAnnounceCoroutine);
        m_RoundAnnounceCoroutine = StartCoroutine(FinishIntroThenAnnouncePendingRound());
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
        // If the intro is still queued or speaking, let it finish and then
        // announce the round so the startup instructions remain audible.
        if (m_IntroPlayingOrQueued)
        {
            m_PendingRound = round;
            m_PendingRoundColor = color;
            m_PendingRoundShape = shape;
            if (m_RoundAnnounceCoroutine != null)
                StopCoroutine(m_RoundAnnounceCoroutine);
            m_RoundAnnounceCoroutine = StartCoroutine(FinishIntroThenAnnouncePendingRound());
            return;
        }

        // Cross is visible now: start the next round instruction immediately.
        if (m_VoiceSynthesizer != null) m_VoiceSynthesizer.Stop();
        if (m_HintGenerator != null) m_HintGenerator.CancelPending();

        if (m_VoiceSynthesizer != null)
            m_VoiceSynthesizer.Speak($"Round {round + 1}. Find the {color} {shape}.", "round");
    }

    IEnumerator FinishIntroThenAnnouncePendingRound()
    {
        while (m_VoiceSynthesizer != null && m_VoiceSynthesizer.IsBusy)
            yield return null;

        m_IntroPlayingOrQueued = false;

        if (m_PendingRound >= 0 && m_VoiceSynthesizer != null)
        {
            int round = m_PendingRound;
            string color = m_PendingRoundColor;
            string shape = m_PendingRoundShape;
            m_PendingRound = -1;
            m_PendingRoundColor = null;
            m_PendingRoundShape = null;

            if (m_HintGenerator != null) m_HintGenerator.CancelPending();
            m_VoiceSynthesizer.Speak($"Round {round + 1}. Find the {color} {shape}.", "round");
        }

        m_RoundAnnounceCoroutine = null;
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
                "Please complete the NASA T L X questionnaire now by using the analog stick, then confirm with the trigger button.",
                "completion");
        }
    }

    public void SpeakClosingThankYou()
    {
        if (m_VoiceSynthesizer == null)
            return;

        m_VoiceSynthesizer.Stop();
        m_VoiceSynthesizer.Speak(k_ClosingLine, "closing");
    }

    [System.Serializable]
    struct ApiKeys
    {
        public string openai_key;
        public string elevenlabs_key;
    }
}
