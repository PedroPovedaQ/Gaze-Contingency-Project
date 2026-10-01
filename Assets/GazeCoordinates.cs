using UnityEngine;

/// <summary>Unity's EXT pose is already left handed; native HTC poses need vendor conversion once.</summary>
public static class GazeCoordinates
{
    public static Ray InputPoseToWorld(Vector3 localOrigin, Quaternion localRotation, Transform parent, bool mirrorOriginX)
    {
        if (mirrorOriginX) localOrigin.x = -localOrigin.x;
        return new Ray(parent.TransformPoint(localOrigin), parent.TransformDirection(localRotation * Vector3.forward).normalized);
    }
    public static Ray NativePoseToWorld(VIVE.OpenXR.XrPosef pose, Transform parent) =>
        InputPoseToWorld(VIVE.OpenXR.OpenXRHelper.ToUnityVector(pose.position),
            VIVE.OpenXR.OpenXRHelper.ToUnityQuaternion(pose.orientation),parent,false);
}
