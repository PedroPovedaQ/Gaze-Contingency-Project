using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class GazeWallFilterChecks
{
    [MenuItem("Tools/Codex/Check Gaze Wall Filtering")]
    public static void Run()
    {
        var gaze = new GameObject("GazeFilterTest");
        gaze.SetActive(false);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var ray = gaze.AddComponent<XRGazeInteractor>();
            var selector = gaze.AddComponent<GazeHighlightManager>();
            selector.ConfigureThroughWallSearch();
            if (ray.raycastMask.value != (1 << 8) || ray.enableUIInteraction || !ray.hitClosestOnly)
                throw new Exception("Search ray filter configuration is incorrect.");
            Vector3 origin = new Vector3(1000, 1000, 1000);
            wall.layer = 7;
            wall.transform.position = origin + Vector3.forward;
            target.layer = 8;
            target.transform.position = origin + Vector3.forward * 2;
            Physics.SyncTransforms();
            if (!Physics.Raycast(origin, Vector3.forward, out var blocked, 3f, ~0) || blocked.collider.gameObject != wall)
                throw new Exception("Fixture must reproduce a wall blocking the unfiltered ray.");
            if (!Physics.Raycast(origin, Vector3.forward, out var filtered, 3f, ray.raycastMask) || filtered.collider.gameObject != target)
                throw new Exception("Filtered gaze must reach the target behind the wall.");
            Debug.Log("[GazeWallFilterChecks] PASS: real physics wall blocks default ray; configured gaze reaches target; gaze UI blocking disabled.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gaze);
            UnityEngine.Object.DestroyImmediate(wall);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
