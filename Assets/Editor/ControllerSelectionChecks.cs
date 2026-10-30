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

    static void CheckHoverFeedback()
    {
        var host = new GameObject("HoverFeedbackFixture"); host.SetActive(false);
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material material = null;
        try
        {
            var selector = host.AddComponent<ControllerRaySelector>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var shine = typeof(ControllerRaySelector).GetMethod("Shine", flags);
            var highlight = typeof(ControllerRaySelector).GetMethod("Highlight", flags);
            var rotate = typeof(ControllerRaySelector).GetMethod("RotateHoveredObject", BindingFlags.Static | BindingFlags.NonPublic);
            var renderer = obj.GetComponent<Renderer>();
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/MRTemplateAssets/Shaders/InteractablePrimitive.shadergraph"));
            renderer.sharedMaterial = material;
            Color original = new Color(.3f, .1f, .4f, .8f);
            material.SetColor("_BaseColor", original);
            var block = new MaterialPropertyBlock();
            shine.Invoke(selector, new object[] { obj }); renderer.GetPropertyBlock(block);
            Color lit = block.GetColor("_BaseColor");
            Check(lit.r > original.r && lit.b > original.b && lit.a == original.a,
                "Gaze shine brightens the surface and preserves alpha.");
            Check(Mathf.Abs(lit.r / lit.b - original.r / original.b) < .001f && material.GetColor("_BaseColor") == original,
                "Shine preserves hue and never edits the shared material.");
            Check(block.GetColor("_EdgeHighlightColor").r > .9f, "Gaze-only shine has a bright gold rim.");
            selector.ResetSelection(); renderer.GetPropertyBlock(block);
            Check(block.GetColor("_BaseColor") == original && block.GetColor("_EdgeHighlightColor").a == 0,
                "Departure/pause/reset removes surface shine and rim.");
            highlight.Invoke(selector, new object[] { obj });
            shine.Invoke(selector, new object[] { obj }); renderer.GetPropertyBlock(block);
            Check(block.GetColor("_EdgeHighlightColor") == new Color(.7f,.87f,1f,1f),
                "Controller blue rim wins while gaze still brightens the surface.");
            selector.ResetSelection();
            obj.transform.position = new Vector3(1,2,3); obj.transform.localScale = Vector3.one * .1f;
            var position = obj.transform.position; var scale = obj.transform.localScale;
            rotate.Invoke(null,new object[] { obj,true,.1f });
            var rotation = obj.transform.rotation;
            Check(Quaternion.Angle(Quaternion.identity,rotation) > 5 && obj.transform.position == position && obj.transform.localScale == scale,
                "Right hover rotates in place without shifting or resizing the object.");
            rotate.Invoke(null,new object[] { obj,false,.1f });
            Check(Quaternion.Angle(rotation,obj.transform.rotation) < .001f,"Leaving the hitbox stops rotation in place.");
            renderer.enabled = false;
            rotate.Invoke(null,new object[] { obj,true,.1f });
            shine.Invoke(selector,new object[] { obj }); renderer.GetPropertyBlock(block);
            Check(Quaternion.Angle(rotation,obj.transform.rotation) < .001f && block.GetColor("_BaseColor") == original,
                "Hidden pre-search and captured objects cannot animate or shine.");
            renderer.enabled = true; obj.GetComponent<Collider>().enabled = false;
            rotate.Invoke(null,new object[] { obj,true,.1f });
            Check(Quaternion.Angle(rotation,obj.transform.rotation) < .001f,"Disabled hitbox cannot drive rotation.");
            Debug.Log("[ControllerSelectionChecks] PASS: ring shine, hue/alpha restoration, controller rim priority, hover rotation and hidden-object guards.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(obj);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }

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
        CheckSettingsShortcut();
        CheckSettingsLifecycle();
        CheckStationaryMenus();
        CheckMixedRealityToggle();
        CheckHoverFeedback();

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

    static void CheckMixedRealityToggle()
    {
        var previous = Camera.main;
        var cameraObject = new GameObject("MixedRealityCamera", typeof(Camera));
        var host = new GameObject("MixedRealityFixture"); host.SetActive(false);
        try
        {
            if (previous != null) previous.tag="Untagged";
            cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.Skybox; camera.backgroundColor=Color.red;
            var bridge=host.AddComponent<VivePassthrough>();
            var features=host.AddComponent<UnityEngine.XR.Templates.MR.ARFeatureController>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(VivePassthrough).GetMethod("BindFeatureController",flags).Invoke(bridge,new object[]{features});
            features.onARPassthroughFeatureChanged.Invoke(true);
            Check(bridge.PassthroughEnabled && camera.clearFlags==CameraClearFlags.SolidColor && camera.backgroundColor.a==0,
                "Existing wrist toggle reveals transparent camera background for HTC underlay");
            features.onARPassthroughFeatureChanged.Invoke(false);
            Check(!bridge.PassthroughEnabled && camera.backgroundColor.a==1,"Wrist off makes the background opaque");
            bridge.SetPassthroughEnabled(true);
            Check(bridge.PassthroughEnabled && camera.backgroundColor.a==0,"Public passthrough API can enable mixed reality");
            typeof(VivePassthrough).GetMethod("OnDisable",flags).Invoke(bridge,null);
            Check(camera.clearFlags==CameraClearFlags.Skybox && camera.backgroundColor==Color.red,
                "Disabling passthrough component restores previous camera settings");
            features.onARPassthroughFeatureChanged.Invoke(false);
            Check(bridge.PassthroughEnabled,"Disabled bridge removes its wrist listener");
            typeof(VivePassthrough).GetMethod("OnEnable",flags).Invoke(bridge,null);
            features.onARPassthroughFeatureChanged.Invoke(false);
            Check(!bridge.PassthroughEnabled,"Reenabled bridge reconnects its wrist listener");
            Debug.Log("[ControllerSelectionChecks] PASS: wrist mixed-reality bridge, camera transparency, off state, restoration and reenable; native layer needs device verification.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(cameraObject);
            if(previous!=null) previous.tag="MainCamera";
        }
    }

    static void CheckStationaryMenus()
    {
        var dev=typeof(SessionConfig).GetProperty("NeutralQuickTest");
        bool originalDev=SessionConfig.NeutralQuickTest;
        var previousCamera = Camera.main;
        var cameraObject = new GameObject("StationaryMenuCamera", typeof(Camera));
        var host = new GameObject("StationaryMenuFixture"); host.SetActive(false);
        var controlsPanel = new GameObject("ControlsFixture", typeof(RectTransform));
        GameObject eyePanel = null, launcher = null, checkpointPanel = null;
        var settingsContent = new GameObject("SettingsContentFixture", typeof(RectTransform));
        settingsContent.transform.localScale = Vector3.one * .001f;
        var menuHost = new GameObject("SettingsHandMenuFixture"); menuHost.SetActive(false);
        var menu = menuHost.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.BodyUI.HandMenu>();
        menu.handMenuUIGameObject = settingsContent;
        GazeDiagnosticView eye = null;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(object target, string name, object value) => target.GetType().GetField(name,flags).SetValue(target,value);
        object Read(object target,string name) => target.GetType().GetField(name,flags).GetValue(target);
        void Call(object target,string name) => target.GetType().GetMethod(name,flags).Invoke(target,null);
        Pose PoseOf(GameObject panel) => new Pose(panel.transform.position,panel.transform.rotation);
        void Unmoved(GameObject panel,Pose pose,string name) => Check(
            Vector3.Distance(panel.transform.position,pose.position)<.0001f &&
            Quaternion.Angle(panel.transform.rotation,pose.rotation)<.01f,name+" stays fixed while the head moves");
        try
        {
            dev.SetValue(null,false);
            if(previousCamera!=null)previousCamera.tag="Untagged";
            cameraObject.tag="MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(1,1.6f,2),Quaternion.Euler(15,30,8));
            var game=host.AddComponent<FindObjectGameManager>();
            var controls=host.AddComponent<StudyRayControls>();
            ((Dictionary<UnityEngine.XR.Interaction.Toolkit.UI.BodyUI.HandMenu,bool>)Read(controls,"m_Menus"))[menu] = false;
            ((Dictionary<Transform,Vector3>)Read(controls,"m_SettingsScales"))[settingsContent.transform] = settingsContent.transform.localScale;
            Set(controls,"m_Game",game);Set(controls,"m_Panel",controlsPanel);
            foreach(string field in new[]{"m_SettingsButton","m_PauseButton","m_RepeatButton","m_EyeDotButton"})
            {
                var button=new GameObject(field,typeof(RectTransform),typeof(UnityEngine.UI.Button));
                button.transform.SetParent(controlsPanel.transform,false);Set(controls,field,button.GetComponent<UnityEngine.UI.Button>());
            }
            Set(controls,"m_EyeDot",host.AddComponent<EyeDot>());
            var eyeDotLabel = new GameObject("EyeDotLabel",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));
            eyeDotLabel.transform.SetParent(controlsPanel.transform,false);
            Set(controls,"m_EyeDotLabel",eyeDotLabel.GetComponent<TMPro.TextMeshProUGUI>());
            // Supply labels added to the production controls since this fixture was written.
            foreach (string field in new[] { "m_FollowingLabel", "m_ProviderLabel" })
            {
                var label = new GameObject(field, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                label.transform.SetParent(controlsPanel.transform, false);
                if (field == "m_ProviderLabel") label.AddComponent<UnityEngine.UI.Button>();
                Set(controls, field, label.GetComponent<TMPro.TextMeshProUGUI>());
            }
            var vicinity=new GameObject("HiddenVicinityControls");vicinity.transform.SetParent(controlsPanel.transform);
            Set(controls,"m_VicinityControls",vicinity);
            Call(controls,"LateUpdate");var controlsPose=PoseOf(controlsPanel);
            host.AddComponent<SharedGazeStream>();eye=host.AddComponent<GazeDiagnosticView>();
            Call(eye,"Start");Set(eye,"m_Game",null);Call(eye,"Update");
            eyePanel=(GameObject)Read(eye,"m_Panel");launcher=(GameObject)Read(eye,"m_Launcher");
            var launcherPose=PoseOf(launcher);
            cameraObject.transform.SetPositionAndRotation(new Vector3(2,1.8f,3),Quaternion.Euler(-25,100,-10));
            Call(controls,"LateUpdate");Call(eye,"Update");
            Unmoved(controlsPanel,controlsPose,"Settings launcher");Unmoved(launcher,launcherPose,"Eye-check launcher");
            eye.SetVisible(true);Call(eye,"Update");var eyePose=PoseOf(eyePanel);
            controls.ToggleSettings();var openControlsPose=PoseOf(controlsPanel);
            var settingsPose = PoseOf(settingsContent);
            Check(Mathf.Abs(Vector3.Distance(settingsContent.transform.position,cameraObject.transform.position)-.8f)<.001f &&
                Mathf.Abs(settingsContent.transform.localScale.x-.0013f)<.00001f, "Settings are 0.8m away and enlarged 30 percent");
            controls.SetSettingsVisible(true);
            Check(controls.SettingsOpen && Mathf.Abs(settingsContent.transform.localScale.x-.0013f)<.00001f,"Opening twice is idempotent, without cumulative scaling");
            var checkpoint=host.AddComponent<StudyCheckpoint>();checkpoint.Show("Paused");
            checkpointPanel=(GameObject)Read(checkpoint,"m_Panel");var checkpointPose=PoseOf(checkpointPanel);
            cameraObject.transform.SetPositionAndRotation(new Vector3(-1,1.5f,2),Quaternion.Euler(20,-60,15));
            Call(eye,"Update");Call(controls,"LateUpdate");
            Unmoved(eyePanel,eyePose,"Open eye panel");Unmoved(controlsPanel,openControlsPose,"Open settings controls");
            Unmoved(settingsContent,settingsPose,"Settings content");
            Unmoved(checkpointPanel,checkpointPose,"Continue checkpoint");
            Check(Quaternion.Angle(eyePanel.transform.rotation,Quaternion.Euler(0,100,0))<.01f,"Menus stay upright, ignoring head pitch and roll");
            eye.SetVisible(false);eye.SetVisible(true);
            Check(Vector3.Distance(eyePanel.transform.position,eyePose.position)>.5f,"Reopening places eye panel near the new view");
            controls.ToggleSettings();controls.ToggleSettings();
            Check(Mathf.Abs(settingsContent.transform.localScale.x-.0013f)<.00001f,"Reopening never compounds settings scale");
            Check(Vector3.Distance(controlsPanel.transform.position,openControlsPose.position)>.5f,"Reopening deliberately repositions settings");
            controls.SetSettingsVisible(false);
            Check(!controls.SettingsOpen && !settingsContent.activeSelf,"Dismiss API hides settings");
            Debug.Log("[ControllerSelectionChecks] PASS: eye/settings launchers, open menus and Continue stay world-fixed through head movement; reopen reanchors upright.");
        }
        finally
        {
            foreach(var panel in new[]{eyePanel,launcher,checkpointPanel,controlsPanel})
                if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);
            if(eye!=null)Call(eye,"OnDestroy");
            UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(menuHost);UnityEngine.Object.DestroyImmediate(settingsContent);
            if(previousCamera!=null)previousCamera.tag="MainCamera";
            dev.SetValue(null,originalDev);
        }
    }

    static void CheckSettingsShortcut()
    {
        var chord = new SettingsShortcut();
        chord.Step(true,false,false,0);
        Check(!chord.Step(true,true,true,1),"First A+stick press does not open settings");
        Check(!chord.Step(true,true,true,1.1),"Holding the stick is not a second click");
        chord.Step(true,true,false,1.15);
        Check(chord.Step(true,true,true,1.3),"A held with a second distinct stick click opens settings");
        Check(!chord.Step(true,true,true,1.31),"Held chord fires once");
        chord.Step(true,true,false,1.4);
        Check(!chord.Step(true,true,true,1.5),"Third click starts a new pair");
        chord.Step(true,false,false,1.6);
        Check(!chord.Step(true,true,true,1.7),"Releasing A cancels the incomplete chord");
        chord.Step(true,true,false,1.8);
        Check(!chord.Step(true,true,true,2.3),"Slow second click restarts rather than opens");
        chord.Step(false,true,false,2.4);
        Check(!chord.Step(true,true,true,2.5),"Reconnect while pressed cannot complete a chord");
        chord.Step(true,true,false,2.6);
        Check(!chord.Step(true,true,true,2.7),"Reconnect requires two fresh clicks");
        chord.Step(true,true,false,2.8);
        Check(chord.Step(true,true,true,2.9),"Fresh chord works after reconnect");
        chord.Reset(); chord.Step(true,false,false,3);
        chord.Step(true,false,true,3.1);chord.Step(true,false,false,3.2);
        Check(!chord.Step(true,false,true,3.3),"Stick double click without A cannot open settings");
        Debug.Log("[ControllerSelectionChecks] PASS: right settings chord edges, hold, A release, timeout, reconnect and repeat.");
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
