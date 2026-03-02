using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Orchestrates the "Find the Object" game mode.
/// Auto-attaches to ObjectSpawner. Intercepts the first tap-spawn to start a game:
/// spawns 9 distinct shape-color combos spread across the detected plane,
/// then cycles through objectives as the player grabs each one.
/// </summary>
public class FindObjectGameManager : MonoBehaviour
{
    const string k_Tag = "[FindObjectGame]";
    const int k_ObjectCount = 9;
    const float k_Margin = 0.05f;     // 5 cm inset from plane edges
    const float k_Jitter = 0.02f;     // ±2 cm random jitter
    const float k_SpawnHeightOffset = 0.05f; // 5 cm above surface
    const float k_ResetDelay = 5f;

    enum GameState { Idle, Playing, Completed }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var spawner = FindObjectOfType<ObjectSpawner>();
        if (spawner != null && spawner.GetComponent<FindObjectGameManager>() == null)
        {
            spawner.gameObject.AddComponent<FindObjectGameManager>();
            Debug.Log($"{k_Tag} Auto-attached to {spawner.gameObject.name}");
        }
    }

    GameState m_State = GameState.Idle;
    ObjectSpawner m_Spawner;
    ShapeObjectFactory m_Factory;
    FindObjectUI m_UI;

    readonly List<(string shape, string color, Color colorValue)> m_Objectives = new();
    readonly List<GameObject> m_SpawnedObjects = new();
    int m_CurrentObjectiveIndex;
    int m_FoundCount;
    int m_ExpectedSpawnCount;

    void OnEnable()
    {
        m_Spawner = GetComponent<ObjectSpawner>();
        if (m_Spawner == null)
        {
            Debug.LogWarning($"{k_Tag} No ObjectSpawner found on {gameObject.name}");
            return;
        }

        // Create UI
        if (m_UI == null)
        {
            m_UI = gameObject.AddComponent<FindObjectUI>();
            m_UI.Initialize();
        }

        m_Spawner.objectSpawned += OnObjectSpawned;
        Debug.Log($"{k_Tag} Initialized, waiting for first spawn to start game");
    }

    void OnDisable()
    {
        if (m_Spawner != null)
            m_Spawner.objectSpawned -= OnObjectSpawned;
    }

    void OnObjectSpawned(GameObject obj)
    {
        switch (m_State)
        {
            case GameState.Idle:
                // First spawn triggers a new game — capture position, destroy trigger object
                StartGame(obj);
                break;

            case GameState.Playing:
                // During game: if this is one of our batch spawns, track it; otherwise block
                if (m_ExpectedSpawnCount > 0)
                {
                    m_ExpectedSpawnCount--;
                    // objectFullyConfigured will fire after ShapeObjectFactory finishes
                }
                else
                {
                    // Stray spawn during gameplay — destroy it
                    Debug.Log($"{k_Tag} Blocking stray spawn during game");
                    Destroy(obj);
                }
                break;

            case GameState.Completed:
                // Block spawns during completion screen
                Debug.Log($"{k_Tag} Blocking spawn during completion");
                Destroy(obj);
                break;
        }
    }

    void StartGame(GameObject triggerObj)
    {
        // Lazily resolve factory — may not have been attached yet during OnEnable
        if (m_Factory == null)
            m_Factory = GetComponent<ShapeObjectFactory>();
        if (m_Factory == null)
        {
            Debug.LogError($"{k_Tag} No ShapeObjectFactory found, cannot start game");
            return;
        }

        Debug.Log($"{k_Tag} Starting game from spawn at {triggerObj.transform.position}");

        // Find the nearest horizontal plane for spawn distribution
        Vector3 spawnCenter = triggerObj.transform.position;
        Vector2 planeSize = new Vector2(0.5f, 0.5f); // fallback
        Vector3 planeNormal = Vector3.up;
        Vector3 planeRight = Vector3.right;
        Vector3 planeForward = Vector3.forward;

        var planes = FindObjectsOfType<VivePlaneData>();
        VivePlaneData bestPlane = null;
        float bestDist = float.MaxValue;
        foreach (var p in planes)
        {
            if (!p.IsHorizontalUp) continue;
            float dist = Vector3.Distance(spawnCenter, p.Center);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestPlane = p;
            }
        }

        if (bestPlane != null)
        {
            spawnCenter = bestPlane.Center;
            planeSize = bestPlane.Size;
            planeNormal = bestPlane.Normal;
            // VivePlaneData: mesh in local XY, normal = local +Z
            planeRight = bestPlane.transform.right;
            planeForward = bestPlane.transform.up; // local Y = forward on horizontal plane
            Debug.Log($"{k_Tag} Using plane: size={planeSize}, center={spawnCenter}");
        }
        else
        {
            Debug.LogWarning($"{k_Tag} No horizontal plane found, using fallback area");
        }

        // Destroy the trigger object — it was just used to start the game
        Destroy(triggerObj);

        // Pick 9 distinct combos from 3 shapes × 4 colors = 12 possible
        var allCombos = new List<(string shape, string color, Color colorValue)>();
        string[] shapes = m_Factory.ShapeNames;
        var colors = ShapeObjectFactory.Colors;

        if (shapes == null || shapes.Length == 0)
        {
            Debug.LogError($"{k_Tag} No shapes available from factory");
            return;
        }

        foreach (var s in shapes)
            foreach (var c in colors)
                allCombos.Add((s, c.name, c.color));

        // Fisher-Yates shuffle
        for (int i = allCombos.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (allCombos[i], allCombos[j]) = (allCombos[j], allCombos[i]);
        }

        // Take first 9
        m_Objectives.Clear();
        for (int i = 0; i < Mathf.Min(k_ObjectCount, allCombos.Count); i++)
            m_Objectives.Add(allCombos[i]);

        // Shuffle objectives again for the find order (different from spawn layout)
        var findOrder = new List<(string shape, string color, Color colorValue)>(m_Objectives);
        for (int i = findOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (findOrder[i], findOrder[j]) = (findOrder[j], findOrder[i]);
        }
        m_Objectives.Clear();
        m_Objectives.AddRange(findOrder);

        // Compute 3×3 grid positions on the plane
        float usableW = planeSize.x - k_Margin * 2;
        float usableH = planeSize.y - k_Margin * 2;
        float cellW = usableW / 3f;
        float cellH = usableH / 3f;

        var positions = new List<Vector3>();
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                // Position within the plane's local coordinate system
                float localX = -usableW / 2f + cellW * (col + 0.5f);
                float localY = -usableH / 2f + cellH * (row + 0.5f);

                // Add jitter
                localX += Random.Range(-k_Jitter, k_Jitter);
                localY += Random.Range(-k_Jitter, k_Jitter);

                // Convert to world space using plane axes
                Vector3 worldPos = spawnCenter
                    + planeRight * localX
                    + planeForward * localY;

                // Spawn slightly above the surface
                worldPos.y = spawnCenter.y + k_SpawnHeightOffset;

                positions.Add(worldPos);
            }
        }

        // Shuffle positions so layout doesn't correlate with objective order
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        // Enter playing state before spawning
        m_State = GameState.Playing;
        m_SpawnedObjects.Clear();
        m_CurrentObjectiveIndex = 0;
        m_FoundCount = 0;
        m_ExpectedSpawnCount = m_Objectives.Count;

        // Subscribe to factory event for tracking spawned objects
        m_Factory.objectFullyConfigured += OnObjectFullyConfigured;

        // Enqueue combos and spawn
        for (int i = 0; i < m_Objectives.Count; i++)
        {
            var combo = m_Objectives[i];
            m_Factory.EnqueueCombo(combo.shape, combo.color);
            m_Spawner.TrySpawnObject(positions[i], Vector3.up);
        }

        // Show first objective and start timer
        ShowCurrentObjective();
        m_UI.StartTimer();

        Debug.Log($"{k_Tag} Game started with {m_Objectives.Count} objectives");
    }

    void OnObjectFullyConfigured(GameObject obj)
    {
        if (m_State != GameState.Playing) return;

        m_SpawnedObjects.Add(obj);

        // Wire grab detection
        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnObjectGrabbed);
        }

        Debug.Log($"{k_Tag} Tracked spawned object: {obj.name} ({m_SpawnedObjects.Count}/{m_Objectives.Count})");

        // Unsubscribe after all objects are configured
        if (m_SpawnedObjects.Count >= m_Objectives.Count)
            m_Factory.objectFullyConfigured -= OnObjectFullyConfigured;
    }

    void OnObjectGrabbed(SelectEnterEventArgs args)
    {
        if (m_State != GameState.Playing) return;

        var obj = args.interactableObject.transform.gameObject;
        var info = obj.GetComponent<SpawnableObjectInfo>();
        if (info == null) return;

        var current = m_Objectives[m_CurrentObjectiveIndex];

        if (info.shapeName == current.shape && info.colorName == current.color)
        {
            // Correct!
            Debug.Log($"{k_Tag} Correct! Found {info.DisplayName}");
            m_FoundCount++;

            // Unsubscribe and deactivate
            var grab = obj.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                // Force drop before deactivating
                if (grab.isSelected)
                {
                    grab.enabled = false;
                }
                grab.selectEntered.RemoveListener(OnObjectGrabbed);
            }
            obj.SetActive(false);

            // Advance objective
            m_CurrentObjectiveIndex++;
            if (m_CurrentObjectiveIndex >= m_Objectives.Count)
            {
                // All found!
                m_State = GameState.Completed;
                float elapsed = m_UI.StopTimer();
                m_UI.ShowCompletion(m_Objectives.Count, elapsed);
                Debug.Log($"{k_Tag} Game complete! All {m_Objectives.Count} objects found in {elapsed:F1}s");
                StartCoroutine(ResetAfterDelay());
            }
            else
            {
                ShowCurrentObjective();
            }
        }
        else
        {
            // Wrong object
            Debug.Log($"{k_Tag} Wrong! Grabbed {info.DisplayName}, wanted {current.color}_{current.shape}");
            m_UI.ShowWrongFeedback();
        }
    }

    void ShowCurrentObjective()
    {
        if (m_CurrentObjectiveIndex >= m_Objectives.Count) return;

        var obj = m_Objectives[m_CurrentObjectiveIndex];
        m_UI.ShowObjective(obj.colorValue, $"{obj.color} {obj.shape}", m_FoundCount, m_Objectives.Count);
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(k_ResetDelay);

        Debug.Log($"{k_Tag} Resetting game");

        // Cleanup all spawned objects
        foreach (var obj in m_SpawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        m_SpawnedObjects.Clear();
        m_Objectives.Clear();

        m_UI.Hide();
        m_State = GameState.Idle;

        Debug.Log($"{k_Tag} Game reset, ready for next round");
    }
}
