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

        CheckRightRayPolicy();
        CheckSettingsLifecycle();

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
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GazeContingencyStudyScene.unity");
        bool openedForCheck = !scene.IsValid() || !scene.isLoaded;
        if (openedForCheck) scene = EditorSceneManager.OpenScene("Assets/Scenes/GazeContingencyStudyScene.unity", OpenSceneMode.Additive);
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
        finally { if (openedForCheck) EditorSceneManager.CloseScene(scene, true); }
        GazeWallFilterChecks.Run();
        Debug.Log("[ControllerSelectionChecks] PASS: trigger edges, pause/tracking gates, no gaze dwell, fixed objects, highlight cleanup, scene rig and right-only UI policy.");
    }
    static void CheckRightRayPolicy()
    {
        var rig = new GameObject("RightOnlyFixture"); rig.SetActive(false);
        var leftObject = new GameObject("LeftRayFixture"); leftObject.transform.SetParent(rig.transform);
        var rightObject = new GameObject("RightRayFixture"); rightObject.transform.SetParent(rig.transform);
        var handObject = new GameObject("HandRayFixture"); handObject.SetActive(false);
        var controlsObject = new GameObject("StudyControlsFixture"); controlsObject.SetActive(false);
        try
        {
            rig.AddComponent<ControllerInputActionManager>();
            var left = leftObject.AddComponent<NearFarInteractor>(); left.handedness = InteractorHandedness.Left;
            var right = rightObject.AddComponent<NearFarInteractor>(); right.handedness = InteractorHandedness.Right;
            var hand = handObject.AddComponent<NearFarInteractor>(); hand.handedness = InteractorHandedness.Right;
            left.enableFarCasting=right.enableFarCasting=hand.enableFarCasting=true;
            left.enableUIInteraction=right.enableUIInteraction=hand.enableUIInteraction=true;
            right.enableFarCasting = right.enableUIInteraction = false; // Startup in hand mode.
            var controls = controlsObject.AddComponent<StudyRayControls>();
            typeof(StudyRayControls).GetMethod("ConfigureRays", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controls,null);
            Check(StudyRayControls.IsStudyRay(right) && !StudyRayControls.IsStudyRay(left) && !StudyRayControls.IsStudyRay(hand), "Only the physical right controller is a study pointer.");
            Check(right.enableFarCasting && right.enableUIInteraction && !right.enableNearCasting,
                "Right ray keeps UI and far targeting without near-grab selection.");
            Check(!left.enableFarCasting && !left.enableUIInteraction && !hand.enableUIInteraction,
                "Left controller and hand rays cannot hover or activate study controls.");
            right.enableFarCasting = right.enableUIInteraction = false;
            left.enableUIInteraction = true;
            typeof(StudyRayControls).GetMethod("ApplyRayPolicy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controls,null);
            Check(right.enableFarCasting && right.enableUIInteraction && !left.enableUIInteraction,
                "A modality switch cannot leave the right pointer permanently unavailable or enable the left pointer.");
            typeof(StudyRayControls).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controls,null);
            Check(left.enableFarCasting && left.enableUIInteraction, "Removing study controls restores original ray settings.");
            Check(!right.enableFarCasting && !right.enableUIInteraction, "Original disabled right-ray settings restore after study.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controlsObject); UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(handObject);
        }
    }

    static void CheckSettingsLifecycle()
    {
        foreach (bool readiness in new[] { false, true })
        {
            var host = new GameObject("SettingsLifecycleFixture"); host.SetActive(false);
            var stimulus = new GameObject("SettingsStimulusFixture");
            GameObject checkpointPanel = null;
            void Set(object target, string name, object value) => target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target,value);
            try
            {
                var game = host.AddComponent<FindObjectGameManager>();
                var ui = host.AddComponent<FindObjectUI>();
                var controls = host.AddComponent<StudyRayControls>();
                Set(game,"m_UI",ui); Set(controls,"m_Game",game);
                ((List<GameObject>)game.SpawnedObjects).Add(stimulus);
                controls.ToggleSettings();
                Check(!game.ResearcherPaused,"Opening Settings in setup does not invent an active trial.");
                Set(game,"m_State",FindObjectGameManager.GameState.Playing);
                if (readiness)
                {
                    var gate = host.AddComponent<TrialReadinessGate>();
                    Set(game,"m_Readiness",gate);
                    gate.Begin(0,Vector3.forward,Quaternion.identity,"Blue","Cube",false);
                }
                else Set(game,"<SearchActive>k__BackingField",true);
                typeof(StudyRayControls).GetMethod("LateUpdate",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controls,null);
                var checkpoint = host.GetComponent<StudyCheckpoint>();
                Check(checkpoint != null,"Newly active trial must create its pause checkpoint.");
                checkpointPanel = (GameObject)typeof(StudyCheckpoint).GetField("m_Panel",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(checkpoint);
                Check(game.ResearcherPaused && !game.SearchActive && !stimulus.activeSelf,
                    "Settings already open must pause newly active readiness/search and hide objects.");
                Check(checkpoint.Waiting && !checkpointPanel.activeSelf,
                    "Continue cannot be visible underneath Settings.");
                controls.ToggleSettings();
                Check(game.ResearcherPaused && checkpoint.Waiting && checkpointPanel.activeSelf,
                    "Closing Settings exposes Continue without resuming automatically.");
                // Model accepted Continue without calling runtime Destroy in edit mode.
                Set(checkpoint,"<Waiting>k__BackingField",false);
                game.ResumeSession();
                Check(!game.ResearcherPaused && stimulus.activeSelf && game.SearchActive != readiness,
                    "Explicit Continue resumes the correct readiness/search stage.");
            }
            finally
            {
                if (checkpointPanel != null) UnityEngine.Object.DestroyImmediate(checkpointPanel);
                ((List<GameObject>)host.GetComponent<FindObjectGameManager>().SpawnedObjects).Clear();
                UnityEngine.Object.DestroyImmediate(stimulus);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }

}
