using UnityEngine;

/// <summary>
/// Stores metadata about a spawned shape for gaze data logging.
/// Added at runtime by ShapeObjectFactory.
/// </summary>
public class SpawnableObjectInfo : MonoBehaviour
{
    public string shapeName;   // "Sphere", "Cube", "Pyramid", "Cylinder", "Star"
    public string colorName;   // "Red", "Blue", "Yellow", "Purple"
    public int shelfLevel;     // 0=table, 1=lower shelf, 2=upper shelf

    /// <summary>
    /// Returns a display name like "Red_Sphere" for logging.
    /// </summary>
    public string DisplayName => $"{colorName}_{shapeName}";

    /// <summary>
    /// Human-readable shelf level name for LLM context.
    /// </summary>
    public string LevelName => shelfLevel switch
    {
        0 => "table",
        1 => "lower shelf",
        2 => "upper shelf",
        _ => $"level {shelfLevel}"
    };
}
