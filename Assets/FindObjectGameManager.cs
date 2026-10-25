using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// 80-round conjunction search game with eight-plane beta or table-shelf layouts.
/// Player finds 1 target via controller ray and trigger. On correct capture: show fixation cross,
/// destroy objects, wait, spawn fresh, finalize gaze interactables, go.
/// </summary>
public class FindObjectGameManager : MonoBehaviour
{
    const string k_Tag = "[FindObjectGame]";
    static int k_ObjectsPerRound => ChallengeSet.ObjectsPerRound;
    static int k_TotalRounds => ChallengeSet.RoundCount;
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

    public event System.Action<int> OnRoundTimedOut;
    public event System.Action<int> OnSearchStarted;
    public event System.Action<string> OnSessionStopped;
    public event System.Action<string> OnCheckpoint;
    public bool SearchActive { get; private set; }
    public bool IsPractice { get; private set; }
    // The trial currently presented, retained through its success feedback.
    // CurrentObjectiveIndex advances on capture and is therefore not a recording ID.
    public int PresentedRoundIndex { get; private set; } = -1;
    public bool PresentedRoundIsPractice { get; private set; }
    public string PresentedVoiceTag { get; private set; }
    int m_PracticeIndex;
    public float CurrentSearchSeconds => (float)m_SearchClock.Elapsed(Time.timeAsDouble);
    readonly StudyTrialClock m_SearchClock = new StudyTrialClock();
    bool m_ResearcherPaused;
    public float TrialTimeoutSeconds => (float)StudyTrialClock.SearchLimitSeconds;
    int m_FoundCount;
    bool m_TechnicallyStopped;
    StudyCheckpoint m_Checkpoint;
    TrialReadinessGate m_Readiness;
    int m_LastCompletedPlane;
    bool m_PausedDuringReadiness;
    public TrialReadinessGate Readiness => m_Readiness;
    public bool AnnouncementReady { get; private set; }
    bool m_OpeningDirectionReady;
    public bool ResearcherPaused => m_ResearcherPaused;
    public event System.Action<string> OnReadinessEvent;
    public void RecordReadinessEvent(string name) => OnReadinessEvent?.Invoke(name);
    public void MarkAnnouncementReady(int round)
    {
        if (!m_TechnicallyStopped && !m_ResearcherPaused && m_State == GameState.Playing && m_CurrentRound == round)
            AnnouncementReady = true;
    }

    [System.Serializable]
    public sealed class ThetaSnapshot
    {
        public string reference = ChallengeSet.ThetaReference;
        public int round;
        public long gazeSequence;
        public double time, gazeReceiptTime;
        public Vector3 eyeOrigin, readinessPosition, targetPosition;
        public float assignedSignedDegrees, realizedSignedDegrees, absoluteDegrees, assignmentErrorDegrees;
        public float targetHeightRelativeToCross;
    }
    public ThetaSnapshot PreCueTheta { get; private set; }

    // Finalize hidden geometry before any directional cue. Never chase the eye
    // during search: subsequent movement and overshoot remain measured outcomes.
    public bool FinalizeTheta(GazeObservation gaze)
    {
        if (IsPractice || !m_UseRotationalLayout) return true;
        if (gaze == null || !gaze.valid || m_Readiness == null) return false;
        var planned = ChallengeSet.Rounds[m_CurrentRound];
        var inverse = Quaternion.Inverse(m_SeatedRotation);
        Vector3 origin = inverse * (gaze.worldOrigin - m_SpawnCenter);
        Vector3 cross = inverse * (m_Readiness.Position - m_SpawnCenter);
        float height = Mathf.Lerp(m_MinObjectHeight, m_MaxObjectHeight, planned.targetHeightFraction);
        if (!RotationalSearchLayout.TryTarget(m_RotationalRadius, origin.x, origin.z, cross.x, cross.z,
            planned.SignedTheta, planned.targetPlane, planned.targetSlot, height, out var target))
        {
            TechnicalStop("theta_geometry_outside_wall: recenter seated position before restarting");
            return false;
        }
        RotationalSearchLayout.Slot[] slots;
        try { slots = RotationalSearchLayout.Build(m_RotationalRadius, m_MinObjectHeight, m_MaxObjectHeight, LayoutSeed, target); }
        catch (System.Exception error)
        {
            Debug.LogError($"{k_Tag} Theta layout failed: {error.Message}");
            TechnicalStop("theta_layout_failed"); return false;
        }
        if (slots.Length != m_SpawnedObjects.Count) { TechnicalStop("theta_object_count_mismatch"); return false; }
        m_RotationalSlots = slots;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var obj = m_SpawnedObjects[i];
            if (obj == null) { TechnicalStop("theta_object_missing"); return false; }
            Vector3 position = m_SpawnCenter + m_SeatedRotation * new Vector3(slot.x, m_FloorHeight - m_SpawnCenter.y + slot.y, slot.z);
            if (obj.GetComponent<SpawnableObjectInfo>()?.shapeName == "Pyramid") position.y -= obj.transform.localScale.y * .5f;
            obj.transform.position = position;
        }
        Physics.SyncTransforms();
        Vector3 targetPosition = m_SpawnedObjects[planned.targetPlane * RotationalSearchLayout.ObjectsPerPlane + planned.targetSlot].transform.position;
        Vector3 toCross = m_Readiness.Position - gaze.worldOrigin, toTarget = targetPosition - gaze.worldOrigin;
        toCross.y = toTarget.y = 0;
        float realized = Vector3.SignedAngle(toCross, toTarget, Vector3.up);
        PreCueTheta = new ThetaSnapshot {
            round = m_CurrentRound, gazeSequence = gaze.sequence, time = Time.realtimeSinceStartupAsDouble,
            gazeReceiptTime = gaze.receiptTime, eyeOrigin = gaze.worldOrigin,
            readinessPosition = m_Readiness.Position, targetPosition = targetPosition,
            assignedSignedDegrees = planned.SignedTheta, realizedSignedDegrees = realized,
            absoluteDegrees = Mathf.Abs(realized), assignmentErrorDegrees = Mathf.DeltaAngle(planned.SignedTheta, realized),
            targetHeightRelativeToCross = targetPosition.y - m_Readiness.Position.y
        };
        if (Mathf.Abs(PreCueTheta.assignmentErrorDegrees) > .01f)
        { TechnicalStop("theta_assignment_mismatch"); return false; }
        return true;
    }

    public void BeginOpeningDirection(int round)
    {
        if (!CanOpenSearch(round) || m_OpeningDirectionReady) return;
        var voice = GetComponent<VoiceAssistantController>();
        if (voice == null) { TechnicalStop("audio_failure: opening voice unavailable"); return; }
        voice.AnnounceSearchOpening(round);
    }
    bool CanOpenSearch(int round) => !m_TechnicallyStopped && m_State == GameState.Playing &&
        m_CurrentRound == round && !SearchActive && !m_ResearcherPaused && AnnouncementReady &&
        m_Readiness != null && m_Readiness.Active && m_Readiness.Completed;

    public void CompleteOpeningDirection(int round)
    {
        if (!CanOpenSearch(round)) return;
        m_OpeningDirectionReady = true;
        RecordReadinessEvent("opening_direction_completed");
        BeginSearch(round);
    }

    public void BeginSearch(int round)
    {
        if (!CanOpenSearch(round) || !m_OpeningDirectionReady) return;
        foreach (var obj in m_SpawnedObjects)
            if (obj != null && obj.TryGetComponent<MeshRenderer>(out var renderer)) renderer.enabled = true;
        m_Readiness.Finish();
        SearchActive = true;
        m_SearchClock.Begin(Time.timeAsDouble);
        m_UI.ShowRoundAudioWait(false);
        m_ControllerSelector?.ResetSelection();
        m_UI.ResumeTimer();
        GetComponent<HintGenerator>()?.OnNewObjective();
        OnSearchStarted?.Invoke(round);
    }

    public void TechnicalStop(string reason)
    {
        if (m_TechnicallyStopped || m_State == GameState.Completed) return;
        m_SearchClock.Pause(Time.timeAsDouble);
        m_TechnicallyStopped = true; SearchActive = false;
        m_EntryAlignment?.Abort();
        GetComponent<HintGenerator>()?.SetLifecycle(GuidancePhase.Stopped, reason);
        GetComponent<VoiceAssistantController>()?.CancelSearchOpening();
        m_Readiness?.Cancel();
        if (m_StartAfterIntroCoroutine != null) { StopCoroutine(m_StartAfterIntroCoroutine); m_StartAfterIntroCoroutine = null; }
        m_UI.PauseTimer();
        m_UI.HideBlockSurvey();
        m_ControllerSelector?.ResetSelection();
        GetComponent<VoiceSynthesizer>()?.Stop();
        GetComponent<HintGenerator>()?.CancelPending();
        m_State = GameState.Completed;
        OnSessionStopped?.Invoke(reason);
        if (m_Checkpoint == null) m_Checkpoint = gameObject.AddComponent<StudyCheckpoint>();
        m_Checkpoint.Show("Session stopped: " + reason + "\nNo replacement trial. Save logs before restarting.");
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused || m_State == GameState.Idle || m_State == GameState.Completed) return;
        if (SearchActive || (m_Readiness != null && m_Readiness.Active)) PauseSession();
        else if (!m_ResearcherPaused) TechnicalStop("interrupted");
    }

    public void WithdrawSession() => TechnicalStop("withdrawal");

    public void PauseSession()
    {
        if ((!SearchActive && (m_Readiness == null || !m_Readiness.Active)) || m_ResearcherPaused) return;
        m_PausedDuringReadiness = !SearchActive;
        if (m_PausedDuringReadiness)
        {
            GetComponent<VoiceAssistantController>()?.CancelSearchOpening();
            m_OpeningDirectionReady = false; m_Readiness.ResetCharge(); m_UI.HideFixationCross();
        }
        GetComponent<HintGenerator>()?.SetLifecycle(GuidancePhase.Paused, "researcher_pause");
        m_ResearcherPaused = true; SearchActive = false; m_SearchClock.Pause(Time.timeAsDouble);
        m_UI.PauseTimer(); m_ControllerSelector?.ResetSelection();
        GetComponent<VoiceSynthesizer>()?.Stop(); GetComponent<HintGenerator>()?.CancelPending();
        foreach (var obj in m_SpawnedObjects) if (obj != null) obj.SetActive(false);
        OnCheckpoint?.Invoke("search_paused");
        if (m_Checkpoint == null) m_Checkpoint = gameObject.AddComponent<StudyCheckpoint>();
        m_Checkpoint.Show("Session paused. Search objects are hidden.");
    }
    public void ResumeSession()
    {
        if (!m_ResearcherPaused || m_TechnicallyStopped) return;
        m_Checkpoint?.Confirm();
        foreach (var obj in m_SpawnedObjects) if (obj != null) obj.SetActive(true);
        m_ControllerSelector?.ResetSelection();
        m_ResearcherPaused = false;
        if (m_PausedDuringReadiness)
        {
            GetComponent<HintGenerator>()?.SetLifecycle(GuidancePhase.Opening, "readiness_resumed");
            AnnouncementReady = false;
            m_Readiness.Show();
            GetComponent<VoiceAssistantController>()?.RepeatReadinessAnnouncement();
            OnCheckpoint?.Invoke("readiness_resumed"); return;
        }
        SearchActive = true; m_SearchClock.Resume(Time.timeAsDouble);
        m_UI.ResumeTimer(); GetComponent<HintGenerator>()?.OnNewObjective(resumed: true);
        OnCheckpoint?.Invoke("search_resumed");
    }

    void Update()
    {
        if (m_ResearcherPaused && m_Checkpoint != null && !m_Checkpoint.Waiting) ResumeSession();
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.pKey.wasPressedThisFrame) PauseSession();
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && m_State != GameState.Idle) WithdrawSession();
#endif
        CheckSearchDeadline();
    }

    public GameState CurrentState => m_State;
    public IReadOnlyList<(string shape, string color, Color colorValue)> Objectives => m_Objectives;
    public IReadOnlyList<GameObject> SpawnedObjects => m_SpawnedObjects;
    public int CurrentObjectiveIndex => m_CurrentRound;
    public int FoundCount => m_FoundCount;
    public int TotalRounds => k_TotalRounds;
    public float GameStartTime => m_GameStartTime;
    public (string shape, string color, Color colorValue) CurrentTarget => m_CurrentTarget;
    public bool CurrentRoundGazeAware => true;
    public string CurrentRoundConditionLabel => "gaze_aware";

    [Header("Debug")]
    [SerializeField] bool m_UseRotationalLayout = true;
    [SerializeField, Range(1f, 3f)] float m_RotationalRadius = RotationalSearchLayout.DefaultRadius;
    [SerializeField, Min(0f)] float m_MinObjectHeight = RotationalSearchLayout.DefaultMinHeight;
    [SerializeField, Min(0.1f)] float m_MaxObjectHeight = RotationalSearchLayout.DefaultMaxHeight;
    [SerializeField] bool m_ShowDegreeGuides = true;
    readonly List<GameObject> m_DegreeGuides = new();
    float m_FloorHeight;
    public float SearchPlaneHeight => m_MaxObjectHeight + RotationalSearchLayout.TopMargin;
    public float SearchPlaneFloorY => m_FloorHeight;

    // Intersect the eye ray with the target wall's object plane, even above or
    // below its outline. Clamping would understate a large vertical correction.
    public bool TryVerticalGuidanceError(Vector3 origin, Vector3 direction, int plane, float targetY,
        out float heightError)
    {
        heightError = 0;
        if (plane < 0 || plane >= RotationalSearchLayout.PlaneCount || SearchPlaneHeight <= 0) return false;
        Vector3 normal = m_SeatedRotation * Quaternion.Euler(0, plane * RotationalSearchLayout.PlaneAngleDegrees, 0) * Vector3.forward;
        var wall = new Plane(normal, m_SpawnCenter + normal * m_RotationalRadius);
        var ray = new Ray(origin, direction);
        if (!wall.Raycast(ray, out float distance) || distance <= 0) return false;
        heightError = targetY - ray.GetPoint(distance).y;
        return !float.IsNaN(heightError) && !float.IsInfinity(heightError);
    }
    public bool IsInsideSearchPlane(Vector3 point, int plane)
    {
        Quaternion rotation = m_SeatedRotation * Quaternion.Euler(0, plane * RotationalSearchLayout.PlaneAngleDegrees, 0);
        Vector3 local = Quaternion.Inverse(rotation) * (point - m_SpawnCenter);
        float halfWidth = m_RotationalRadius * Mathf.Tan(Mathf.PI / RotationalSearchLayout.PlaneCount);
        return Mathf.Abs(local.x) <= halfWidth && point.y >= m_FloorHeight &&
            point.y <= m_FloorHeight + SearchPlaneHeight;
    }
    // Guidance zones are horizontal sectors. Vertical scanning must not count as
    // leaving/reentering the correct area or trigger an overshoot cue.
    public bool IsGazeInSearchSector(Vector3 origin, Vector3 direction, int plane, out bool valid)
    {
        valid = false;
        if (plane < 0 || plane >= RotationalSearchLayout.PlaneCount) return false;
        Quaternion inverse = Quaternion.Inverse(m_SeatedRotation * Quaternion.Euler(0,
            plane * RotationalSearchLayout.PlaneAngleDegrees, 0));
        Vector3 localOrigin = inverse * (origin - m_SpawnCenter);
        Vector3 localDirection = inverse * direction;
        return RotationalSearchLayout.IsInHorizontalSector(localOrigin.x, localOrigin.z,
            localDirection.x, localDirection.z, m_RotationalRadius, out valid);
    }

    public int LayoutSeed { get; private set; }
    // This preference hides only debug wall outlines/degree labels. Search-zone
    // geometry, objects, readiness and gaze guidance do not depend on visibility.
    public bool DegreeGuidesVisible => m_ShowDegreeGuides;
    public event System.Action<bool> DegreeGuidesVisibilityChanged;
    public void SetDegreeGuidesVisible(bool visible)
    {
        m_ShowDegreeGuides = visible;
        foreach (var guide in m_DegreeGuides) if (guide != null) guide.SetActive(visible);
        DegreeGuidesVisibilityChanged?.Invoke(visible);
    }
    public bool RotationalBetaEnabled => m_UseRotationalLayout;
    public Vector3 SeatedOrigin => m_SpawnCenter;
    public float SeatedForwardYaw => m_SeatedRotation.eulerAngles.y;
    Quaternion m_SeatedRotation = Quaternion.identity;
    RotationalSearchLayout.Slot[] m_RotationalSlots;
    Material m_PlaneOutlineMaterial;
    bool m_CenterAccepted;
    EntryGazeAlignment m_EntryAlignment;
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
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || !go.scene.IsValid())
                continue;

            string n = go.name;
            bool isTemplateTutorialUi =
                n == "Text Poke Button OK" ||
                n == "Text Poke Button" ||
                n == "Text Poke Button Continue" ||
                n == "Spatial Panel Manipulator" ||
                n == "Video Player Slider" ||
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
    ControllerRaySelector m_ControllerSelector;
    TrialDataLogger m_TrialLogger;
    Coroutine m_ResetCoroutine;
    Coroutine m_AppExitCoroutine;
    Coroutine m_StartAfterIntroCoroutine;
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
        if (GetComponent<StudyRayControls>() == null) gameObject.AddComponent<StudyRayControls>();
        m_Readiness = GetComponent<TrialReadinessGate>();
        if (m_Readiness == null) m_Readiness = gameObject.AddComponent<TrialReadinessGate>();
        if (GetComponent<TrialReplayRecorder>() == null) gameObject.AddComponent<TrialReplayRecorder>();
        if (m_UI == null) { m_UI = gameObject.AddComponent<FindObjectUI>(); m_UI.Initialize(); }
        if (m_UI != null)
            m_UI.ShowStartPrompt();
        if (m_UI != null)
        {
            m_UI.OnNasaTlxSubmitted -= HandleNasaTlxSubmitted;
            m_UI.OnNasaTlxSubmitted += HandleNasaTlxSubmitted;
            m_UI.OnResetRequested -= HandleResetRequested;
            m_UI.OnResetRequested += HandleResetRequested;
        }
        m_Spawner.objectSpawned += OnObjectSpawned;
    }

    IEnumerator Start()
    {
        if (!m_UseRotationalLayout) yield break;
        VoiceModeSelector selector;
        VoiceAssistantController voice;
        do
        {
            yield return null;
            if (m_TechnicallyStopped || m_CenterAccepted) yield break;
            selector = GetComponent<VoiceModeSelector>();
            voice = GetComponent<VoiceAssistantController>();
        } while (selector == null || !selector.IsComplete || voice == null || !voice.IsReady);
        // Let the setup confirmation disappear before showing the independent start gate.
        if (!SessionConfig.DevelopmentQuickTest) yield return new WaitForSeconds(3.1f);
        if (m_TechnicallyStopped || m_CenterAccepted) yield break;
        m_UI.HideStartPrompt();
        // Participant/voice setup is settled before explicit calibration and acceptance.
        // Neither the practice trial nor its clock may begin behind this screen.
        while (Camera.main == null && !m_TechnicallyStopped) yield return null;
        if (m_TechnicallyStopped) yield break;
        m_EntryAlignment = GetComponent<EntryGazeAlignment>();
        if (m_EntryAlignment == null) m_EntryAlignment = gameObject.AddComponent<EntryGazeAlignment>();
        yield return m_EntryAlignment.Run();
        if (m_TechnicallyStopped || !m_EntryAlignment.Completed) yield break;
        if (m_Checkpoint == null) m_Checkpoint = gameObject.AddComponent<StudyCheckpoint>();
        while (!m_CenterAccepted && !m_TechnicallyStopped)
        {
            m_Checkpoint.Show((SessionConfig.DevelopmentQuickTest ? "<b>" + SessionConfig.QuickTestTitle + " · DEVELOPMENT ONLY</b>\n" : "<b>360° SEARCH BETA</b>\n") +
                "Sit at the center and face your chosen forward direction.\nEight planes will surround you, 21 objects on each.\nStay seated and turn to search.",
                "Center and begin");
            while (m_Checkpoint.Waiting && !m_TechnicallyStopped) yield return null;
            if (m_TechnicallyStopped || m_CenterAccepted) yield break;
            if (!StartCenteredSession())
                Debug.LogWarning($"{k_Tag} Centering not ready; keep the headset on and face forward, then try again.");
        }
    }

    public bool StartCenteredSession()
    {
        if (m_EntryAlignment == null || !m_EntryAlignment.Completed) return false;
        if (!m_UseRotationalLayout || m_CenterAccepted || m_State != GameState.Idle || !Application.isFocused || Camera.main == null) return false;
        var selector = GetComponent<VoiceModeSelector>();
        var voice = GetComponent<VoiceAssistantController>();
        if (selector == null || !selector.IsComplete || voice == null || !voice.IsReady) return false;
        if (m_Spawner == null || m_Spawner.objectPrefabs == null || m_Spawner.objectPrefabs.Count == 0) return false;
        if (GetComponent<ShapeObjectFactory>() == null) return false;
        var origin = Camera.main.GetComponentInParent<XROrigin>();
        if (origin == null || origin.CurrentTrackingOriginMode != TrackingOriginModeFlags.Floor)
        {
            Debug.LogWarning($"{k_Tag} Floor tracking must be ready before centering the floor-height layout.");
            return false;
        }
        m_FloorHeight = origin.Origin.transform.position.y;
        m_SpawnCenter = Camera.main.transform.position;
        Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) return false;
        m_SeatedRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        m_RotationalRadius = Mathf.Clamp(m_RotationalRadius, 1f, 3f);
        m_CenterAccepted = true;
        m_Checkpoint?.Confirm();
        voice.PlayStudyIntroduction();
        StartGame(null);
        return true;
    }

    void OnDisable()
    {
        ChallengeSet.DebugRoundCountOverride = 0;
        if (m_Spawner != null) m_Spawner.objectSpawned -= OnObjectSpawned;
        if (m_ControllerSelector != null) m_ControllerSelector.OnObjectCaptured -= OnObjectCaptured;
        if (m_StartAfterIntroCoroutine != null)
        {
            StopCoroutine(m_StartAfterIntroCoroutine);
            m_StartAfterIntroCoroutine = null;
        }
        if (m_UI != null)
        {
            m_UI.OnNasaTlxSubmitted -= HandleNasaTlxSubmitted;
            m_UI.OnResetRequested -= HandleResetRequested;
        }
    }

    void OnObjectSpawned(GameObject obj)
    {
        if (m_UseRotationalLayout) { Destroy(obj); return; }
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

        if (!m_UseRotationalLayout)
        {
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
        }

        m_ControllerSelector = GetComponent<ControllerRaySelector>();
        if (m_ControllerSelector == null) m_ControllerSelector = gameObject.AddComponent<ControllerRaySelector>();
        if (m_ControllerSelector != null)
        {
            if (m_UseRotationalLayout)
            {
                FindObjectOfType<GazeHighlightManager>()?.ConfigureThroughWallSearch();
                m_ControllerSelector.ThroughWallSearch = true;
            }
            m_ControllerSelector.OnObjectCaptured += OnObjectCaptured;
        }
        m_NasaTlxSubmittedForRun = false;

        var voice = GetComponent<VoiceAssistantController>();
        if (voice != null && voice.IsIntroBlockingExperimentStart)
        {
            if (m_UI != null)
                m_UI.ShowStartPromptWaiting();
            m_StartAfterIntroCoroutine = StartCoroutine(StartGameAfterIntroCoroutine(voice));
            return;
        }

        if (m_UI != null)
            m_UI.HideStartPrompt();
        BeginSessionStart();
    }

    IEnumerator StartGameAfterIntroCoroutine(VoiceAssistantController voice)
    {
        while (voice != null && voice.IsIntroBlockingExperimentStart)
            yield return null;

        m_StartAfterIntroCoroutine = null;
        BeginSessionStart();
    }

    void BeginSessionStart()
    {
        if (m_State != GameState.Idle)
            return;

        if (m_UI != null)
            m_UI.HideStartPrompt();

        // Start with the same transition ritual as between rounds:
        // fixation cross + announced goal, then spawn.
        m_State = GameState.Transitioning;
        m_GameStartTime = Time.time;
        m_CurrentRound = 0; m_FoundCount = 0;
        PresentedRoundIndex = -1;
        m_UI.StartTimer();
        m_UI.PauseTimer();
        BuildObjectiveList();

        // Show participant state before emitting the session start event.
        m_UI.SetAgentState(CurrentRoundGazeAware, CurrentRoundConditionLabel);

        if (!m_ShelvesBuilt)
        {
            if (m_UseRotationalLayout)
            {
                CreateRotationalFrames();
                m_UI.PositionForRotationalSearch();
            }
            else
            {
                var (shelfObjs, _) = ShelfSpawner.CreateShelvesAndSpawnPoints(
                    m_SpawnCenter, m_PlaneSize, m_PlaneRight, m_PlaneForward, k_ObjectsPerRound);
                m_ShelfObjects.AddRange(shelfObjs);
                m_UI.PositionStaticLeft(m_SpawnCenter, ShelfSpawner.ObjectFacingRotation);
            }
            m_ShelvesBuilt = true;
        }

        IsPractice = !SessionConfig.DevelopmentQuickTest; m_PracticeIndex = 0;
        if (IsPractice) SetPracticeObjective();
        else OnGameStarted?.Invoke(); // Start the normal loggers before the first test trial.
        StartCoroutine(BeginFirstRoundTransition());
    }

    // =====================================================================
    //  Spawn round — instantiate, configure, wait, finalize
    // =====================================================================

    void SetPracticeObjective()
    {
        var practice = ChallengeSet.PracticeRound(m_PracticeIndex);
        m_CurrentRound = practice.roundIndex;
        while (m_Objectives.Count <= m_CurrentRound) m_Objectives.Add(("", "", Color.white));
        m_Objectives[m_CurrentRound] = (practice.target.shape, practice.target.color, practice.target.colorValue);
    }

    void CreateRotationalFrames()
    {
        m_DegreeGuides.Clear();
        float bottom = m_FloorHeight - m_SpawnCenter.y;
        float top = bottom + SearchPlaneHeight;
        float halfWidth = RotationalSearchLayout.FrameHalfWidth(m_RotationalRadius);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            m_PlaneOutlineMaterial = new Material(shader);
            m_PlaneOutlineMaterial.color = new Color(0.55f, 0.65f, 0.7f, 1f);
        }
        for (int plane = 0; plane < RotationalSearchLayout.PlaneCount; plane++)
        {
            float azimuth = plane * RotationalSearchLayout.PlaneAngleDegrees;
            Quaternion rotation = m_SeatedRotation * Quaternion.Euler(0f, azimuth, 0f);
            Vector3 center = m_SpawnCenter + rotation * Vector3.forward * (m_RotationalRadius + RotationalSearchLayout.FrameDepthOffset);
            var frame = new GameObject($"SearchPlane_{plane}_{azimuth}deg");
            frame.transform.SetPositionAndRotation(center, rotation);
            if (m_PlaneOutlineMaterial != null)
            {
                var line = frame.AddComponent<LineRenderer>();
                line.sharedMaterial = m_PlaneOutlineMaterial;
                line.useWorldSpace = false;
                line.loop = true; line.widthMultiplier = 0.006f;
                line.positionCount = 4;
                line.SetPositions(new[] { new Vector3(-halfWidth, bottom, 0), new Vector3(-halfWidth, top, 0),
                    new Vector3(halfWidth, top, 0), new Vector3(halfWidth, bottom, 0) });
            }
            var label = new GameObject("PlaneLabel");
            label.transform.SetParent(frame.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.43f, 0f);
            var text = label.AddComponent<TextMeshPro>();
            // Signed labels around seated forward; the rear wall is both -180 and +180.
            float signedAzimuth = Mathf.DeltaAngle(0f, azimuth);
            text.text = plane == 0 ? "0° · FORWARD" : plane == 4 ? "±180°" : $"{signedAzimuth:+0;-0;0}°";
            text.fontSize = 1.8f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(0.8f, 0.15f);
            m_ShelfObjects.Add(frame);
            m_DegreeGuides.Add(frame);
            frame.SetActive(m_ShowDegreeGuides);
        }
        Debug.Log($"{k_Tag} Rotational beta centered at {m_SpawnCenter}, yaw={SeatedForwardYaw:F1}, radius={m_RotationalRadius:F2}; 8 planes x 21 objects.");
    }

    void OnDestroy()
    {
        if (m_PlaneOutlineMaterial != null) Destroy(m_PlaneOutlineMaterial);
    }

    IEnumerator FinishPractice()
    {
        while (GetComponent<VoiceAssistantController>()?.SuccessFeedbackBusy == true) yield return null;
        if (m_TechnicallyStopped) yield break;
        m_State = GameState.Transitioning;
        foreach (var obj in m_SpawnedObjects) if (obj != null) Destroy(obj);
        m_SpawnedObjects.Clear();
        m_UI.HideObjectiveDuringTransition();
        if (m_Checkpoint == null) m_Checkpoint = gameObject.AddComponent<StudyCheckpoint>();
        m_Checkpoint.Show("Practice complete.\nConfirm task understanding before beginning the two study blocks.");
        while (m_Checkpoint.Waiting && !m_TechnicallyStopped) yield return null;
        if (m_TechnicallyStopped) yield break;
        IsPractice = false; m_CurrentRound = 0; BuildObjectiveList();
        m_GameStartTime = Time.time; m_UI.StartTimer(); m_UI.PauseTimer();
        SessionConfig.ApplyRoundVoice(0);
        OnGameStarted?.Invoke();
        yield return BeginFirstRoundTransition();
    }

    IEnumerator DoSpawnRound()
    {
        // --- Deterministic challenge from ChallengeSet ---
        var round = IsPractice ? ChallengeSet.PracticeRound(m_PracticeIndex) : ChallengeSet.Rounds[m_CurrentRound];
        m_CurrentTarget = (round.target.shape, round.target.color, round.target.colorValue);

        // The guidance condition is fixed throughout the run.
        int participantNumber = GetParticipantNumber();
        m_UI.SetAgentState(CurrentRoundGazeAware, CurrentRoundConditionLabel);
        Debug.Log($"{k_Tag} Round {m_CurrentRound + 1}: participant={participantNumber}, scheduleIndex={round.blockIndex}, condition={CurrentRoundConditionLabel}");

        // --- Get spawn points ---
        m_SpawnPoints.Clear();
        if (m_UseRotationalLayout)
        {
            LayoutSeed = IsPractice ? RotationalSearchLayout.SeedBase + 1000 + m_CurrentRound : round.layoutSeed;
            m_RotationalSlots = RotationalSearchLayout.Build(m_RotationalRadius, m_MinObjectHeight, m_MaxObjectHeight, LayoutSeed);
            foreach (var slot in m_RotationalSlots)
                m_SpawnPoints.Add(new ShelfSpawner.SpawnPoint
                {
                    position = m_SpawnCenter + m_SeatedRotation * new Vector3(slot.x, m_FloorHeight - m_SpawnCenter.y + slot.y, slot.z),
                    row = slot.slot, col = slot.plane
                });
        }
        else
        {
            m_SpawnPoints.AddRange(ShelfSpawner.ComputeSpawnPoints(
                m_SpawnCenter, m_PlaneSize, m_PlaneRight, m_PlaneForward, ChallengeSet.ObjectsPerRound));
        }

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
            obj.transform.rotation = m_UseRotationalLayout
                ? m_SeatedRotation * Quaternion.Euler(0f, m_RotationalSlots[i].azimuth + 180f, 0f)
                : ShelfSpawner.ObjectFacingRotation;

            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info != null)
            {
                info.objectId = $"{(IsPractice ? "practice" : "r")}{m_CurrentRound:D2}_o{i:D2}";
                info.shelfLevel = sp.row;
                info.shelfColumn = sp.col;
                if (m_UseRotationalLayout)
                {
                    info.planeId = m_RotationalSlots[i].plane;
                    info.planeSlot = m_RotationalSlots[i].slot;
                    info.planeAzimuth = m_RotationalSlots[i].azimuth;
                }

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
            if (rootRenderer != null) rootRenderer.enabled = false; // Reveal only at search onset.

            m_SpawnedObjects.Add(obj);
        }

        // Wait for DestroyImmediate to fully process
        yield return null;

        if (m_TechnicallyStopped) yield break;

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
            ControllerRaySelector.PreventGrab(grab);

            grab.colliders.Clear();
            foreach (var col in obj.GetComponents<Collider>())
                grab.colliders.Add(col);
        }

        // Wait for interaction manager to register all new interactables
        yield return null;
        yield return null;

        if (m_TechnicallyStopped) yield break;

        // Require a released trigger before confirming on these new objects
        if (m_ControllerSelector != null) m_ControllerSelector.ResetSelection();

        ShowCurrentObjective();
        OnRoundReady?.Invoke(m_CurrentRound, m_CurrentTarget.color, m_CurrentTarget.shape);
        Debug.Log($"{k_Tag} Round {m_CurrentRound + 1}/{k_TotalRounds}: locate {m_CurrentTarget.color} {m_CurrentTarget.shape} ({m_SpawnedObjects.Count} spawned)");
    }

    // =====================================================================
    //  Capture
    // =====================================================================

    void OnObjectCaptured(GameObject obj)
    {
        if (m_State != GameState.Playing || !SearchActive) return;
        if (CheckSearchDeadline()) return; // Deadline wins even if trigger processing precedes Update.
        if (obj == null || !obj.activeInHierarchy) return;

        var info = obj.GetComponent<SpawnableObjectInfo>();
        if (info == null) return;

        bool correct = info.shapeName == m_CurrentTarget.shape && info.colorName == m_CurrentTarget.color;
        // Preserve trial identity and trigger/ray state before any capture callbacks advance it.
        GetComponent<TrialReplayRecorder>()?.Selection(obj, correct);
        if (correct)
        {
            m_LastCompletedPlane = info.planeId >= 0 ? info.planeId : 0;
            Debug.Log($"{k_Tag} Round {m_CurrentRound + 1} correct: {info.DisplayName}");
            m_SearchClock.Pause(Time.timeAsDouble);
            SearchActive = false;
            GetComponent<HintGenerator>()?.SetLifecycle(GuidancePhase.Completed, "correct_target_selected");
            m_UI.PauseTimer();
            TargetCrumble.Play(obj, this);
            AdvanceAfterRound(true);
        }
        else
        {
            Debug.Log($"{k_Tag} Wrong: {info.DisplayName}, wanted {m_CurrentTarget.color}_{m_CurrentTarget.shape}");
            m_UI.ShowWrongFeedback();
            OnWrongCapture?.Invoke(info.DisplayName, $"{m_CurrentTarget.color}_{m_CurrentTarget.shape}");
            if (m_ControllerSelector != null) m_ControllerSelector.ResetSelection();
        }
    }

    bool CheckSearchDeadline()
    {
        if (!TryExpireRound()) return false;
        AdvanceAfterRound(false);
        return true;
    }

    bool TryExpireRound()
    {
        if (m_State != GameState.Playing || !SearchActive || !m_SearchClock.DeadlineReached(Time.timeAsDouble)) return false;
        m_SearchClock.Expire();
        SearchActive = false;
        GetComponent<HintGenerator>()?.SetLifecycle(GuidancePhase.TimedOut, "search_deadline_reached");
        GetComponent<HintGenerator>()?.CancelPending();
        GetComponent<VoiceSynthesizer>()?.Stop();
        m_ControllerSelector?.ResetSelection();
        m_UI?.PauseTimer(); m_UI?.ShowTimeout();
        // The schedule chains each next start to this trial's planned target wall, even on a miss.
        var planned = IsPractice ? ChallengeSet.PracticeRound(m_PracticeIndex) : ChallengeSet.Rounds[m_CurrentRound];
        m_LastCompletedPlane = planned.targetPlane;
        foreach (var obj in m_SpawnedObjects) if (obj != null) obj.SetActive(false);
        OnRoundTimedOut?.Invoke(m_CurrentRound); // Preserve presented trial and voice through feedback.
        return true;
    }

    void AdvanceAfterRound(bool selected)
    {
        if (IsPractice)
        {
            if (selected) GetComponent<VoiceAssistantController>()?.PlaySuccessFeedback();
            m_PracticeIndex++;
            if (m_PracticeIndex < ChallengeSet.PracticeTrialCount) { SetPracticeObjective(); StartCoroutine(TransitionToNextRound()); }
            else StartCoroutine(FinishPractice());
            return;
        }
        int ended = m_CurrentRound++;
        if (selected) { m_FoundCount++; OnObjectFound?.Invoke(ended); }
        m_ControllerSelector?.ResetSelection();
        if (m_CurrentRound >= k_TotalRounds)
        {
            m_State = GameState.Completed;
            ClearPlayfieldForPostRunSurvey();
            float elapsed = m_UI.StopTimer();
            m_LastCompletedElapsed = elapsed;
            m_UI.ShowCompletion(k_TotalRounds, elapsed, externalSurvey: !SessionConfig.DevelopmentQuickTest);
            OnGameCompleted?.Invoke(elapsed);
        }
        else StartCoroutine(TransitionToNextRound());
    }

    // =====================================================================
    //  Round transition — simple: destroy, cross, wait, respawn
    // =====================================================================

    IEnumerator TransitionToNextRound()
    {
        while (GetComponent<VoiceAssistantController>()?.SuccessFeedbackBusy == true) yield return null;
        if (m_TechnicallyStopped) yield break;
        m_State = GameState.Transitioning;
        if (m_ControllerSelector != null) m_ControllerSelector.ResetSelection();

        // Pause timer until the new objective is announced
        m_UI.PauseTimer();

        // Destroy all objects
        foreach (var obj in m_SpawnedObjects)
            if (obj != null) Destroy(obj);
        m_SpawnedObjects.Clear();
        m_SpawnPoints.Clear();
        m_UI.HideObjectiveDuringTransition();

        if (!IsPractice && SessionConfig.VoiceBlocksEnabled && m_CurrentRound == ChallengeSet.RoundsPerBlock)
        {
            GetComponent<VoiceSynthesizer>()?.Stop();
            GetComponent<HintGenerator>()?.CancelPending();
            OnCheckpoint?.Invoke("block_end");
            if (m_UseRotationalLayout) m_UI.PositionForRotationalSearch();
            else m_UI.PositionStaticLeft(m_SpawnCenter, ShelfSpawner.ObjectFacingRotation);
            if (m_Checkpoint == null) m_Checkpoint = gameObject.AddComponent<StudyCheckpoint>();
            m_Checkpoint.Show("Block 1 complete.\nTake a break. Check comfort and eye tracking.\nConfirm when ready for the second voice.");
            while (m_Checkpoint.Waiting && !m_TechnicallyStopped) yield return null;
            if (m_TechnicallyStopped) yield break;
            SessionConfig.ApplyRoundVoice(m_CurrentRound);
            OnCheckpoint?.Invoke("block_start");
        }

        yield return BeginFirstRoundTransition();
    }

    IEnumerator BeginFirstRoundTransition()
    {
        m_State = GameState.Transitioning;
        AnnouncementReady = false; m_OpeningDirectionReady = false; PreCueTheta = null;
        GetComponent<VoiceAssistantController>()?.CancelSearchOpening();
        m_ControllerSelector?.ResetSelection();
        m_UI.HideObjectiveDuringTransition();
        var planned = IsPractice ? ChallengeSet.PracticeRound(m_PracticeIndex) : ChallengeSet.Rounds[m_CurrentRound];
        // Match the real completed target wall to the planned start, including both boundaries.
        if (m_UseRotationalLayout && planned.startPlane != m_LastCompletedPlane)
        {
            TechnicalStop("readiness_start_wall_mismatch"); yield break;
        }
        Quaternion rotation = m_SeatedRotation * Quaternion.Euler(0, m_LastCompletedPlane * 45, 0);
        Vector3 crossPosition = m_SpawnCenter + rotation * Vector3.forward * m_RotationalRadius;
        if (!IsPractice) crossPosition += rotation * Vector3.right *
            (planned.crossOffsetFraction * RotationalSearchLayout.UsableHalfWidth(m_RotationalRadius));
        crossPosition.y = m_FloorHeight + (m_MinObjectHeight + m_MaxObjectHeight) * 0.5f;
        // Establish replay/trial identity before the first readiness event or next instruction.
        SetPresentedTrialContext();
        OnRoundTransitionStarted?.Invoke(m_CurrentRound, planned.target.color, planned.target.shape);
        m_Readiness.Begin(m_LastCompletedPlane, crossPosition, rotation, planned.target.color, planned.target.shape, IsPractice);
        // Let destruction finish, then prepare hidden objects while the cross charges.
        yield return null;
        if (m_TechnicallyStopped) yield break;
        m_State = GameState.Playing;
        yield return DoSpawnRound();
    }

    // Called only when the next trial is actually presented, not at capture time.
    void SetPresentedTrialContext()
    {
        PresentedRoundIndex = m_CurrentRound;
        PresentedRoundIsPractice = IsPractice;
        PresentedVoiceTag = SessionConfig.VoiceBlocksEnabled ? SessionConfig.VoiceLabelForRound(m_CurrentRound) : SessionConfig.VoiceTag;
    }

    void ShowCurrentObjective()
    {
        m_UI.ShowObjective(m_CurrentTarget.colorValue,
            $"{m_CurrentTarget.color} {m_CurrentTarget.shape}",
            IsPractice ? m_PracticeIndex : m_CurrentRound, IsPractice ? ChallengeSet.PracticeTrialCount : k_TotalRounds, IsPractice);
        m_UI.ShowRoundAudioWait(true);
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
        if (m_ControllerSelector != null) m_ControllerSelector.OnObjectCaptured -= OnObjectCaptured;
        if (m_UI != null) m_UI.ShowStartPrompt();
        m_State = GameState.Idle;
        m_ResetCoroutine = null;
    }

    void HandleNasaTlxSubmitted(FindObjectUI.NasaTlxResult tlx)
    {
        if (m_TechnicallyStopped) return;
        // Study survey responses arrive through the external, versioned final survey.
        if (!SessionConfig.DevelopmentQuickTest) return;
        if (m_State != GameState.Completed || m_UI == null) return;
        if (m_NasaTlxSubmittedForRun) return;
        if (m_TrialLogger == null) m_TrialLogger = GetComponent<TrialDataLogger>();
        if (m_TrialLogger != null)
            m_TrialLogger.RecordNasaTlx(
                tlx.mental, tlx.physical, tlx.temporal,
                tlx.performance, tlx.effort, tlx.frustration, SessionConfig.VoiceBlocksEnabled ? 1 : -1);
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
        if (m_ControllerSelector != null) m_ControllerSelector.ResetSelection();

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
