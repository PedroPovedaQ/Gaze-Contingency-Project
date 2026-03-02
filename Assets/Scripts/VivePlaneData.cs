using UnityEngine;
using static VIVE.OpenXR.PlaneDetection.VivePlaneDetection;

/// <summary>
/// Stores plane metadata for VIVE-detected planes, serving as a lightweight
/// replacement for ARPlane on GameObjects created by VivePlaneProvider.
/// </summary>
public class VivePlaneData : MonoBehaviour
{
    public ulong PlaneId { get; set; }
    public Vector2 Size { get; set; }
    public XrPlaneDetectorOrientationEXT Orientation { get; set; }
    public XrPlaneDetectorSemanticTypeEXT SemanticType { get; set; }

    /// <summary>
    /// World-space center of the plane (dynamic from transform).
    /// </summary>
    public Vector3 Center => transform.position;

    /// <summary>
    /// World-space normal of the plane (dynamic from transform).
    /// VIVE plane meshes lie in the XY plane with normal along local +Z.
    /// </summary>
    public Vector3 Normal => transform.forward;

    /// <summary>
    /// Returns true if this is a horizontal upward surface (floor, table/platform).
    /// </summary>
    public bool IsHorizontalUp =>
        Orientation == XrPlaneDetectorOrientationEXT.HORIZONTAL_UPWARD_EXT;
}
