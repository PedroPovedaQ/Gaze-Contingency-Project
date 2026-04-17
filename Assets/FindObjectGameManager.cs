using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// 14-round conjunction search game. Each round spawns 56 objects on bookcases.
/// Player finds 1 target via gaze dwell. On correct capture: show fixation cross,
/// destroy objects, wait, spawn fresh, finalize gaze interactables, go.
/// </summary>
public class FindObjectGameManager : MonoBehaviour
{
    const string k_Tag = "[FindObjectGame]";
    static int k_ObjectsPerRound => ChallengeSet.ObjectsPerRound;
    static int k_TotalRounds => ChallengeSet.RoundCount;
    const float k_TransitionPause = 4f;
    const float k_BlankPauseMin = 0.2f;
    const float k_BlankPauseMax = 1.3f;
    const float k_ResetDelay = 5f;
    const float k_ExitAfterThankYouDelay = 9f;

    public enum GameState { Idle, Playing, Transitioning, Completed }

    public event System.Action OnGameStarted;
    public event System.Action<int> OnObjectFound;
    public event System.Action<string, string> OnWrongCapture;
    public event System.Action<float> OnGameCompleted;
    /// <summary>Fires when a new round's target is set and objects are ready.</summary>
    public event System.Action<int, string, string> OnRoundReady; // round, color, shape
    /// <summary>Fires when fixation cross appears and next goal can be announced.</summary>
    public event System.Action<int, string, string> OnRoundTransitionStarted; // next round, color, shape

    public GameState CurrentState => m_State;
    public IReadOnlyList<(string shape, string color, Color colorValue)> Objectives => m_Objectives;
    public IReadOnlyList<GameObject> SpawnedObjects => m_SpawnedObjects;
    public int CurrentObjectiveIndex => m_CurrentRound;
    public int FoundCount => m_CurrentRound;
    public int TotalRounds => k_TotalRounds;
    public float GameStartTime => m_GameStartTime;
    public (string shape, string color, Color colorValue) CurrentTarget => m_CurrentTarget;
    public bool CurrentRoundGazeAware { get; private set; }
    public string CurrentRoundConditionLabel { get; private set; } = "";

    [Header("Debug")]
    [SerializeField] bool m_UseDebugRoundCountOverride;
    [SerializeField, Min(1)] int m_DebugRoundCount = ChallengeSet.TotalRounds;

    float m_GameStartTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var spawner = FindObjectOfType<ObjectSpawner>();
        if (spawner != null && spawner.GetComponent<FindObjectGameManager>() == null)
        {
            spawner.gameObject.AddComponent<FindObjectGameManager>();
            Debug.Log($"{k_Tag} Auto-attached");
        }
        DisableTemplateSystems();
    }

    static void DisableTemplateSystems()
    {
        foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>())
            if (mb.GetType().Name == "GoalManager")
            {
                foreach (var c in mb.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
                mb.enabled = false;
            }
        foreach (var vp in Object.FindObjectsOfType<VideoPlayer>())
        {
            vp.Stop(); vp.enabled = false;
            foreach (var c in vp.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
        }

        // Hide tutorial/coaching windows from the MR template startup flow.
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            string n = go.name;
            bool isTemplateTutorialUi =
                n == "Text Poke Button OK" ||
                n == "Text Poke Button" ||
                n == "Text Poke Button Continue" ||
                n.IndexOf("Tutorial Player", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("TutorialPlayer", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isTemplateTutorialUi)
            {
                go.SetActive(false);
                Debug.Log($"{k_Tag} Hid template UI: {go.name}");
            }
        }

        // Some template tutorial panels can be re-enabled by internal scripts and
        // are easiest to identify by their visible text labels.
        foreach (var tmp in Object.FindObjectsOfType<TMP_Text>(true))
        {
            string t = tmp.text != null ? tmp.text.Trim() : "";
            if (string.IsNullOrEmpty(t)) continue;

            bool isTemplateText =
                t == "Default Input Controls" ||
                t == "Continue";
            if (!isTemplateText) continue;

            if (IsUnderTutorialUi(tmp.transform))
            {
                tmp.gameObject.SetActive(false);
                if (tmp.transform.parent != null)
                    tmp.transform.parent.gameObject.SetActive(false);
                Debug.Log($"{k_Tag} Hid template text UI: '{t}' ({tmp.gameObject.name})");
            }
        }
    }

    static bool IsUnderTutorialUi(Transform t)
    {
        while (t != null)
        {
            string n = t.name;
            if (n.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Coaching", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Goal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }

    GameState m_State = GameState.Idle;
    ObjectSpawner m_Spawner;
    ShapeObjectFactory m_Factory;
    FindObjectUI m_UI;
    GazeHighlightManager m_GazeDwell;
    TrialDataLogger m_TrialLogger;
    Coroutine m_ResetCoroutine;
    Coroutine m_AppExitCoroutine;
    float m_LastCompletedElapsed;
    bool m_NasaTlxSubmittedForRun;

    readonly List<(string shape, string color, Color colorValue)> m_Objectives = new();
    readonly List<GameObject> m_SpawnedObjects = new();
    readonly List<ShelfSpawner.SpawnPoint> m_SpawnPoints = new();
    readonly List<GameObject> m_ShelfObjects = new();
    int m_CurrentRound;
    (string shape, string color, Color colorValue) m_CurrentTarget;
    bool m_ShelvesBuilt;

    Vector3 m_SpawnCenter;
    Vector2 m_PlaneSize;
    Vector3 m_PlaneRight;
    Vector3 m_PlaneForward;

    void OnEnable()
    {
        ApplyDebugOverrides();
        Physics.IgnoreLayerCollision(8, 8, true);
        m_Spawner = GetComponent<ObjectSpawner>();
        if (m_Spawner == null) return;
        if (m_UI == null) { m_UI = gameObject.AddComponent<FindObjectUI>(); m_UI.Initialize(); }
        if (m_UI != null)
        {
            m_UI.OnNasaTlxSubmitted -= HandleNasaTlxSubmitted;
            m_UI.OnNasaTlxSubmitted += HandleNasaTlxSubmitted;
            m_UI.OnResetRequested -= HandleResetRequested;
            m_UI.OnResetRequested += HandleResetRequested;
        }
        m_Spawner.objectSpawned += OnObjectSpawned;
    }

    void OnDisable()
    {
        ChallengeSet.DebugRoundCountOverride = 0;
        if (m_Spawner != null) m_Spawner.objectSpawned -= OnObjectSpawned;
        if (m_GazeDwell != null) m_GazeDwell.OnObjectCaptured -= OnObjectCaptured;
        if (m_UI != null)
        {
            m_UI.OnNasaTlxSubmitted -= HandleNasaTlxSubmitted;
            m_UI.OnResetRequested -= HandleResetRequested;
        }
    }

    void OnObjectSpawned(GameObject obj)
    {
        if (m_State == GameState.Idle)
        {
            var voice = GetComponent<VoiceAssistantController>();
            if (voice != null && !voice.IsReady) { StartCoroutine(WaitThenStart(obj)); return; }
            StartGame(obj);
        }
        else Destroy(obj);
    }

    IEnumerator WaitThenStart(GameObject obj)
    {
        var voice = GetComponent<VoiceAssistantController>();
        while (voice != null && !voice.IsReady) yield return null;
        if (obj != null) StartGame(obj);
    }

    void ApplyDebugOverrides()
    {
        int overrideCount = m_UseDebugRoundCountOverride
            ? Mathf.Clamp(m_DebugRoundCount, 1, ChallengeSet.TotalRounds)
            : 0;
        ChallengeSet.DebugRoundCountOverride = overrideCount;

        if (overrideCount > 0)
        {
            Debug.Log($"{k_Tag} Debug round override active: {ChallengeSet.RoundCount}/{ChallengeSet.TotalRounds} rounds");
        }
    }

    // =====================================================================

    void StartGame(GameObject triggerObj)
    {
        if (m_Factory == null) m_Factory = GetComponent<ShapeObjectFactory>();
        if (m_Factory == null) return;

        m_SpawnCenter = triggerObj.transform.position;
        m_PlaneSize = new Vector2(0.5f, 0.5f);
        m_PlaneRight = Vector3.right;
        m_PlaneForward = Vector3.forward;

        var planes = FindObjectsOfType<VivePlaneData>();
        float best = float.MaxValue;
        VivePlaneData bestP = null;
        foreach (var p in planes)
        {
            if (!p.IsHorizontalUp) continue;
            float d = Vector3.Distance(m_SpawnCenter, p.Center);
            if (d < best) { best = d; bestP = p; }
        }
        if (bestP != null)
        {
            m_SpawnCenter = bestP.Center;
            m_PlaneSize = bestP.Size;
            m_PlaneRight = bestP.transform.right;
            m_PlaneForward = bestP.transform.up;
        }
        Destroy(triggerObj);

        m_GazeDwell = FindObjectOfType<GazeHighlightManager>();
        if (m_GazeDwell != null)
            m_GazeDwell.OnObjectCaptured += OnObjectCaptured;
        m_NasaTlxSubmittedForRun = false;

        // Start with the same transition ritual as between rounds:
        // fixation cross + announced goal, then spawn.
        m_State = GameState.Transitioning;
        m_GameStartTime = Time.time;
        m_CurrentRound = 0;
        m_UI.StartTimer();
        m_UI.PauseTimer();
        BuildObjectiveList();

        // Initialize round-1 condition before emitting start events so loggers
        // and assistants observe the correct initial mode.
        int participantNumber = GetParticipantNumber();
        CurrentRoundGazeAware = ChallengeSet.IsGazeAware(m_CurrentRound, participantNumber);
        CurrentRoundConditionLabel = ChallengeSet.GetConditionLabel(m_CurrentRound, participantNumber);
        var hints = GetComponent<HintGenerator>();
        if (hints != null) hints.gazeAwareTips = CurrentRoundGazeAware;
        m_UI.SetAgentState(CurrentRoundGazeAware, CurrentRoundConditionLabel);

        if (!m_ShelvesBuilt)
        {
            var (shelfObjs, _) = ShelfSpawner.CreateShelvesAndSpawnPoints(
                m_SpawnCenter, m_PlaneSize, m_PlaneRight, m_PlaneForward, k_ObjectsPerRound);
            m_ShelfObjects.AddRange(shelfObjs);
            m_ShelvesBuilt = true;
            m_UI.PositionStaticLeft(m_SpawnCenter, ShelfSpawner.ObjectFacingRotation);
        }

        OnGameStarted?.Invoke();
        StartCoroutine(BeginFirstRoundTransition());
    }

    // =====================================================================
    //  Spawn round — instantiate, configure, wait, finalize
    // =====================================================================

    IEnumerator DoSpawnRound()
    {
        // --- Deterministic challenge from ChallengeSet ---
        var round = ChallengeSet.Rounds[m_CurrentRound];
        m_CurrentTarget = (round.target.shape, round.target.color, round.target.colorValue);

        // Determine gaze-aware/unaware for this round
        int participantNumber = GetParticipantNumber();
        bool gazeAware = ChallengeSet.IsGazeAware(m_CurrentRound, participantNumber);
        CurrentRoundGazeAware = gazeAware;
        CurrentRoundConditionLabel = ChallengeSet.GetConditionLabel(m_CurrentRound, participantNumber);

        var hints = GetComponent<HintGenerator>();
        if (hints != null) hints.gazeAwareTips = gazeAware;
        m_UI.SetAgentState(gazeAware, CurrentRoundConditionLabel);
        Debug.Log($"{k_Tag} Round {m_CurrentRound + 1}: participant={participantNumber}, scheduleIndex={round.blockIndex}, condition={CurrentRoundConditionLabel}");

        // --- Get spawn points ---
        m_SpawnPoints.Clear();
        m_SpawnPoints.AddRange(ShelfSpawner.ComputeSpawnPoints(
            m_SpawnCenter, m_PlaneSize, m_PlaneRight, m_PlaneForward, ChallengeSet.ObjectsPerRound));

        // --- Instantiate and configure (deterministic order = deterministic shelf positions) ---
        m_SpawnedObjects.Clear();
        var prefabs = m_Spawner.objectPrefabs;
        int count = Mathf.Min(round.objects.Length, m_SpawnPoints.Count);

        for (int i = 0; i < count; i++)
        {
            var def = round.objects[i];
            m_Factory.EnqueueCombo(def.shape, def.color);

            var obj = Instantiate(prefabs[0]);

            // IMMEDIATELY destroy the prefab's XRGrabInteractable before it
            // registers stale colliders with the interaction manager.
            var oldGrab = obj.GetComponent<XRGrabInteractable>();
            if (oldGrab != null) DestroyImmediate(oldGrab);

            obj.transform.position = m_SpawnPoints[i].position;
            obj.transform.rotation = Quaternion.identity;

            // Configure: swap mesh, replace colliders, set material, scale
            m_Factory.ConfigureObject(obj);

            // Position at designated spawn point
            var sp = m_SpawnPoints[i];
            obj.transform.position = sp.position;
            obj.transform.rotation = ShelfSpawner.ObjectFacingRotation;

            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info != null)
            {
                info.shelfLevel = sp.row;
                info.shelfColumn = sp.col;

                // Pyramid mesh pivot is at its base (others are centered), so after
                // spawn-positioning we lower by half height to align bases on planks.
                if (info.shapeName == "Pyramid")
                {
                    var p = obj.transform.position;
                    p.y -= obj.transform.localScale.y * 0.5f;
                    obj.transform.position = p;
                }
            }

            // Set layer 8 on everything
            foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 8;

            // Hide child renderers, keep root visible
            HideChildRenderers(obj);
            var rootRenderer = obj.GetComponent<MeshRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = true;

            m_SpawnedObjects.Add(obj);
        }

        // Wait for DestroyImmediate to fully process
        yield return null;

        // Add FRESH XRGrabInteractable with correct colliders — never touches stale prefab state
        foreach (var obj in m_SpawnedObjects)
        {
            if (obj == null) continue;
            var grab = obj.AddComponent<XRGrabInteractable>();
            grab.allowGazeInteraction = true;
            grab.allowGazeSelect = false;
            grab.allowGazeAssistance = false;
            grab.movementType = XRGrabInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;

            grab.colliders.Clear();
            foreach (var col in obj.GetComponents<Collider>())
                grab.colliders.Add(col);
        }

        // Wait for interaction manager to register all new interactables
        yield return null;
        yield return null;

        // Reset gaze dwell so it starts fresh on these new objects
        if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

        ShowCurrentObjective();
        OnRoundReady?.Invoke(m_CurrentRound, m_CurrentTarget.color, m_CurrentTarget.shape);
        Debug.Log($"{k_Tag} Round {m_CurrentRound + 1}/{k_TotalRounds}: find {m_CurrentTarget.color} {m_CurrentTarget.shape} ({m_SpawnedObjects.Count} spawned)");
    }

    // =====================================================================
    //  Capture
    // =====================================================================

    void OnObjectCaptured(GameObject obj)
    {
        if (m_State != GameState.Playing) return;
        if (obj == null || !obj.activeInHierarchy) return;

        var info = obj.GetComponent<SpawnableObjectInfo>();
        if (info == null) return;

        if (info.shapeName == m_CurrentTarget.shape && info.colorName == m_CurrentTarget.color)
        {
            Debug.Log($"{k_Tag} Round {m_CurrentRound + 1} correct: {info.DisplayName}");
            m_CurrentRound++;
            OnObjectFound?.Invoke(m_CurrentRound - 1);
            if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

            if (m_CurrentRound >= k_TotalRounds)
            {
                m_State = GameState.Completed;
                ClearPlayfieldForPostRunSurvey();
                float elapsed = m_UI.StopTimer();
                m_LastCompletedElapsed = elapsed;
                m_UI.ShowCompletion(k_TotalRounds, elapsed);
                OnGameCompleted?.Invoke(elapsed);
            }
            else
            {
                StartCoroutine(TransitionToNextRound());
            }
        }
        else
        {
            Debug.Log($"{k_Tag} Wrong: {info.DisplayName}, wanted {m_CurrentTarget.color}_{m_CurrentTarget.shape}");
            m_UI.ShowWrongFeedback();
            OnWrongCapture?.Invoke(info.DisplayName, $"{m_CurrentTarget.color}_{m_CurrentTarget.shape}");
            if (m_GazeDwell != null) m_GazeDwell.ResetDwell();
        }
    }

    // =====================================================================
    //  Round transition — simple: destroy, cross, wait, respawn
    // =====================================================================

    IEnumerator TransitionToNextRound()
    {
        m_State = GameState.Transitioning;
        if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

        // Pause timer until the new objective is announced
        m_UI.PauseTimer();

        // Destroy all objects
        foreach (var obj in m_SpawnedObjects)
            if (obj != null) Destroy(obj);
        m_SpawnedObjects.Clear();
        m_SpawnPoints.Clear();
        m_UI.HideObjectiveDuringTransition();

        // Show fixation cross with next-goal text in the top-left.
        if (m_CurrentRound >= 0 && m_CurrentRound < ChallengeSet.RoundCount)
        {
            var nextTarget = ChallengeSet.Rounds[m_CurrentRound].target;
            m_UI.ShowFixationCross(nextTarget.color, nextTarget.shape);
            OnRoundTransitionStarted?.Invoke(m_CurrentRound, nextTarget.color, nextTarget.shape);
        }
        else
        {
            m_UI.ShowFixationCross();
        }

        // Pause
        yield return new WaitForSeconds(k_TransitionPause);

        // Hide cross
        m_UI.HideFixationCross();

        // Randomized blank interval to reduce anticipation before objects appear.
        float blankPause = Random.Range(k_BlankPauseMin, k_BlankPauseMax);
        yield return new WaitForSeconds(blankPause);

        // Wait for destroys to fully process
        yield return null;
        yield return null;

        if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

        // Spawn next round
        m_State = GameState.Playing;
        yield return DoSpawnRound();
    }

    IEnumerator BeginFirstRoundTransition()
    {
        if (m_GazeDwell != null) m_GazeDwell.ResetDwell();
        m_UI.HideObjectiveDuringTransition();

        if (m_CurrentRound >= 0 && m_CurrentRound < ChallengeSet.RoundCount)
        {
            var firstTarget = ChallengeSet.Rounds[m_CurrentRound].target;
            m_UI.ShowFixationCross(firstTarget.color, firstTarget.shape);
            OnRoundTransitionStarted?.Invoke(m_CurrentRound, firstTarget.color, firstTarget.shape);
        }
        else
        {
            m_UI.ShowFixationCross();
        }

        yield return new WaitForSeconds(k_TransitionPause);
        m_UI.HideFixationCross();

        float blankPause = Random.Range(k_BlankPauseMin, k_BlankPauseMax);
        yield return new WaitForSeconds(blankPause);

        yield return null;
        yield return null;

        if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

        m_State = GameState.Playing;
        yield return DoSpawnRound();
    }

    void ShowCurrentObjective()
    {
        m_UI.ShowObjective(m_CurrentTarget.colorValue,
            $"{m_CurrentTarget.color} {m_CurrentTarget.shape}",
            m_CurrentRound, k_TotalRounds);
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(k_ResetDelay);
        foreach (var obj in m_SpawnedObjects) if (obj != null) Destroy(obj);
        m_SpawnedObjects.Clear();
        foreach (var s in m_ShelfObjects) if (s != null) Destroy(s);
        m_ShelfObjects.Clear();
        m_ShelvesBuilt = false;
        ShelfSpawner.ClearCache();
        m_SpawnPoints.Clear();
        m_Objectives.Clear();
        if (m_GazeDwell != null) m_GazeDwell.OnObjectCaptured -= OnObjectCaptured;
        m_UI.Hide();
        m_State = GameState.Idle;
        m_ResetCoroutine = null;
    }

    void HandleNasaTlxSubmitted(FindObjectUI.NasaTlxResult tlx)
    {
        if (m_State != GameState.Completed || m_UI == null) return;
        if (m_NasaTlxSubmittedForRun) return;
        if (m_TrialLogger == null) m_TrialLogger = GetComponent<TrialDataLogger>();
        if (m_TrialLogger != null)
            m_TrialLogger.RecordNasaTlx(
                tlx.mental, tlx.physical, tlx.temporal,
                tlx.performance, tlx.effort, tlx.frustration);
        m_NasaTlxSubmittedForRun = true;
        m_UI.ShowThankYouMessage();

        var voiceAssistant = GetComponent<VoiceAssistantController>();
        if (voiceAssistant != null)
            voiceAssistant.SpeakClosingThankYou();

        if (m_AppExitCoroutine != null)
            StopCoroutine(m_AppExitCoroutine);
        m_AppExitCoroutine = StartCoroutine(QuitApplicationAfterThankYouDelay());
    }

    IEnumerator QuitApplicationAfterThankYouDelay()
    {
        yield return new WaitForSeconds(k_ExitAfterThankYouDelay);
        Debug.Log($"{k_Tag} Closing application after thank-you message");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        m_AppExitCoroutine = null;
    }

    void HandleResetRequested()
    {
        if (m_ResetCoroutine != null)
        {
            StopCoroutine(m_ResetCoroutine);
            m_ResetCoroutine = null;
        }

        SessionConfig.ResetForNewParticipant();

        Debug.Log($"{k_Tag} Reset requested from completion UI; reloading scene to restart onboarding for a new participant");
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    string BuildFallbackStatsText()
    {
        int minutes = (int)(m_LastCompletedElapsed / 60f);
        float seconds = m_LastCompletedElapsed % 60f;
        string timeStr = minutes > 0 ? $"{minutes}:{seconds:00.0}s" : $"{seconds:F1}s";
        return
            "Session Stats\n" +
            $"Rounds completed: {m_CurrentRound}/{k_TotalRounds}\n" +
            $"Total time: {timeStr}\n" +
            "Detailed metrics saved to trial_summary.json";
    }

    void ClearPlayfieldForPostRunSurvey()
    {
        // End-of-run should show only post-run UI (NASA-TLX prompt),
        // not the searchable scene content.
        if (m_UI != null) m_UI.HideFixationCross();
        if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

        foreach (var obj in m_SpawnedObjects)
            if (obj != null) Destroy(obj);
        m_SpawnedObjects.Clear();
        m_SpawnPoints.Clear();

        foreach (var shelf in m_ShelfObjects)
            if (shelf != null) Destroy(shelf);
        m_ShelfObjects.Clear();
        m_ShelvesBuilt = false;
        ShelfSpawner.ClearCache();
    }

    static int GetParticipantNumber()
    {
        string id = SessionConfig.ParticipantId;
        if (!string.IsNullOrEmpty(id) && id.Length > 1 && int.TryParse(id.Substring(1), out int num))
            return num;
        return 1; // default
    }

    static void HideChildRenderers(GameObject obj)
    {
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }

    void BuildObjectiveList()
    {
        m_Objectives.Clear();
        var rounds = ChallengeSet.Rounds;
        for (int i = 0; i < k_TotalRounds; i++)
        {
            var t = rounds[i].target;
            m_Objectives.Add((t.shape, t.color, t.colorValue));
        }
    }
}
