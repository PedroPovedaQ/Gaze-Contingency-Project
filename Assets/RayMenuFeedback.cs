using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Shared blue hover feedback and one pulse per physical-controller pointer entry.</summary>
[RequireComponent(typeof(Selectable))]
public class RayMenuFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    readonly HashSet<int> m_Pointers = new();
    Selectable m_Control;

    // Includes inactive wrist/tutorial controls already supplied by the scene prefabs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachSceneControls()
    {
        // Menu confirmation is a ray click, never a global Submit/A action or gaze dwell.
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
        foreach (var gaze in FindObjectsByType<XRGazeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            gaze.enableUIInteraction = false;
        foreach (var control in FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (control.GetComponentInParent<Canvas>(true)?.renderMode == RenderMode.WorldSpace) Attach(control);
    }

    public static void Attach(Selectable control)
    {
        if (control.GetComponent<RayMenuFeedback>() == null) control.gameObject.AddComponent<RayMenuFeedback>();
        control.transition = Selectable.Transition.ColorTint;
        var colors = control.colors;
        colors.highlightedColor = new Color(0.5f, 0.85f, 1f);
        colors.pressedColor = new Color(0.25f, 0.65f, 1f);
        colors.selectedColor = colors.normalColor; // No persistent highlight after the ray leaves.
        control.colors = colors;
        control.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    void Awake() => m_Control = GetComponent<Selectable>();
    void OnDisable() => m_Pointers.Clear();

    public void OnPointerEnter(PointerEventData data)
    {
        if (!Application.isFocused || !m_Control.IsInteractable() || !m_Pointers.Add(data.pointerId)) return;
        // XRI tells us which ray generated the UI hit; don't vibrate both controllers.
        if (data is TrackedDeviceEventData tracked && tracked.interactor is XRBaseInputInteractor ray &&
            ray.GetComponentInParent<ControllerInputActionManager>() != null)
            ray.SendHapticImpulse(0.2f, 0.04f);
    }

    public void OnPointerExit(PointerEventData data) => m_Pointers.Remove(data.pointerId);

    /// <summary>Make a real raycastable UI button; XRI owns pointer-down/up and trigger confirmation.</summary>
    public static Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size,
        UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.35f, 0.48f, 1f);
        var button = go.GetComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener(action); Attach(button);
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(go.transform, false);
        var tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.rectTransform.sizeDelta = new Vector2(size.x - 20, 54);
        tmp.text = label; tmp.fontSize = 25; tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 19; tmp.fontSizeMax = 25;
        tmp.raycastTarget = false;
        return button;
    }
}
