using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>One audition per controller entry, with no looping while a ray stays on the card.</summary>
[RequireComponent(typeof(Button))]
public class VoicePreviewHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    readonly HashSet<int> m_Pointers = new();
    Action m_Preview;
    public void Initialize(Action preview) => m_Preview = preview;

    public void OnPointerEnter(PointerEventData data)
    {
        bool controller = data is TrackedDeviceEventData tracked && tracked.interactor is XRBaseInputInteractor ray &&
            ray.GetComponentInParent<ControllerInputActionManager>() != null;
        if (Application.isPlaying && (!controller || !Application.isFocused)) return;
        if (!GetComponent<Button>().IsInteractable()) return;
        if (m_Pointers.Add(data.pointerId) && m_Pointers.Count == 1) m_Preview?.Invoke();
    }

    public void OnPointerExit(PointerEventData data) => m_Pointers.Remove(data.pointerId);
    void OnDisable() => m_Pointers.Clear();
}
