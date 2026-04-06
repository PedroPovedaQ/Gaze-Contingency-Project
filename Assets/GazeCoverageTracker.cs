using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Tracks per-object gaze fixation time, gaze history, zone coverage,
/// and classifies gaze behavior patterns for adaptive hint timing.
/// Auto-attaches to ObjectSpawner.
/// </summary>
public class GazeCoverageTracker : MonoBehaviour
{
    const string k_Tag = "[GazeCoverage]";
    const int k_RingCapacity = 20;
    const float k_MinFixationDuration = 0.1f; // ignore sub-100ms glances
    const int k_ClassifyWindow = 10; // last N events for behavior classification
    const int k_ZoneCount = 7;

    public enum GazeBehavior { Systematic, Normal, Erratic, Stuck }

    public struct FixationEvent
    {
        public string objectId;
        public float startTime;
        public float duration;
        public int shelfLevel;
    }

    struct ObjectFixationRecord
    {
        public float totalFixationTime;
        public int visitCount;
        public float lastVisitTime;
        public int shelfLevel;
    }

    FindObjectGameManager m_GameManager;
    XRBaseInputInteractor m_GazeInteractor;

    readonly Dictionary<string, ObjectFixationRecord> m_Records = new();
    readonly FixationEvent[] m_RingBuffer = new FixationEvent[k_RingCapacity];
    int m_RingHead;
    int m_RingCount;

    readonly float[] m_ZoneLastScanTime = new float[k_ZoneCount];
    readonly float[] m_ZoneTotalFixation = new float[k_ZoneCount];

    // Current fixation state
    string m_CurrentObject;
    int m_CurrentLevel;
    float m_FixationStart;

    public void Initialize(FindObjectGameManager gameManager)
    {
        m_GameManager = gameManager;
        Debug.Log($"{k_Tag} Initialized");
    }

    void Start()
    {
        var highlighter = FindObjectOfType<GazeHighlightManager>();
        if (highlighter != null)
        {
            m_GazeInteractor = highlighter.GetComponent<XRBaseInputInteractor>();
            Debug.Log($"{k_Tag} Found gaze interactor on {highlighter.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"{k_Tag} No GazeHighlightManager found, coverage tracking unavailable");
        }
    }

    void Update()
    {
        if (m_GazeInteractor == null || m_GameManager == null) return;
        if (m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing) return;

        // Determine what the player is currently looking at
        string hoveredId = null;
        int hoveredLevel = -1;
        var hovered = m_GazeInteractor.interactablesHovered;
        if (hovered.Count > 0 && hovered[0] != null)
        {
            var info = hovered[0].transform.GetComponent<SpawnableObjectInfo>();
            if (info != null)
            {
                hoveredId = info.DisplayName;
                hoveredLevel = info.shelfLevel;
            }
        }

        // Did the gaze target change?
        if (hoveredId != m_CurrentObject)
        {
            FinalizeCurrentFixation();

            if (hoveredId != null)
            {
                m_CurrentObject = hoveredId;
                m_CurrentLevel = hoveredLevel;
                m_FixationStart = Time.time;
            }
            else
            {
                m_CurrentObject = null;
                m_CurrentLevel = -1;
                m_FixationStart = 0f;
            }
        }

        // Update zone tracking for current gaze
        if (hoveredLevel >= 0 && hoveredLevel < k_ZoneCount)
        {
            m_ZoneLastScanTime[hoveredLevel] = Time.time;
            m_ZoneTotalFixation[hoveredLevel] += Time.deltaTime;
        }
    }

    void FinalizeCurrentFixation()
    {
        if (m_CurrentObject == null || m_FixationStart <= 0f) return;

        float duration = Time.time - m_FixationStart;
        if (duration < k_MinFixationDuration) return;

        // Update per-object record
        if (!m_Records.TryGetValue(m_CurrentObject, out var record))
        {
            record = new ObjectFixationRecord { shelfLevel = m_CurrentLevel };
        }
        record.totalFixationTime += duration;
        record.visitCount++;
        record.lastVisitTime = Time.time;
        m_Records[m_CurrentObject] = record;

        // Push to ring buffer
        m_RingBuffer[m_RingHead] = new FixationEvent
        {
            objectId = m_CurrentObject,
            startTime = m_FixationStart,
            duration = duration,
            shelfLevel = m_CurrentLevel
        };
        m_RingHead = (m_RingHead + 1) % k_RingCapacity;
        if (m_RingCount < k_RingCapacity) m_RingCount++;
    }

    // --- Public API ---

    /// <summary>
    /// Classifies current gaze behavior based on recent fixation patterns.
    /// </summary>
    public GazeBehavior ClassifyBehavior()
    {
        if (m_RingCount < 3) return GazeBehavior.Normal;

        int window = Mathf.Min(m_RingCount, k_ClassifyWindow);
        float totalDuration = 0f;
        var uniqueObjects = new HashSet<string>();
        int zoneSwitches = 0;
        var zoneCounts = new int[k_ZoneCount];
        int prevZone = -1;

        for (int i = 0; i < window; i++)
        {
            int idx = ((m_RingHead - 1 - i) % k_RingCapacity + k_RingCapacity) % k_RingCapacity;
            var evt = m_RingBuffer[idx];

            totalDuration += evt.duration;
            uniqueObjects.Add(evt.objectId);

            if (evt.shelfLevel >= 0 && evt.shelfLevel < k_ZoneCount)
                zoneCounts[evt.shelfLevel]++;

            if (prevZone >= 0 && evt.shelfLevel != prevZone)
                zoneSwitches++;
            prevZone = evt.shelfLevel;
        }

        float avgDuration = totalDuration / window;
        float uniqueRatio = (float)uniqueObjects.Count / window;

        // Find dominant zone
        int maxZoneCount = 0;
        for (int i = 0; i < k_ZoneCount; i++)
        {
            if (zoneCounts[i] > maxZoneCount)
                maxZoneCount = zoneCounts[i];
        }
        float dominantZonePct = (float)maxZoneCount / window;

        float searchTime = m_GameManager != null
            ? Time.time - m_GameManager.GameStartTime
            : 0f;

        // Decision tree
        if (avgDuration < 0.6f && zoneSwitches > 4)
            return GazeBehavior.Erratic;

        if (dominantZonePct > 0.7f && searchTime > 15f)
            return GazeBehavior.Stuck;

        if (avgDuration > 1.5f && uniqueRatio > 0.6f)
            return GazeBehavior.Systematic;

        return GazeBehavior.Normal;
    }

    /// <summary>
    /// Returns the most recent fixation events (newest first).
    /// </summary>
    public List<FixationEvent> GetRecentFixations(int count)
    {
        var result = new List<FixationEvent>();
        int n = Mathf.Min(count, m_RingCount);
        for (int i = 0; i < n; i++)
        {
            int idx = ((m_RingHead - 1 - i) % k_RingCapacity + k_RingCapacity) % k_RingCapacity;
            result.Add(m_RingBuffer[idx]);
        }
        result.Reverse(); // oldest first for natural reading order
        return result;
    }

    /// <summary>
    /// Returns display names of objects the player has examined for at least minDuration seconds total.
    /// </summary>
    public List<(string name, float duration)> GetExaminedObjects(float minDuration = 0.3f)
    {
        var result = new List<(string name, float duration)>();
        foreach (var kvp in m_Records)
        {
            if (kvp.Value.totalFixationTime >= minDuration)
                result.Add((kvp.Key, kvp.Value.totalFixationTime));
        }
        result.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return result;
    }

    /// <summary>
    /// Returns display names of spawned objects the player has NOT examined.
    /// </summary>
    public List<string> GetUnexaminedObjects()
    {
        var result = new List<string>();
        if (m_GameManager == null) return result;

        foreach (var obj in m_GameManager.SpawnedObjects)
        {
            if (obj == null || !obj.activeSelf) continue;
            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info == null) continue;

            string id = info.DisplayName;
            if (!m_Records.TryGetValue(id, out var rec) || rec.totalFixationTime < 0.3f)
                result.Add(id);
        }
        return result;
    }

    /// <summary>
    /// Returns Time.time of when a shelf level was last scanned, or 0 if never.
    /// </summary>
    public float GetZoneLastScanTime(int level)
    {
        if (level < 0 || level >= k_ZoneCount) return 0f;
        return m_ZoneLastScanTime[level];
    }

    /// <summary>
    /// Returns total fixation time for a zone.
    /// </summary>
    public float GetZoneTotalFixation(int level)
    {
        if (level < 0 || level >= k_ZoneCount) return 0f;
        return m_ZoneTotalFixation[level];
    }

    /// <summary>
    /// Returns how many distinct fixations an object has received.
    /// </summary>
    public int GetRevisitCount(string objectId)
    {
        return m_Records.TryGetValue(objectId, out var rec) ? rec.visitCount : 0;
    }

    /// <summary>
    /// Returns the non-target object with the highest revisit count, or null.
    /// </summary>
    public string GetMostRevisitedNonTarget()
    {
        if (m_GameManager == null) return null;
        var objectives = m_GameManager.Objectives;
        int idx = m_GameManager.CurrentObjectiveIndex;
        if (idx >= objectives.Count) return null;

        var target = objectives[idx];
        string targetId = $"{target.color}_{target.shape}";

        string best = null;
        int bestCount = 2; // only return if visited 3+ times
        foreach (var kvp in m_Records)
        {
            if (kvp.Key == targetId) continue;
            if (kvp.Value.visitCount > bestCount)
            {
                bestCount = kvp.Value.visitCount;
                best = kvp.Key;
            }
        }
        return best;
    }

    /// <summary>
    /// Resets all tracking data. Call on game start.
    /// </summary>
    public void Reset()
    {
        m_Records.Clear();
        m_RingHead = 0;
        m_RingCount = 0;
        m_CurrentObject = null;
        m_CurrentLevel = -1;
        m_FixationStart = 0f;

        for (int i = 0; i < k_ZoneCount; i++)
        {
            m_ZoneLastScanTime[i] = 0f;
            m_ZoneTotalFixation[i] = 0f;
        }

        Debug.Log($"{k_Tag} Reset all tracking data");
    }
}
