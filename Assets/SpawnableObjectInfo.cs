using UnityEngine;

/// <summary>
/// Stores metadata about a spawned shape for gaze data logging.
/// Added at runtime by ShapeObjectFactory.
/// </summary>
public class SpawnableObjectInfo : MonoBehaviour
{
    public string shapeName;   // "Sphere", "Cube", "Pyramid"
    public string colorName;   // "Red", "Blue", "Yellow", "Purple"

    /// <summary>
    /// Returns a display name like "Red_Sphere" for logging.
    /// </summary>
    public string DisplayName => $"{colorName}_{shapeName}";
}
