using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>Researcher checkpoints advance only through their pointed-at Continue button.</summary>
public class StudyCheckpoint : MonoBehaviour
{
    GameObject m_Panel;
    public bool Waiting { get; private set; }
    public void Show(string message, string actionPrompt = "Continue")
    {
        if (m_Panel != null) { m_Panel.SetActive(false); Destroy(m_Panel); }
        m_Panel = new GameObject("StudyCheckpoint");
        var camera = Camera.main;
        if (camera != null) m_Panel.transform.SetParent(camera.transform, false);
        m_Panel.transform.localPosition = new Vector3(0, 0, 1.2f);
        m_Panel.transform.localScale = Vector3.one * 0.001f;
        m_Panel.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_Panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        m_Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 450);
        m_Panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.95f);
        var textObject = new GameObject("Text");
        textObject.transform.SetParent(m_Panel.transform, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.rectTransform.sizeDelta = new Vector2(740, 300);
        text.rectTransform.anchoredPosition = new Vector2(0, 55);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 30; text.raycastTarget = false;
        text.text = message + "\n\nPoint at the button and pull the trigger.";
        RayMenuFeedback.CreateButton(m_Panel.transform, actionPrompt, new Vector2(0, -155), new Vector2(440, 64), Confirm);
        Waiting = true;
    }
    public void Confirm()
    {
        if (!Waiting || !Application.isFocused) return;
        Waiting = false;
        if (m_Panel != null) { m_Panel.SetActive(false); Destroy(m_Panel); }
    }
}
