using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

// Reuses EyeGazeInteraction's enabled extension; does not register a duplicate extension.
#if UNITY_EDITOR
[UnityEditor.XR.OpenXR.Features.OpenXRFeature(
    UiName = "Gaze runtime timestamp diagnostics",
    BuildTargetGroups = new[] { UnityEditor.BuildTargetGroup.Android },
    Company = "Gaze Contingency Project", Version = "1.0.0",
    Desc = "Record OpenXR eye-pose times without replacing application gaze. Not a sensor-rate certification.",
    FeatureId = FeatureId)]
#endif
public sealed class NativeGazeTimestampFeature : OpenXRFeature
{
    public const string FeatureId = "research.gaze.runtime-timestamps";
    public static bool NativeLoaded { get; private set; }
    public static bool Available { get; private set; }
    public static string Status { get; private set; } = "Native timestamp feature has not initialized.";
    bool m_Hooked;
    protected override IntPtr HookGetInstanceProcAddr(IntPtr function)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { var hook = NativeGazeInterop.ng_hook(function); m_Hooked = hook != IntPtr.Zero; NativeLoaded = m_Hooked; return m_Hooked ? hook : function; }
        catch (Exception e) { Status = "Native timestamp adapter unavailable: " + e.GetType().Name; Debug.LogWarning(Status); }
#endif
        return function;
    }
    protected override bool OnInstanceCreate(ulong instance)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
          if (m_Hooked)
          {
            bool extension = OpenXRRuntime.IsExtensionEnabled("XR_EXT_eye_gaze_interaction");
            Available = NativeGazeInterop.ng_begin(instance, extension ? 1 : 0) == 1;
            Status = Available ? "Native eye-pose timestamp diagnostics active; sensor capture semantics unverified."
                : "Runtime eye-gaze extension or native path unavailable.";
            Debug.Log("[NativeGaze] " + Status);
          }
        }
        catch (Exception e) { Available=false; Status="Native gaze initialization failed: "+e.GetType().Name; Debug.LogWarning(Status); }
#endif
        // Optional diagnostics must not prevent the rest of OpenXR from initializing.
        return true;
    }
    protected override void OnInstanceDestroy(ulong instance)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (m_Hooked) NativeGazeInterop.ng_end();
#endif
        Available = false; Status = "OpenXR instance ended.";
    }
}

// ABI matches native/gaze_timestamp/gaze_timestamp.cpp, tested at runtime before draining.
// XrTime values are signed nanoseconds in the OpenXR clock; never Unity realtime.
[Serializable, StructLayout(LayoutKind.Sequential)]
public struct NativeGazeRecord
{
    public ulong sequence, epoch, session, space, baseSpace, locationFlags;
    public long requestedTime, runtimePoseTime, receiptMonotonicNs;
    public int result, timestampChained;
    public float qx,qy,qz,qw,px,py,pz;
    public float offsetQx,offsetQy,offsetQz,offsetQw,offsetX,offsetY,offsetZ;
}
[Serializable, StructLayout(LayoutKind.Sequential)]
public struct NativeGazeStatus
{
    public ulong epoch, records, dropped, spaces, eyeActions, locateCalls;
    public int enabled, recordBytes;
}
internal static class NativeGazeInterop
{
    const string Library = "gaze_timestamp";
    [DllImport(Library)] internal static extern IntPtr ng_hook(IntPtr next);
    [DllImport(Library)] internal static extern int ng_begin(ulong instance, int extensionEnabled);
    [DllImport(Library)] internal static extern void ng_end();
    [DllImport(Library)] internal static extern void ng_status(out NativeGazeStatus status);
    [DllImport(Library)] internal static extern int ng_drain([Out] NativeGazeRecord[] output, int maximum);
    [DllImport(Library)] internal static extern long ng_monotonic_ns();
    [DllImport(Library)] internal static extern int ng_take_htc([Out] HtcQueryStamp[] output, int count);
}
