using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// 7-round conjunction search game. Each round spawns 42 objects on bookcases.
/// Player finds 1 target via gaze dwell. On correct capture: show fixation cross,
/// destroy objects, wait, spawn fresh, finalize gaze interactables, go.
/// </summary>
public class FindObjectGameManager : MonoBehaviour
{
    const string k_Tag = "[FindObjectGame]";
    static int k_ObjectsPerRound => ChallengeSet.ObjectsPerRound;
    static int k_TotalRounds => ChallengeSet.RoundCount;
    const float k_TransitionPause = 4f;
    const float k_ResetDelay = 5f;

    public enum GameState { Idle, Playing, Transitioning, Completed }

    public event System.Action OnGameStarted;
    public event System.Action<int> OnObjectFound;
    public event System.Action<string, string> OnWrongCapture;
    public event System.Action<float> OnGameCompleted;
    /// <summary>Fires when a new round's target is set and objects are ready.</summary>
    public event System.Action<int, string, string> OnRoundReady; // round, color, shape

    public GameState CurrentState => m_State;
    public IReadOnlyList<(string shape, string color, Color colorValue)> Objectives => m_Objectives;
    public IReadOnlyList<GameObject> SpawnedObjects => m_SpawnedObjects;
    public int CurrentObjectiveIndex => m_CurrentRound;
    public int FoundCount => m_CurrentRound;
    public int TotalRounds => k_TotalRounds;
    public float GameStartTime => m_GameStartTime;

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

        // Hide OK button from coaching UI
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name == "Text Poke Button OK")
            {
                go.SetActive(false);
                Debug.Log($"{k_Tag} Hid OK button");
            }
        }
    }

    GameState m_State = GameState.Idle;
    ObjectSpawner m_Spawner;
    ShapeObjectFactory m_Factory;
    FindObjectUI m_UI;
    GazeHighlightManager m_GazeDwell;

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
        Physics.IgnoreLayerCollision(8, 8, true);
        m_Spawner = GetComponent<ObjectSpawner>();
        if (m_Spawner == null) return;
        if (m_UI == null) { m_UI = gameObject.AddComponent<FindObjectUI>(); m_UI.Initialize(); }
        m_Spawner.objectSpawned += OnObjectSpawned;
    }

    void OnDisable()
    {
        if (m_Spawner != null) m_Spawner.objectSpawned -= OnObjectSpawned;
        if (m_GazeDwell != null) m_GazeDwell.OnObjectCaptured -= OnObjectCaptured;
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

        m_State = GameState.Playing;
        m_GameStartTime = Time.time;
        m_CurrentRound = 0;
        m_UI.StartTimer();

        if (!m_ShelvesBuilt)
        {
            var (shelfObjs, _) = ShelfSpawner.CreateShelvesAndSpawnPoints(
                m_SpawnCenter, m_PlaneSize, m_PlaneRight, m_PlaneForward, k_ObjectsPerRound);
            m_ShelfObjects.AddRange(shelfObjs);
            m_ShelvesBuilt = true;
            m_UI.PositionStaticLeft(m_SpawnCenter, ShelfSpawner.ObjectFacingRotation);
        }

        OnGameStarted?.Invoke();
        StartCoroutine(DoSpawnRound());
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
        bool gazeAware = ChallengeSet.IsGazeAware(m_CurrentRound, GetParticipantNumber());
        var hints = GetComponent<HintGenerator>();
        if (hints != null) hints.gazeAwareTips = gazeAware;
        Debug.Log($"{k_Tag} Round {m_CurrentRound}: gazeAware={gazeAware}");
        Debug.Log($"{k_Tag} Round {m_CurrentRound + 1}: block={round.blockIndex}, condition={ChallengeSet.GetConditionLabel(m_CurrentRound, GetParticipantNumber())}");

        m_Objectives.Clear();
        m_Objectives.Add(m_CurrentTarget);

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
            if (info != null) { info.shelfLevel = sp.row; info.shelfColumn = sp.col; }

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
        string condition = ChallengeSet.GetConditionLabel(m_CurrentRound, GetParticipantNumber());
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
                float elapsed = m_UI.StopTimer();
                m_UI.ShowCompletion(k_TotalRounds, elapsed);
                OnGameCompleted?.Invoke(elapsed);
                StartCoroutine(ResetAfterDelay());
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

        // Destroy all objects
        foreach (var obj in m_SpawnedObjects)
            if (obj != null) Destroy(obj);
        m_SpawnedObjects.Clear();
        m_SpawnPoints.Clear();

        // Show fixation cross
        m_UI.ShowFixationCross();

        // Pause
        yield return new WaitForSeconds(k_TransitionPause);

        // Hide cross
        m_UI.HideFixationCross();

        // Wait for destroys to fully process
        yield return null;
        yield return null;

        if (m_GazeDwell != null) m_GazeDwell.ResetDwell();

        // Spawn next round
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
}
