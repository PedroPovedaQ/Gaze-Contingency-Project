using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Builds a dynamic text snapshot of the game state for the LLM.
/// Includes current objective, progress, elapsed time, spatial descriptions
/// of remaining objects relative to the player, and gaze information.
/// </summary>
public class AgentContext : MonoBehaviour
{
    const string k_Tag = "[AgentContext]";

    FindObjectGameManager m_GameManager;
    FindObjectUI m_UI;
    GazeCoverageTracker m_CoverageTracker;
    XRBaseInputInteractor m_GazeInteractor;
    readonly StringBuilder m_Builder = new();

    public void Initialize(FindObjectGameManager gameManager, FindObjectUI ui, GazeCoverageTracker coverageTracker = null)
    {
        m_GameManager = gameManager;
        m_UI = ui;
        m_CoverageTracker = coverageTracker;
    }

    void Start()
    {
        // Find the gaze interactor (same one used by GazeHighlightManager)
        var highlighter = FindObjectOfType<GazeHighlightManager>();
        if (highlighter != null)
        {
            m_GazeInteractor = highlighter.GetComponent<XRBaseInputInteractor>();
            Debug.Log($"{k_Tag} Found gaze interactor on {highlighter.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"{k_Tag} No GazeHighlightManager found, gaze context unavailable");
        }
    }

    /// <summary>
    /// Returns a structured text prompt describing the current game state
    /// for the LLM to generate contextual hints.
    /// </summary>
    public string BuildContextPrompt()
    {
        if (m_GameManager == null || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
            return "";

        var cam = Camera.main;
        if (cam == null) return "";

        m_Builder.Clear();

        // Current objective
        var objectives = m_GameManager.Objectives;
        int idx = m_GameManager.CurrentObjectiveIndex;
        if (idx < objectives.Count)
        {
            var target = objectives[idx];
            m_Builder.AppendLine($"CURRENT TARGET: {target.color} {target.shape}");
        }

        // Progress
        m_Builder.AppendLine($"PROGRESS: {m_GameManager.FoundCount}/{objectives.Count} found (searching for #{idx + 1})");

        // Elapsed time
        if (m_UI != null && m_UI.IsTimerRunning)
        {
            float elapsed = Time.time - m_UI.TimerStartTime;
            m_Builder.AppendLine($"TIME SEARCHING: {elapsed:F0} seconds");
        }

        // Gaze information
        string gazedObjectDesc = GetGazedObjectDescription();
        if (!string.IsNullOrEmpty(gazedObjectDesc))
        {
            m_Builder.AppendLine($"PLAYER IS LOOKING AT: {gazedObjectDesc}");
        }
        else
        {
            m_Builder.AppendLine("PLAYER IS LOOKING AT: nothing / empty space");
        }

        // Gaze direction relative to table
        if (cam != null)
        {
            Vector3 gazeDir = cam.transform.forward;
            string gazeDescription = DescribeGazeDirection(gazeDir);
            m_Builder.AppendLine($"GAZE DIRECTION: {gazeDescription}");
        }

        // Remaining objects with spatial descriptions
        m_Builder.AppendLine("\nOBJECTS ON DISPLAY:");
        var spawnedObjects = m_GameManager.SpawnedObjects;
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            var obj = spawnedObjects[i];
            if (obj == null || !obj.activeSelf) continue;

            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info == null) continue;

            string spatial = DescribeObjectRelativeToPlayer(obj.transform.position, cam.transform);
            bool isTarget = idx < objectives.Count &&
                            info.shapeName == objectives[idx].shape &&
                            info.colorName == objectives[idx].color;

            string marker = isTarget ? " [TARGET]" : "";
            m_Builder.AppendLine($"  - {info.colorName} {info.shapeName}: {spatial}, on {info.LevelName}{marker}");
        }

        // Gaze coverage data (if tracker available)
        if (m_CoverageTracker != null)
        {
            // Gaze history
            var recent = m_CoverageTracker.GetRecentFixations(8);
            if (recent.Count > 0)
            {
                m_Builder.Append("\nGAZE HISTORY (recent): ");
                for (int i = 0; i < recent.Count; i++)
                {
                    if (i > 0) m_Builder.Append(" → ");
                    m_Builder.Append($"{recent[i].objectId} ({recent[i].duration:F1}s)");
                }
                m_Builder.AppendLine();
            }

            // Objects examined
            var examined = m_CoverageTracker.GetExaminedObjects();
            if (examined.Count > 0)
            {
                m_Builder.Append("OBJECTS EXAMINED: ");
                int cap = Mathf.Min(examined.Count, 10);
                for (int i = 0; i < cap; i++)
                {
                    if (i > 0) m_Builder.Append(", ");
                    m_Builder.Append($"{examined[i].name} ({examined[i].duration:F1}s)");
                }
                if (examined.Count > 10)
                    m_Builder.Append($" [and {examined.Count - 10} more]");
                m_Builder.AppendLine();
            }

            // Objects not yet examined
            var unexamined = m_CoverageTracker.GetUnexaminedObjects();
            if (unexamined.Count > 0)
            {
                m_Builder.Append("OBJECTS NOT YET EXAMINED: ");
                int cap = Mathf.Min(unexamined.Count, 5);
                for (int i = 0; i < cap; i++)
                {
                    if (i > 0) m_Builder.Append(", ");
                    m_Builder.Append(unexamined[i]);
                }
                if (unexamined.Count > 5)
                    m_Builder.Append($" [and {unexamined.Count - 5} more]");
                m_Builder.AppendLine();
            }

            // Zone coverage
            m_Builder.Append("ZONE COVERAGE: ");
            string[] zoneNames = { "bottom shelf", "second shelf", "middle shelf", "fourth shelf", "top shelf" };
            for (int z = 0; z < zoneNames.Length; z++)
            {
                if (z > 0) m_Builder.Append(", ");
                float total = m_CoverageTracker.GetZoneTotalFixation(z);
                string label = total < 1f ? "NOT SCANNED" :
                               total < 5f ? "briefly glanced" :
                               total < 15f ? "partially scanned" : "well-scanned";
                m_Builder.Append($"{zoneNames[z]}={label}");
            }
            m_Builder.AppendLine();
        }

        return m_Builder.ToString();
    }

    /// <summary>
    /// Returns a description of what the player is currently gazing at,
    /// or null if nothing.
    /// </summary>
    public string GetGazedObjectDescription()
    {
        if (m_GazeInteractor == null) return null;

        var hovered = m_GazeInteractor.interactablesHovered;
        if (hovered.Count == 0) return null;

        var interactable = hovered[0];
        if (interactable == null) return null;

        var info = interactable.transform.GetComponent<SpawnableObjectInfo>();
        if (info != null)
            return $"{info.colorName} {info.shapeName}";

        return interactable.transform.name;
    }

    /// <summary>
    /// Returns whether the player is currently gazing at the target object.
    /// </summary>
    public bool IsGazingAtTarget()
    {
        if (m_GazeInteractor == null || m_GameManager == null) return false;
        if (m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing) return false;

        var objectives = m_GameManager.Objectives;
        int idx = m_GameManager.CurrentObjectiveIndex;
        if (idx >= objectives.Count) return false;

        var target = objectives[idx];
        var hovered = m_GazeInteractor.interactablesHovered;

        for (int i = 0; i < hovered.Count; i++)
        {
            var interactable = hovered[i];
            if (interactable == null) continue;

            var info = interactable.transform.GetComponent<SpawnableObjectInfo>();
            if (info != null && info.shapeName == target.shape && info.colorName == target.color)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a short spatial hint like "to your left" or "ahead and to the right"
    /// describing where the current target is relative to the player. Returns null
    /// if the target can't be found.
    /// </summary>
    public string GetTargetDirectionHint()
    {
        if (m_GameManager == null || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
            return null;
        var cam = Camera.main;
        if (cam == null) return null;

        var objectives = m_GameManager.Objectives;
        int idx = m_GameManager.CurrentObjectiveIndex;
        if (idx >= objectives.Count) return null;
        var target = objectives[idx];

        // Find the target object
        foreach (var obj in m_GameManager.SpawnedObjects)
        {
            if (obj == null || !obj.activeSelf) continue;
            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info != null && info.shapeName == target.shape && info.colorName == target.color)
            {
                Vector3 local = cam.transform.InverseTransformPoint(obj.transform.position);

                // Horizontal direction
                string horiz = null;
                if (local.x > 0.15f) horiz = "to your right";
                else if (local.x < -0.15f) horiz = "to your left";

                // Vertical / shelf
                string vert = null;
                if (local.y > 0.15f) vert = "higher up";
                else if (local.y < -0.15f) vert = "lower down";

                if (horiz != null && vert != null) return $"{horiz} and {vert}";
                if (horiz != null) return horiz;
                if (vert != null) return vert;
                return "right in front of you";
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the shelf level name of the current target, or null.
    /// </summary>
    public string GetTargetShelfName()
    {
        if (m_GameManager == null || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
            return null;
        var objectives = m_GameManager.Objectives;
        int idx = m_GameManager.CurrentObjectiveIndex;
        if (idx >= objectives.Count) return null;
        var target = objectives[idx];

        foreach (var obj in m_GameManager.SpawnedObjects)
        {
            if (obj == null || !obj.activeSelf) continue;
            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info != null && info.shapeName == target.shape && info.colorName == target.color)
                return info.LevelName;
        }
        return null;
    }

    string DescribeObjectRelativeToPlayer(Vector3 worldPos, Transform playerTransform)
    {
        // Convert to player-local space
        Vector3 local = playerTransform.InverseTransformPoint(worldPos);

        // Distance
        float distance = new Vector3(local.x, 0, local.z).magnitude;

        // Direction components
        var parts = new List<string>();

        // Left/right
        if (Mathf.Abs(local.x) > 0.1f)
            parts.Add(local.x > 0 ? $"{Mathf.Abs(local.x):F1}m to your right" : $"{Mathf.Abs(local.x):F1}m to your left");

        // Forward/behind
        if (Mathf.Abs(local.z) > 0.1f)
            parts.Add(local.z > 0 ? $"{Mathf.Abs(local.z):F1}m ahead" : $"{Mathf.Abs(local.z):F1}m behind you");

        // Vertical
        if (local.y > 0.15f)
            parts.Add("above eye level");
        else if (local.y < -0.15f)
            parts.Add("below eye level");

        if (parts.Count == 0)
            return "directly in front of you";

        return string.Join(" and ", parts);
    }

    string DescribeGazeDirection(Vector3 gazeForward)
    {
        var parts = new List<string>();

        // Horizontal: left/right bias
        if (gazeForward.x > 0.3f)
            parts.Add("looking right");
        else if (gazeForward.x < -0.3f)
            parts.Add("looking left");

        // Vertical: up/down
        if (gazeForward.y > 0.2f)
            parts.Add("looking up");
        else if (gazeForward.y < -0.2f)
            parts.Add("looking down at the table");

        // Forward
        if (gazeForward.z > 0.5f)
            parts.Add("looking forward");

        if (parts.Count == 0)
            return "neutral";

        return string.Join(", ", parts);
    }
}
