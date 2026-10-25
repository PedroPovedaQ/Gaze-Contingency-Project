using System;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;

public static class NativeGazeTimestampSetup
{
    [MenuItem("Tools/Codex/Enable Native Gaze Timestamp Diagnostics")]
    public static void Enable()
    {
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var feature = settings?.GetFeature<NativeGazeTimestampFeature>();
        if (feature == null) throw new InvalidOperationException("Refresh OpenXR features after importing native gaze diagnostics.");
        var eye = settings.GetFeature<UnityEngine.XR.OpenXR.Features.Interactions.EyeGazeInteraction>();
        if (eye == null || !eye.enabled) throw new InvalidOperationException("Enable the existing Eye Gaze Interaction feature first.");
        feature.enabled = true;
        EditorUtility.SetDirty(feature); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        Debug.Log("[NativeGaze] Android diagnostic feature enabled. Sensor 120 Hz remains unverified.");
    }
}
