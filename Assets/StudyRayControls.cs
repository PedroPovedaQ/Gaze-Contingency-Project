using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

/// <summary>
/// Right-ray access to existing settings, pause and guidance repeat, with no left controller.
/// Settings temporarily dock in front of the participant; the original settings content is reused.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class StudyRayControls : MonoBehaviour
{
    FindObjectGameManager m_Game;
    GameObject m_Panel;
    GameObject m_DismissPanel;
    readonly SettingsShortcut m_Shortcut = new();
    readonly Dictionary<Transform, Vector3> m_SettingsScales = new();
    public bool SettingsOpen => m_SettingsOpen;
    Button m_SettingsButton, m_PauseButton, m_RepeatButton;
    TextMeshProUGUI m_SettingsLabel, m_FollowingLabel;
    Button m_EyeDotButton;
    TextMeshProUGUI m_EyeDotLabel;
    EyeDot m_EyeDot;
    GameObject m_VicinityControls;
    TextMeshProUGUI m_RingLabel, m_RadiusLabel, m_ActionPolicyLabel, m_ProviderLabel;
    readonly Dictionary<NearFarInteractor, (bool near, bool far, bool ui)> m_Rays = new();
    readonly Dictionary<XRRayInteractor, bool> m_LegacyRays = new();
    readonly Dictionary<XRPokeInteractor, bool> m_Pokes = new();
    readonly Dictionary<HandMenu, bool> m_Menus = new();
    bool m_SettingsOpen;
    bool m_PanelPositioned;

    void Start()
    {
        m_Game = GetComponent<FindObjectGameManager>();
        m_EyeDot = GetComponent<EyeDot>() ?? gameObject.AddComponent<EyeDot>();
        ConfigureRays();
        // Stop the palm follower from requiring another hand while the right hand points.
        foreach (var menu in FindObjectsByType<HandMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            m_Menus[menu] = menu.enabled;
            menu.enabled = false;
            if (menu.handMenuUIGameObject != null)
            {
                var content = menu.handMenuUIGameObject;
                m_SettingsScales[content.transform] = content.transform.localScale;
                content.SetActive(false);
            }
        }
        m_Panel = new GameObject("Study ray controls", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
        m_Panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_Panel.GetComponent<Canvas>().worldCamera = Camera.main;
        m_Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(240,230);
        m_Panel.transform.localScale = Vector3.one * .0011f;
        m_SettingsButton = RayMenuFeedback.CreateButton(m_Panel.transform,"Settings",new Vector2(0,75),new Vector2(230,64),ToggleSettings);
        m_SettingsLabel = m_SettingsButton.GetComponentInChildren<TextMeshProUGUI>();
        m_PauseButton = RayMenuFeedback.CreateButton(m_Panel.transform,"Pause",Vector2.zero,new Vector2(230,64),()=>m_Game.PauseSession());
        m_RepeatButton = RayMenuFeedback.CreateButton(m_Panel.transform,"Repeat guidance",new Vector2(0,-75),new Vector2(230,64),()=>GetComponent<HintGenerator>()?.RequestRepeat());
        m_EyeDotButton = RayMenuFeedback.CreateButton(m_Panel.transform, "Eye dot: Off", new Vector2(0,150),
            new Vector2(230,64), () => m_EyeDot.SetVisible(!m_EyeDot.Visible));
        m_EyeDotLabel = m_EyeDotButton.GetComponentInChildren<TextMeshProUGUI>();
        m_EyeDotButton.gameObject.SetActive(false);
        m_VicinityControls = new GameObject("Development gaze vicinity controls", typeof(RectTransform));
        m_VicinityControls.transform.SetParent(m_Panel.transform, false);
        var ring = RayMenuFeedback.CreateButton(m_VicinityControls.transform, "Target ring: Off", new Vector2(0,-155), new Vector2(290,64), () => {
            var hints = GetComponent<HintGenerator>();
            if (hints?.Vicinity != null) hints.SetVicinityRing(!hints.Vicinity.RingVisible);
        });
        m_RingLabel = ring.GetComponentInChildren<TextMeshProUGUI>();
        RayMenuFeedback.CreateButton(m_VicinityControls.transform, "Radius −", new Vector2(-75,-225), new Vector2(140,60),
            () => GetComponent<HintGenerator>()?.AdjustVicinityPadding(-1));
        RayMenuFeedback.CreateButton(m_VicinityControls.transform, "Radius +", new Vector2(75,-225), new Vector2(140,60),
            () => GetComponent<HintGenerator>()?.AdjustVicinityPadding(1));
        var radius = new GameObject("Gaze radius value", typeof(RectTransform), typeof(TextMeshProUGUI));
        radius.transform.SetParent(m_VicinityControls.transform, false);
        m_RadiusLabel = radius.GetComponent<TextMeshProUGUI>();
        m_RadiusLabel.rectTransform.sizeDelta = new Vector2(320,65);
        m_RadiusLabel.rectTransform.anchoredPosition = new Vector2(0,-295);
        StudyPromptCard.StyleText(m_RadiusLabel, 22);
        m_ActionPolicyLabel = RayMenuFeedback.CreateButton(m_VicinityControls.transform, "DEV guidance: adaptive",
            new Vector2(0,-365), new Vector2(320,64),
            () => GetComponent<HintGenerator>()?.ToggleDevelopmentGuidancePolicy()).GetComponentInChildren<TextMeshProUGUI>();
        m_ProviderLabel = RayMenuFeedback.CreateButton(m_Panel.transform, "Model: Local MiniLM",
            new Vector2(340,-155), new Vector2(320,110),
            () => GetComponent<HintGenerator>()?.ToggleAssistanceProvider()).GetComponentInChildren<TextMeshProUGUI>();
        m_ProviderLabel.GetComponentInParent<Button>(true).gameObject.SetActive(false);
        var following = new GameObject("Active assistance status", typeof(RectTransform), typeof(TextMeshProUGUI));
        following.transform.SetParent(m_Panel.transform, false);
        m_FollowingLabel = following.GetComponent<TextMeshProUGUI>();
        m_FollowingLabel.rectTransform.sizeDelta = new Vector2(570,130);
        m_FollowingLabel.rectTransform.anchoredPosition = new Vector2(145,265);
        StudyPromptCard.StyleText(m_FollowingLabel, 20);
        m_FollowingLabel.raycastTarget = false;
        m_VicinityControls.SetActive(false);
        m_DismissPanel = new GameObject("Dismiss settings", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
        m_DismissPanel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_DismissPanel.GetComponent<Canvas>().worldCamera = Camera.main;
        m_DismissPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(320,70);
        m_DismissPanel.transform.localScale = Vector3.one * .0011f;
        RayMenuFeedback.CreateButton(m_DismissPanel.transform, "Close settings", Vector2.zero,
            new Vector2(320,70), () => SetSettingsVisible(false));
        m_DismissPanel.SetActive(false);
    }

    void Update()
    {
        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool a = false, stick = false;
        bool available = Application.isFocused && right.isValid &&
            right.TryGetFeatureValue(CommonUsages.primaryButton, out a) &&
            right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out stick);
        if (m_Shortcut.Step(available, a, stick, Time.realtimeSinceStartupAsDouble)) ToggleSettings();
    }

    void OnDisable() => m_Shortcut.Reset();
    void OnApplicationFocus(bool focused) { if (!focused) m_Shortcut.Reset(); }

    public static bool IsStudyRay(NearFarInteractor ray) => ray != null &&
        ray.handedness == InteractorHandedness.Right && ray.GetComponentInParent<ControllerInputActionManager>(true) != null;

    void ConfigureRays()
    {
        foreach (var ray in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            m_Rays[ray] = (ray.enableNearCasting, ray.enableFarCasting, ray.enableUIInteraction);
        foreach (var ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            m_LegacyRays[ray] = ray.enableUIInteraction;
        foreach (var poke in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            m_Pokes[poke] = poke.enableUIInteraction;
        ApplyRayPolicy();
    }

    void ApplyRayPolicy()
    {
        foreach (var item in m_Rays)
        {
            var ray = item.Key; if (ray == null) continue;
            bool right = IsStudyRay(ray);
            ray.enableNearCasting = false;
            // A controller may start inactive while the headset reports hand mode.
            // Original values are for restoration, not a permanent startup veto.
            ray.enableFarCasting = right;
            ray.enableUIInteraction = right;
        }
        // Gaze, teleport and hand-poke UI paths must not activate study controls.
        foreach (var item in m_LegacyRays) if (item.Key != null) item.Key.enableUIInteraction = false;
        foreach (var item in m_Pokes) if (item.Key != null) item.Key.enableUIInteraction = false;
    }

    public void ToggleSettings() => SetSettingsVisible(!m_SettingsOpen);

    public void SetSettingsVisible(bool visible)
    {
        if (visible == m_SettingsOpen) return;
        if (visible && m_Game != null && m_Game.CurrentState == FindObjectGameManager.GameState.Transitioning) return;
        m_SettingsOpen = visible;
        if (m_SettingsOpen)
        {
            if (m_Game != null) m_Game.PauseSession();
            var head = Camera.main != null ? Camera.main.transform : null;
            PlaceControls();
            foreach (var item in m_Menus)
            {
                var content = item.Key != null ? item.Key.handMenuUIGameObject : null;
                if (content == null || head == null) continue;
                var heading = Quaternion.Euler(0, head.eulerAngles.y, 0);
                content.transform.SetPositionAndRotation(head.position + heading * Vector3.forward * .8f, heading);
                if (m_SettingsScales.TryGetValue(content.transform, out var originalScale))
                    content.transform.localScale = originalScale * 1.3f;
                content.SetActive(true);
            }
            if (m_DismissPanel != null && head != null)
                m_DismissPanel.transform.SetPositionAndRotation(head.position + Quaternion.Euler(0, head.eulerAngles.y, 0) *
                    new Vector3(0,-.4f,.78f), Quaternion.Euler(0, head.eulerAngles.y, 0));
        }
        else foreach (var item in m_Menus)
            if (item.Key != null && item.Key.handMenuUIGameObject != null) item.Key.handMenuUIGameObject.SetActive(false);
        GetComponent<StudyCheckpoint>()?.SetVisible(!m_SettingsOpen);
        if (m_SettingsLabel != null) m_SettingsLabel.text = m_SettingsOpen ? "Close settings" : "Settings";
        if (m_DismissPanel != null) m_DismissPanel.SetActive(m_SettingsOpen);
    }

    void LateUpdate()
    {
        ApplyRayPolicy(); // Input modality changes cannot re-enable the unused hand's UI.
        if (m_SettingsOpen)
        {
            // Settings can open before readiness/search exists (setup or success
            // feedback). Pause any trial that becomes active while it stays open.
            m_Game?.PauseSession();
            GetComponent<StudyCheckpoint>()?.SetVisible(false);
        }
        if (m_Panel == null || Camera.main == null || m_Game == null) return;
        if (!m_PanelPositioned) PlaceControls();
        m_SettingsButton.interactable = m_SettingsOpen || m_Game.CurrentState != FindObjectGameManager.GameState.Transitioning;
        m_PauseButton.interactable = !m_SettingsOpen && !m_Game.ResearcherPaused &&
            (m_Game.SearchActive || m_Game.Readiness?.Active == true);
        m_RepeatButton.interactable = !m_SettingsOpen && m_Game.SearchActive;
        m_EyeDotButton.gameObject.SetActive(m_SettingsOpen);
        m_EyeDotButton.interactable = m_Game.RotationalBetaEnabled;
        m_EyeDotLabel.text = "Eye dot: " + (m_EyeDot.Visible ? "On" : "Off");
        m_FollowingLabel.gameObject.SetActive(SessionConfig.DevelopmentQuickTest || m_SettingsOpen);
        if (m_FollowingLabel.gameObject.activeSelf)
        {
            var following = GetComponent<HintGenerator>()?.CaptureAssistanceStatus() ?? SessionConfig.CaptureAssistanceSelection();
            m_FollowingLabel.text = "Selected: " + following.SelectionLabel + "\nConnection: " + following.ConnectionLabel +
                "\nLast accepted: " + following.LastDecisionLabel;
        }
        m_ProviderLabel.GetComponentInParent<Button>(true).gameObject.SetActive(m_SettingsOpen);
        m_ProviderLabel.text = SessionConfig.StudyModelLocked ? "Study model: Local MiniLM\n" + (GetComponent<SpatialVoice.StudyNavigationVoiceBridge>()?.LocalModelStatus ?? "Connect Mac") : !SessionConfig.JevLedGuidance ? "Model: unused in baseline" : SessionConfig.LocalAdaptiveModel
            ? "Model: Local MiniLM\n" + (GetComponent<SpatialVoice.StudyNavigationVoiceBridge>()?.LocalModelStatus ?? "Connect Mac") + "\nTap to use Jev"
            : "Model: Jev\nTap to use Local MiniLM";
        m_ProviderLabel.GetComponentInParent<Button>(true).interactable = !SessionConfig.StudyModelLocked && SessionConfig.JevLedGuidance &&
            (!m_Game.SearchActive || m_Game.ResearcherPaused);
        m_VicinityControls.SetActive(m_SettingsOpen && SessionConfig.DevelopmentQuickTest);
        if (m_VicinityControls.activeSelf)
        {
            m_ActionPolicyLabel.text = "DEV guidance: " + (SessionConfig.JevLedGuidance ? "adaptive" : "baseline");
            var vicinity = GetComponent<HintGenerator>()?.Vicinity;
            m_RingLabel.text = "DEV target ring: " + (vicinity != null && vicinity.RingVisible ? "On" : "Off");
            m_RadiusLabel.text = $"Gaze radius: object + {(vicinity != null ? vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees):0}°";
        }
    }

    void PlaceControls()
    {
        if (m_Panel == null || Camera.main == null) return;
        var head = Camera.main.transform;
        var heading = Quaternion.Euler(0, head.eulerAngles.y, 0);
        m_Panel.transform.SetPositionAndRotation(head.position + heading * new Vector3(-.5f,-.12f,.8f), heading);
        m_PanelPositioned = true;
    }

    void OnDestroy()
    {
        foreach (var item in m_Rays) if (item.Key != null)
        { item.Key.enableNearCasting=item.Value.near; item.Key.enableFarCasting=item.Value.far; item.Key.enableUIInteraction=item.Value.ui; }
        foreach (var item in m_LegacyRays) if (item.Key != null) item.Key.enableUIInteraction=item.Value;
        foreach (var item in m_Pokes) if (item.Key != null) item.Key.enableUIInteraction=item.Value;
        foreach (var item in m_Menus) if (item.Key != null) item.Key.enabled=item.Value;
        foreach (var item in m_SettingsScales) if (item.Key != null) item.Key.localScale = item.Value;
        if (m_Panel != null) Destroy(m_Panel);
        if (m_DismissPanel != null) Destroy(m_DismissPanel);
    }
}

/// <summary>Hold A throughout two distinct stick presses within 450ms. Release/reconnect cannot complete an old chord.</summary>
public sealed class SettingsShortcut
{
    bool m_Initialized, m_StickDown;
    double m_FirstPress = double.NegativeInfinity;
    public void Reset() { m_Initialized = m_StickDown = false; m_FirstPress = double.NegativeInfinity; }
    public bool Step(bool available, bool aHeld, bool stickDown, double now)
    {
        if (!available) { Reset(); return false; }
        if (!m_Initialized) { m_Initialized = true; m_StickDown = stickDown; return false; }
        bool pressed = stickDown && !m_StickDown;
        m_StickDown = stickDown;
        if (!aHeld) { m_FirstPress = double.NegativeInfinity; return false; }
        if (!pressed) return false;
        if (now >= m_FirstPress && now - m_FirstPress <= .45)
        { m_FirstPress = double.NegativeInfinity; return true; }
        m_FirstPress = now;
        return false;
    }
}
