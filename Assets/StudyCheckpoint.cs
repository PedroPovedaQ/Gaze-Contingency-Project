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
        m_Panel = StudyPromptCard.Create(null, "StudyCheckpoint", new Vector2(800, 450));
        PlacePanel();
        m_Panel.transform.localScale = Vector3.one * 0.001f;
        m_Panel.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_Panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        m_Panel.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 450);
        var textObject = new GameObject("Text");
        textObject.transform.SetParent(m_Panel.transform, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.rectTransform.sizeDelta = new Vector2(740, 300);
        text.rectTransform.anchoredPosition = new Vector2(0, 55);
        StudyPromptCard.StyleText(text, 30);
        text.text = message + "\n\nPoint at the button and pull the trigger.";
        RayMenuFeedback.CreateButton(m_Panel.transform, actionPrompt, new Vector2(0, -155), new Vector2(440, 64), Confirm);
        Waiting = true;
    }
    public void SetVisible(bool visible)
    {
        if (m_Panel == null) return;
        if (visible && Waiting && !m_Panel.activeSelf) PlacePanel();
        m_Panel.SetActive(visible && Waiting);
    }
    void PlacePanel()
    {
        var camera = Camera.main;
        if (camera == null) return;
        var heading = Quaternion.Euler(0, camera.transform.eulerAngles.y, 0);
        m_Panel.transform.SetPositionAndRotation(camera.transform.position + heading * Vector3.forward * 1.2f, heading);
    }
    public void Confirm()
    {
        if (!Waiting || !Application.isFocused) return;
        Waiting = false;
        if (m_Panel != null) { m_Panel.SetActive(false); Destroy(m_Panel); }
    }
}
