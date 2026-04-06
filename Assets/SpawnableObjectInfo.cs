using UnityEngine;

/// <summary>
/// Stores metadata about a spawned shape for gaze data logging.
/// Added at runtime by ShapeObjectFactory.
/// </summary>
public class SpawnableObjectInfo : MonoBehaviour
{
    public string shapeName;   // "Sphere", "Cube", "Pyramid", "Cylinder", "Star"
    public string colorName;   // "Red", "Blue", "Yellow", "Purple"
    public int shelfLevel;     // 0-4: bottom to top shelf row
    public int shelfColumn;    // 0=left bookcase, 1=right bookcase

    /// <summary>Returns a display name like "Red_Sphere" for logging.</summary>
    public string DisplayName => $"{colorName}_{shapeName}";

    /// <summary>Human-readable shelf row name.</summary>
    public string LevelName => shelfLevel switch
    {
        0 => "bottom shelf",
        1 => "second shelf",
        2 => "third shelf",
        3 => "middle shelf",
        4 => "fifth shelf",
        5 => "sixth shelf",
        6 => "top shelf",
        _ => $"shelf {shelfLevel}"
    };

    /// <summary>Human-readable column name.</summary>
    public string ColumnName => shelfColumn switch
    {
        0 => "left bookcase",
        1 => "right bookcase",
        _ => $"column {shelfColumn}"
    };
}
