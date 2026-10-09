using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    Button m_SettingsButton, m_PauseButton, m_RepeatButton;
    TextMeshProUGUI m_SettingsLabel;
    GameObject m_VicinityControls;
    TextMeshProUGUI m_RingLabel, m_RadiusLabel;
    readonly Dictionary<NearFarInteractor, (bool near, bool far, bool ui)> m_Rays = new();
    readonly Dictionary<XRRayInteractor, bool> m_LegacyRays = new();
    readonly Dictionary<XRPokeInteractor, bool> m_Pokes = new();
    readonly Dictionary<HandMenu, bool> m_Menus = new();
    bool m_SettingsOpen;
    bool m_PanelPositioned;

    void Start()
    {
        m_Game = GetComponent<FindObjectGameManager>();
        ConfigureRays();
        // Stop the palm follower from requiring another hand while the right hand points.
        foreach (var menu in FindObjectsByType<HandMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            m_Menus[menu] = menu.enabled;
            menu.enabled = false;
            if (menu.handMenuUIGameObject != null) menu.handMenuUIGameObject.SetActive(false);
        }
        m_Panel = new GameObject("Study ray controls", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
        m_Panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_Panel.GetComponent<Canvas>().worldCamera = Camera.main;
        m_Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(240,230);
        m_Panel.transform.localScale = Vector3.one * .00085f;
        m_SettingsButton = RayMenuFeedback.CreateButton(m_Panel.transform,"Settings",new Vector2(0,75),new Vector2(230,64),ToggleSettings);
        m_SettingsLabel = m_SettingsButton.GetComponentInChildren<TextMeshProUGUI>();
        m_PauseButton = RayMenuFeedback.CreateButton(m_Panel.transform,"Pause",Vector2.zero,new Vector2(230,64),()=>m_Game.PauseSession());
        m_RepeatButton = RayMenuFeedback.CreateButton(m_Panel.transform,"Repeat guidance",new Vector2(0,-75),new Vector2(230,64),()=>GetComponent<HintGenerator>()?.RequestRepeat());
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
        m_VicinityControls.SetActive(false);
    }

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

    public void ToggleSettings()
    {
        if (!m_SettingsOpen && m_Game != null && m_Game.CurrentState == FindObjectGameManager.GameState.Transitioning) return;
        m_SettingsOpen = !m_SettingsOpen;
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
                content.transform.SetPositionAndRotation(head.position + heading * Vector3.forward * 1.1f, heading);
                content.SetActive(true);
            }
        }
        else foreach (var item in m_Menus)
            if (item.Key != null && item.Key.handMenuUIGameObject != null) item.Key.handMenuUIGameObject.SetActive(false);
        GetComponent<StudyCheckpoint>()?.SetVisible(!m_SettingsOpen);
        if (m_SettingsLabel != null) m_SettingsLabel.text = m_SettingsOpen ? "Close settings" : "Settings";
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
        m_VicinityControls.SetActive(m_SettingsOpen && SessionConfig.NeutralQuickTest);
        if (m_VicinityControls.activeSelf)
        {
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
        m_Panel.transform.SetPositionAndRotation(head.position + heading * new Vector3(-.65f,-.23f,1.1f), heading);
        m_PanelPositioned = true;
    }

    void OnDestroy()
    {
        foreach (var item in m_Rays) if (item.Key != null)
        { item.Key.enableNearCasting=item.Value.near; item.Key.enableFarCasting=item.Value.far; item.Key.enableUIInteraction=item.Value.ui; }
        foreach (var item in m_LegacyRays) if (item.Key != null) item.Key.enableUIInteraction=item.Value;
        foreach (var item in m_Pokes) if (item.Key != null) item.Key.enableUIInteraction=item.Value;
        foreach (var item in m_Menus) if (item.Key != null) item.Key.enabled=item.Value;
        if (m_Panel != null) Destroy(m_Panel);
    }
}
