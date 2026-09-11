using UnityEngine;
using UnityEngine.XR;
using TMPro;
using System.Collections.Generic;

/// <summary>Explicit researcher checkpoints; never advances on a held button.</summary>
public class StudyCheckpoint : MonoBehaviour
{
    GameObject m_Panel;
    bool m_Held = true;
    public bool Waiting { get; private set; }
    readonly List<InputDevice> m_Devices = new List<InputDevice>();
    public void Show(string message)
    {
        if (m_Panel != null) Destroy(m_Panel);
        m_Panel = new GameObject("StudyCheckpoint");
        var camera = Camera.main;
        if (camera != null) m_Panel.transform.SetParent(camera.transform, false);
        m_Panel.transform.localPosition = new Vector3(0, 0, 1.2f);
        m_Panel.transform.localScale = Vector3.one * 0.001f;
        m_Panel.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 400);
        m_Panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.95f);
        var textObject = new GameObject("Text");
        textObject.transform.SetParent(m_Panel.transform, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.rectTransform.sizeDelta = new Vector2(740, 360);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 30;
        text.text = message + "\n\nResearcher: press Trigger / Enter to continue.";
        Waiting = true; m_Held = true;
    }
    public void Confirm()
    {
        Waiting = false;
        if (m_Panel != null) Destroy(m_Panel);
    }
    void Update()
    {
        if (!Waiting) return;
        bool pressed = false;
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, m_Devices);
        foreach (var device in m_Devices)
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool down) && down) pressed = true;
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        pressed |= keyboard != null && keyboard.enterKey.isPressed;
#else
        pressed |= Input.GetKey(KeyCode.Return);
#endif
        if (pressed && !m_Held) Confirm();
        m_Held = pressed;
    }
}
