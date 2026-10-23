using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.MR;

/// <summary>
/// Orchestrates the game assistant. Loads API keys at startup (before game can start),
/// then wires VoiceSynthesizer and HintGenerator for the duration of the game.
/// </summary>
public partial class VoiceAssistantController : MonoBehaviour
{
    const string k_Tag = "[VoiceAssist]";
    const string k_KeysFile = "api_keys.json";
    const string k_IntroLine =
        "You will use the agent's guidance to find an object whose identity is not shown to you. " +
        "Follow the directions, point your right controller at the object, then press the trigger to select it. " +
        "Stay seated and use the chair to turn.";
    const string k_ClosingLine =
        "Thank you for your participation in this experiment, please remove the headset now and have a great day";
    const string k_RotationalIntroLine = k_IntroLine +
        " Objects will surround you. The agent will guide you to the right area, then help you look up or down. " +
        "Your first two trials will be practice.";
    static string StudyIntroLine => FindObjectOfType<FindObjectGameManager>()?.RotationalBetaEnabled == true
        ? k_RotationalIntroLine : k_IntroLine;
    public void PlayStudyIntroduction() => HandleInitialStartPressed();
    const int k_WrongCueSampleRate = 22050;
    const float k_WrongCueDurationSeconds = 0.22f;

    FindObjectGameManager m_GameManager;
    AgentContext m_AgentContext;
    HintGenerator m_HintGenerator;
    VoiceSynthesizer m_VoiceSynthesizer;
    GazeCoverageTracker m_CoverageTracker;
    AudioSource m_EffectAudioSource;
    AudioClip m_WrongCaptureCue, m_SuccessCue;
    Coroutine m_SuccessRoutine;
    public bool SuccessFeedbackBusy { get; private set; }
    static readonly string[] k_SuccessPhrases = SpatialVoice.RoundEncouragement.AllTexts();
    Coroutine m_OpeningCoroutine;
    System.Action<string,string,string,string> m_OpeningAudioObserver;
    Coroutine m_RoundAnnounceCoroutine;
    Coroutine m_IntroAnnounceCoroutine;
    VoiceModeSelector m_VoiceModeSelector;
    bool m_IntroRequested;
    bool m_IntroPlayed;
    bool m_IntroPlayingOrQueued;
    int m_PendingRound = -1;
    string m_PendingRoundColor;
    string m_PendingRoundShape;

    /// <summary>True once API keys are loaded and all sub-systems are ready.</summary>
    public bool IsReady { get; private set; }
    public bool IsIntroBlockingExperimentStart =>
        (m_VoiceModeSelector != null && !m_VoiceModeSelector.IsComplete) || m_IntroPlayingOrQueued;

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

        m_EffectAudioSource = gameObject.AddComponent<AudioSource>();
        m_EffectAudioSource.spatialBlend = 0f;
        m_EffectAudioSource.playOnAwake = false;
        m_EffectAudioSource.volume = 0.26f;
        m_WrongCaptureCue = CreateWrongCaptureCue();
        m_SuccessCue = CreateSuccessCue();

        m_VoiceSynthesizer = gameObject.AddComponent<VoiceSynthesizer>();
        m_VoiceSynthesizer.Telemetry += TrackRoundSpeech;
        m_HintGenerator = gameObject.AddComponent<HintGenerator>();

        // Subscribe to game events
        m_GameManager.OnGameStarted += HandleGameStarted;
        m_GameManager.OnObjectFound += HandleObjectFound;
        m_GameManager.OnWrongCapture += HandleWrongCapture;
        m_GameManager.OnGameCompleted += HandleGameCompleted;
        m_GameManager.OnRoundTransitionStarted += HandleRoundTransitionStarted;
        m_GameManager.OnRoundReady += HandleRoundReady;
        m_GameManager.OnSearchStarted += BeginRoundPerformance;
        m_GameManager.OnSessionStopped += StopRoundEncouragement;
        GoalManager.InitialStartPressed += HandleInitialStartPressed;
        GoalManager.TutorialPlayerVisibilityChanged += HandleTutorialPlayerVisibilityChanged;

        // Load API keys — blocks game start until ready
        StartCoroutine(LoadKeys());
    }

    IEnumerator LoadKeys()
    {
        string elevenLabsKey = "", gatewayKey = "";
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
                gatewayKey = keys.jev_key;
                Debug.Log($"{k_Tag} Loaded API keys");
            }
            else
            {
                Debug.LogWarning($"{k_Tag} Could not load API keys: {request.error}");
            }
        }

        // Initialize sub-systems with the loaded keys
        m_VoiceSynthesizer.Initialize(elevenLabsKey);
        m_VoiceSynthesizer.PlaybackFailed += HandleAudioFailure;
        m_HintGenerator.Initialize(elevenLabsKey, m_AgentContext, m_VoiceSynthesizer, m_CoverageTracker);
        var navigation = gameObject.AddComponent<SpatialVoice.StudyNavigationVoiceBridge>();
        navigation.Initialize(gatewayKey, elevenLabsKey, m_VoiceSynthesizer);
        m_EncouragementClient = new SpatialVoice.JevDecisionClient(gatewayKey);
        m_EncouragementClient.Trace += m_VoiceSynthesizer.RecordJevTrace;


        // Self-similar voice: enrollment (mic -> clone) + the on-entry mode picker.
        var enrollment = gameObject.AddComponent<VoiceEnrollment>();
        enrollment.Initialize(m_VoiceSynthesizer.CloneClient);
        m_VoiceModeSelector = gameObject.AddComponent<VoiceModeSelector>();
        m_VoiceModeSelector.SelectionCompleted += HandleVoiceSelected;
        m_VoiceModeSelector.Initialize(enrollment, m_VoiceSynthesizer);

        IsReady = true;
        Debug.Log($"{k_Tag} Ready. Key={(!string.IsNullOrEmpty(elevenLabsKey) ? "present" : "MISSING")}");

        if (m_IntroRequested && !m_IntroPlayed)
            PlayIntroIfPossible();
    }

    void HandleVoiceSelected()
    {
        if (m_IntroRequested) PlayIntroIfPossible();
    }

    const string k_CompletionLine = "The search task is complete. Please wait for the researcher to guide you through the next steps.";
    public const string AudioCheckLine = VoicePromptText.AudioLevelCheck;
    public static string RoundPhrase(int round, string color, string shape, bool isPractice = false) =>
        (isPractice ? "This is a practice round. It does not count toward the study. " : "") +
        DirectionalHintPolicy.ReadinessPrompt(round);
    public static string[] PhraseLibrary()
    {
        var phrases = new System.Collections.Generic.HashSet<string>(HintGenerator.AllPhrases());
        phrases.UnionWith(SpatialVoice.StudyNavigationVoiceBridge.Phrases());
        phrases.Add(k_CompletionLine); phrases.Add(StudyIntroLine); phrases.Add(k_ClosingLine); phrases.Add(AudioCheckLine); phrases.UnionWith(k_SuccessPhrases);
        foreach (var round in ChallengeSet.Rounds)
        {
            phrases.Add(RoundPhrase(round.roundIndex, round.target.color, round.target.shape));
            phrases.Add(VoicePromptText.WrongSelectionPhrase(round.target.color, round.target.shape));
        }
        for (int i = 0; i < ChallengeSet.PracticeTrialCount; i++)
        {
            var practice = ChallengeSet.PracticeRound(i);
            phrases.Add(RoundPhrase(practice.roundIndex, practice.target.color, practice.target.shape, true));
            phrases.Add(VoicePromptText.WrongSelectionPhrase(practice.target.color, practice.target.shape));
        }
        return new System.Collections.Generic.List<string>(phrases).ToArray();
    }

    public static string[] StarterPhrases()
    {
        var practice = ChallengeSet.PracticeRound(0);
        var phrases = new System.Collections.Generic.List<string>(HintGenerator.AllPhrases());
        phrases.AddRange(k_SuccessPhrases);
        phrases.AddRange(new[] { AudioCheckLine, StudyIntroLine,
            RoundPhrase(practice.roundIndex, practice.target.color, practice.target.shape, true),
            VoicePromptText.WrongSelectionPhrase(practice.target.color, practice.target.shape) });
        return phrases.ToArray();
    }

    public static string[] BackgroundPhrases()
    {
        var ordered = new System.Collections.Generic.List<string>();
        for (int i = 0; i < ChallengeSet.PracticeTrialCount; i++)
        {
            var practice = ChallengeSet.PracticeRound(i);
            ordered.Add(RoundPhrase(practice.roundIndex, practice.target.color, practice.target.shape, true));
            ordered.Add(VoicePromptText.WrongSelectionPhrase(practice.target.color, practice.target.shape));
        }
        foreach (var round in ChallengeSet.Rounds)
        {
            ordered.Add(RoundPhrase(round.roundIndex, round.target.color, round.target.shape));
            ordered.Add(VoicePromptText.WrongSelectionPhrase(round.target.color, round.target.shape));
        }
        ordered.AddRange(PhraseLibrary());
        var seen = new System.Collections.Generic.HashSet<string>();
        return ordered.FindAll(phrase => seen.Add(phrase)).ToArray();
    }

    void HandleAudioFailure(string reason)
    {
        if (m_GameManager != null && m_GameManager.CurrentState != FindObjectGameManager.GameState.Idle &&
            m_GameManager.CurrentState != FindObjectGameManager.GameState.Completed)
        {
            m_HintGenerator?.CancelPending();
            m_GameManager.TechnicalStop("audio_failure: " + reason);
        }
    }

    void OnDisable()
    {
        CancelSuccessFeedback();
        if (m_VoiceSynthesizer != null) m_VoiceSynthesizer.Telemetry -= TrackRoundSpeech;
        CancelSearchOpening();
        if (m_VoiceSynthesizer != null) m_VoiceSynthesizer.PlaybackFailed -= HandleAudioFailure;
        if (m_VoiceModeSelector != null)
            m_VoiceModeSelector.SelectionCompleted -= HandleVoiceSelected;
        if (m_IntroAnnounceCoroutine != null)
        {
            StopCoroutine(m_IntroAnnounceCoroutine);
            m_IntroAnnounceCoroutine = null;
        }
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
            m_GameManager.OnSearchStarted -= BeginRoundPerformance;
            m_GameManager.OnSessionStopped -= StopRoundEncouragement;
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
        if (SessionConfig.DevelopmentQuickTest)
        {
            m_IntroPlayed = true;
            m_IntroRequested = false;
            m_IntroPlayingOrQueued = false;
            return;
        }
        if (m_IntroPlayed)
            return;

        m_IntroPlayingOrQueued = true;

        if (!IsReady || m_VoiceSynthesizer == null ||
            (m_VoiceModeSelector != null && !m_VoiceModeSelector.IsComplete))
            return;

        m_VoiceSynthesizer.Stop();
        // Instructions use the selected neutral voice, regardless of the first block's condition.
        m_VoiceSynthesizer.Speak(StudyIntroLine, "intro", VoiceCondition.Generic);
        m_IntroPlayed = true;
        m_IntroRequested = false;
        if (m_IntroAnnounceCoroutine != null)
            StopCoroutine(m_IntroAnnounceCoroutine);
        m_IntroAnnounceCoroutine = StartCoroutine(FinishIntroThenAnnouncePendingRound());
    }

    void HandleGameStarted()
    {
        CancelSuccessFeedback(); m_RoundPerformance = null; m_LastEncouragement = null; m_EncouragementSessionStopped = false;
        m_CoverageTracker?.Reset();
        // First-round goal announcement is now triggered by
        // FindObjectGameManager via OnRoundTransitionStarted, after the
        // fixation cross appears.
        if (m_HintGenerator != null)
            m_HintGenerator.CancelPending();
    }

    void HandleObjectFound(int roundIndex) => PlaySuccessFeedback();

    public void PlaySuccessFeedback()
    {
        if (SuccessFeedbackBusy || m_EncouragementSessionStopped || !isActiveAndEnabled) return;
        m_HintGenerator?.CancelPending(); m_VoiceSynthesizer?.Stop();
        m_SuccessRoutine = StartCoroutine(SuccessFeedback());
    }
    IEnumerator SuccessFeedback()
    {
        SuccessFeedbackBusy = true;
        var facts = CaptureRoundPerformance();
        var candidates = SpatialVoice.RoundEncouragement.Candidates(facts, m_LastEncouragement,
            text => m_VoiceSynthesizer != null && m_VoiceSynthesizer.IsPreparedPhrase(text));
        var voice = SessionConfig.Voice;
        string voiceId = voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
        int round = m_GameManager != null ? m_GameManager.PresentedRoundIndex : -1;
        bool practice = m_GameManager != null && m_GameManager.PresentedRoundIsPractice;
        m_SuccessCancellation = new System.Threading.CancellationTokenSource();
        double deadline = Time.realtimeSinceStartupAsDouble + SpatialVoice.RoundEncouragement.DeadlineMilliseconds / 1000d;
        var decision = RequestRoundEncouragement(facts, candidates, m_SuccessCancellation.Token);
        if (m_SuccessCue != null && m_EffectAudioSource != null)
        {
            m_EffectAudioSource.PlayOneShot(m_SuccessCue);
            GetComponent<TrialReplayRecorder>()?.RecordEffect(m_SuccessCue, "success_chime", m_EffectAudioSource.volume);
            GetComponent<TrialDataLogger>()?.RecordSuccessEffect(true, m_SuccessCue.length);
            yield return new WaitForSecondsRealtime(m_SuccessCue.length);
            GetComponent<TrialDataLogger>()?.RecordSuccessEffect(false, m_SuccessCue.length);
        }
        // The manager waits before changing voice/block. Reinforcement belongs to the completed trial.
        while (!decision.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        string status = "local_fallback";
        SpatialVoice.EncouragementPhrase selected = candidates.Count > 0 ? candidates[0] : null;
        if (decision.IsCompleted && Time.realtimeSinceStartupAsDouble <= deadline)
        {
            try { var choice = decision.GetAwaiter().GetResult(); if (choice != null) { selected = choice; status = "jev_selected"; } }
            catch (System.Exception e) { status = "fallback_" + e.GetType().Name; }
        }
        m_SuccessCancellation.Cancel();
        if (isActiveAndEnabled && m_GameManager != null && m_GameManager.PresentedRoundIndex == round &&
            m_GameManager.PresentedRoundIsPractice == practice && SessionConfig.Voice == voice &&
            voiceId == (voice == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId))
        {
            RecordEncouragement(facts, selected, status);
            if (selected != null && m_VoiceSynthesizer.IsPreparedPhrase(selected.text))
            { m_LastEncouragement = selected.text; m_VoiceSynthesizer.Speak(selected.text, "congrats", voice, guidanceDecisionId: m_EncouragementDecisionId); }
        }
        while (m_VoiceSynthesizer != null && m_VoiceSynthesizer.IsBusy) yield return null;
        m_SuccessCancellation?.Dispose(); m_SuccessCancellation = null;
        SuccessFeedbackBusy = false; m_SuccessRoutine = null;
    }
    static AudioClip CreateSuccessCue()
    {
        const int rate = 22050; var samples = new float[(int)(rate*.38f)];
        for (int i=0;i<samples.Length;i++)
        {
            float t=i/(float)rate;
            samples[i]=.25f*Mathf.Sin(2*Mathf.PI*(t<.12f?660:880)*t)*Mathf.Min(1,t/.01f)*Mathf.Exp(-8*t);
        }
        var clip=AudioClip.Create("success_chime_v1",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
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
        CancelSuccessFeedback();
        // If the intro is still queued or speaking, let it finish and then
        // announce the round so the startup instructions remain audible.
        if (m_IntroPlayingOrQueued)
        {
            m_HintGenerator?.SetLifecycle(GuidancePhase.Opening, "round_readiness");
            m_PendingRound = round;
            m_PendingRoundColor = color;
            m_PendingRoundShape = shape;
            if (m_IntroAnnounceCoroutine != null)
                StopCoroutine(m_IntroAnnounceCoroutine);
            m_IntroAnnounceCoroutine = StartCoroutine(FinishIntroThenAnnouncePendingRound());
            return;
        }

        SessionConfig.ApplyRoundVoice(round);

        // Cross is visible now: start the next round instruction immediately.
        if (m_VoiceSynthesizer != null) m_VoiceSynthesizer.Stop();
        if (m_HintGenerator != null) { m_HintGenerator.CancelPending(); m_HintGenerator.SetLifecycle(GuidancePhase.Opening, "round_readiness"); }

        if (m_VoiceSynthesizer != null)
            m_VoiceSynthesizer.Speak(RoundPhrase(round, color, shape, m_GameManager != null && m_GameManager.IsPractice), "round");
    }

    IEnumerator FinishIntroThenAnnouncePendingRound()
    {
        while ((m_VoiceSynthesizer != null && m_VoiceSynthesizer.IsBusy) ||
            (m_GameManager != null && m_GameManager.ResearcherPaused))
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

            if (m_HintGenerator != null) { m_HintGenerator.CancelPending(); m_HintGenerator.SetLifecycle(GuidancePhase.Opening, "round_readiness"); }
            m_VoiceSynthesizer.Speak(RoundPhrase(round, color, shape, m_GameManager != null && m_GameManager.IsPractice), "round");
        }

        m_IntroAnnounceCoroutine = null;
    }

    public void AnnounceSearchOpening(int round)
    {
        if (m_OpeningCoroutine != null) return;
        m_OpeningCoroutine = StartCoroutine(SearchOpening(round));
    }
    public void CancelSearchOpening()
    {
        if (m_OpeningCoroutine != null) StopCoroutine(m_OpeningCoroutine);
        m_OpeningCoroutine = null;
        if (m_OpeningAudioObserver != null && m_VoiceSynthesizer != null)
            m_VoiceSynthesizer.Telemetry -= m_OpeningAudioObserver;
        m_OpeningAudioObserver = null;
        m_VoiceSynthesizer?.InterruptIfAbout("guidance_opening");
    }
    bool OpeningStillCurrent(int round) => m_GameManager != null &&
        m_GameManager.CurrentState == FindObjectGameManager.GameState.Playing &&
        m_GameManager.CurrentObjectiveIndex == round && !m_GameManager.SearchActive &&
        !m_GameManager.ResearcherPaused && m_GameManager.Readiness != null && m_GameManager.Readiness.Completed;

    IEnumerator SearchOpening(int round)
    {
        yield return null; // Store the handle before cancellation/failure callbacks can run.
        if (m_VoiceSynthesizer == null || m_HintGenerator == null)
        { m_OpeningCoroutine = null; m_GameManager.TechnicalStop("audio_failure: opening unavailable"); yield break; }
        string phrase = null, decisionId = null;
        while (OpeningStillCurrent(round) && !m_HintGenerator.TryPrepareOpening(out phrase, out decisionId))
            yield return null; // Invalid gaze never supplies a guessed opening direction.
        if (!OpeningStillCurrent(round)) { m_OpeningCoroutine = null; yield break; }
        bool finished = false;
        m_OpeningAudioObserver = (kind, context, clip, detail) => {
            if (kind == "audio_playback_end" && context == "guidance_opening" &&
                detail != null && detail.Contains("decision_id=" + decisionId + ";")) finished = true;
        };
        m_VoiceSynthesizer.Telemetry += m_OpeningAudioObserver;
        m_GameManager.RecordReadinessEvent("opening_direction_started");
        m_VoiceSynthesizer.Speak(phrase, "guidance_opening", guidanceDecisionId: decisionId);
        while (OpeningStillCurrent(round) && m_VoiceSynthesizer.IsBusy) yield return null;
        m_VoiceSynthesizer.Telemetry -= m_OpeningAudioObserver; m_OpeningAudioObserver = null;
        m_OpeningCoroutine = null;
        if (!OpeningStillCurrent(round)) yield break;
        // Idle alone is not success: Stop(), failed playback or a replacement clip
        // must not accidentally reveal objects and begin the measured search.
        if (!finished || !string.IsNullOrEmpty(m_VoiceSynthesizer.LastError))
        { m_GameManager.TechnicalStop("audio_failure: opening interrupted"); yield break; }
        m_GameManager.CompleteOpeningDirection(round);
    }

    public void RepeatReadinessAnnouncement()
    {
        if (m_GameManager == null || m_GameManager.Readiness == null || !m_GameManager.Readiness.Active) return;
        if (m_RoundAnnounceCoroutine != null) StopCoroutine(m_RoundAnnounceCoroutine);
        var target = m_GameManager.CurrentTarget;
        int round = m_GameManager.CurrentObjectiveIndex;
        HandleRoundTransitionStarted(round, target.color, target.shape);
        m_RoundAnnounceCoroutine = StartCoroutine(WaitForAnnouncementAndResumeTimer(round, target.color, target.shape));
    }

    System.Collections.IEnumerator WaitForAnnouncementAndResumeTimer(int round, string color, string shape)
    {
        // Allow all objects-ready subscribers to record their boundary first.
        yield return null;
        // Wait until the transition-phase round announcement finishes.
        while (m_IntroPlayingOrQueued || m_PendingRound >= 0 ||
            (m_VoiceSynthesizer != null && m_VoiceSynthesizer.IsBusy))
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

        // The manager owns the single authoritative search-onset gate.
        if (!string.IsNullOrEmpty(m_VoiceSynthesizer.LastError)) { m_RoundAnnounceCoroutine = null; yield break; }
        m_GameManager.MarkAnnouncementReady(round);
        Debug.Log($"{k_Tag} Round {round + 1} announcement ready; awaiting gaze gate: {color} {shape}");
        m_RoundAnnounceCoroutine = null;
    }

    void HandleWrongCapture(string capturedName, string wantedName)
    {
        if (m_RoundPerformance != null && m_GameManager.SearchActive) m_RoundPerformance.incorrectSelections++;
        Debug.Log($"{k_Tag} Wrong capture: {capturedName} instead of {wantedName}");
        if (m_EffectAudioSource != null && m_WrongCaptureCue != null)
            m_EffectAudioSource.PlayOneShot(m_WrongCaptureCue);
        if (m_HintGenerator != null) m_HintGenerator.OnWrongCapture();
        if (m_VoiceSynthesizer != null && m_GameManager != null)
        {
            var target = m_GameManager.CurrentTarget;
            m_VoiceSynthesizer.Speak(VoicePromptText.WrongSelectionPhrase(target.color, target.shape), "wrong_selection");
        }
    }

    void HandleGameCompleted(float elapsedSeconds) => StartCoroutine(CompleteAfterSuccess());
    IEnumerator CompleteAfterSuccess()
    {
        while (SuccessFeedbackBusy) yield return null;
        if (m_EncouragementSessionStopped || !isActiveAndEnabled) yield break;
        m_HintGenerator?.CancelPending();
        m_VoiceSynthesizer?.Speak(k_CompletionLine, "completion");
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
        public string elevenlabs_key;
        public string jev_key; // Optional local configuration; never logged.
    }

    static AudioClip CreateWrongCaptureCue()
    {
        int sampleCount = Mathf.CeilToInt(k_WrongCueSampleRate * k_WrongCueDurationSeconds);
        float[] samples = new float[sampleCount];
        float phaseA = 0f;
        float phaseB = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)k_WrongCueSampleRate;
            float normalized = sampleCount > 1 ? i / (float)(sampleCount - 1) : 1f;
            float envelope = Mathf.Pow(1f - normalized, 1.8f);
            float freqA = Mathf.Lerp(210f, 165f, normalized);
            float freqB = Mathf.Lerp(320f, 245f, normalized);

            phaseA += 2f * Mathf.PI * freqA / k_WrongCueSampleRate;
            phaseB += 2f * Mathf.PI * freqB / k_WrongCueSampleRate;

            float toneA = Mathf.Sin(phaseA);
            float toneB = Mathf.Sin(phaseB);
            float buzz = Mathf.Sign(Mathf.Sin(phaseA * 0.5f)) * 0.12f;
            samples[i] = (toneA * 0.42f + toneB * 0.20f + buzz) * envelope;
        }

        var clip = AudioClip.Create("WrongCaptureCue", sampleCount, 1, k_WrongCueSampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
