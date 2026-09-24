using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public static class ControllerSelectionChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    [MenuItem("Tools/Codex/Check Controller Selection")]
    public static void Run()
    {
        var consume = typeof(ControllerRaySelector).GetMethod("ConsumePress", BindingFlags.Static | BindingFlags.NonPublic);
        bool armed = false;
        bool Press(bool valid, bool down, bool search)
        {
            object[] args = { valid, down, search, armed };
            bool result = (bool)consume.Invoke(null, args);
            armed = (bool)args[3];
            return result;
        }
        Check(!Press(true, true, true), "Held trigger on trial entry must not confirm.");
        Check(!Press(true, false, true) && Press(true, true, true), "Release then press confirms.");
        Check(!Press(true, true, true), "Holding cannot repeat.");
        Check(!Press(true, false, false) && !Press(true, true, true), "Pause/resume requires fresh release.");
        Press(true, false, true);
        Check(!Press(false, true, true) && !Press(true, true, true), "Tracking recovery while held cannot confirm.");
        Check(!Press(true, false, true) && Press(true, true, true), "Rearmed trigger confirms again.");
        Check(typeof(GazeHighlightManager).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic) == null,
            "Gaze must have no dwell loop.");

        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.SetActive(false);
        try
        {
            var grab = obj.AddComponent<XRGrabInteractable>();
            ControllerRaySelector.PreventGrab(grab);
            ControllerRaySelector.PreventGrab(grab);
            Check(grab.selectFilters.count == 1 && !grab.selectFilters.GetAt(0).Process(null, grab),
                "Search objects must reject XRI grab selection; setup is idempotent.");
            var host = new GameObject("ControllerSelectionTest");
            host.SetActive(false);
            try
            {
                var selector = host.AddComponent<ControllerRaySelector>();
                var highlight = typeof(ControllerRaySelector).GetMethod("Highlight", BindingFlags.Instance | BindingFlags.NonPublic);
                highlight.Invoke(selector, new object[] { obj });
                var block = new MaterialPropertyBlock();
                obj.GetComponent<Renderer>().GetPropertyBlock(block);
                Check(block.GetColor("_EdgeHighlightColor").a > 0, "Ray hit must highlight.");
                selector.ResetSelection();
                obj.GetComponent<Renderer>().GetPropertyBlock(block);
                Check(block.GetColor("_EdgeHighlightColor").a == 0, "Reset must clear highlight.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        finally { UnityEngine.Object.DestroyImmediate(obj); }

        // Verify the actual shipped rig, not assumptions about the template version.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GazeContingencyStudyScene.unity", OpenSceneMode.Additive);
        try
        {
            var hands = new HashSet<InteractorHandedness>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var ray in root.GetComponentsInChildren<NearFarInteractor>(true))
                {
                    if (ray.GetComponentInParent<ControllerInputActionManager>(true) == null) continue;
                    Check(ray.farInteractionCaster is CurveInteractionCaster, "Controller needs configurable curve caster.");
                    Check(ray.enableFarCasting, "Controller ray casting must be enabled.");
                    hands.Add(ray.handedness);
                    Debug.Log($"[ControllerSelectionChecks] Controller ray: {ray.name}, {ray.handedness}");
                }
            Check(hands.Contains(InteractorHandedness.Left) && hands.Contains(InteractorHandedness.Right),
                "Study scene must expose physical controller rays for both hands.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        GazeWallFilterChecks.Run();
        Debug.Log("[ControllerSelectionChecks] PASS: trigger edges, pause/tracking gates, no gaze dwell, fixed objects, highlight cleanup, both scene controllers.");
    }
}
