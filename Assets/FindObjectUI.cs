using UnityEngine;
using TMPro;
using LazyFollow = UnityEngine.XR.Interaction.Toolkit.UI.LazyFollow;

/// <summary>
/// Runtime-created world-space HUD for the Find Object game.
/// Shows current objective, progress, wrong-pick feedback, and completion.
/// Uses LazyFollow to float in front of the user's view.
/// </summary>
public class FindObjectUI : MonoBehaviour
{
    const string k_Tag = "[FindObjectUI]";

    Canvas m_Canvas;
    RectTransform m_CanvasRect;
    LazyFollow m_LazyFollow;

    TextMeshProUGUI m_ObjectiveText;
    TextMeshProUGUI m_ProgressText;
    GameObject m_CompletionPanel;
    TextMeshProUGUI m_CompletionText;

    float m_WrongFeedbackEndTime;
    string m_CurrentObjectiveString;

    public void Initialize()
    {
        // World-space Canvas
        var canvasGO = new GameObject("FindObjectCanvas");
        canvasGO.transform.SetParent(transform, false);

        m_Canvas = canvasGO.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.WorldSpace;

        m_CanvasRect = canvasGO.GetComponent<RectTransform>();
        m_CanvasRect.sizeDelta = new Vector2(400, 200);
        canvasGO.transform.localScale = Vector3.one * 0.001f; // 1 unit = 1mm

        // LazyFollow — floats in front of the camera
        m_LazyFollow = canvasGO.AddComponent<LazyFollow>();
        m_LazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
        m_LazyFollow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;

        // Background panel
        var bgGO = CreatePanel(canvasGO.transform, "Background",
            new Vector2(400, 200), new Color(0f, 0f, 0f, 0.7f));
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchoredPosition = Vector2.zero;

        // Objective text (top area)
        m_ObjectiveText = CreateText(bgGO.transform, "ObjectiveText",
            new Vector2(380, 80), new Vector2(0, 30), 36);
        m_ObjectiveText.alignment = TextAlignmentOptions.Center;

        // Progress text (bottom area)
        m_ProgressText = CreateText(bgGO.transform, "ProgressText",
            new Vector2(380, 40), new Vector2(0, -50), 28);
        m_ProgressText.alignment = TextAlignmentOptions.Center;
        m_ProgressText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // Completion panel (hidden initially)
        m_CompletionPanel = CreatePanel(canvasGO.transform, "CompletionPanel",
            new Vector2(400, 200), new Color(0.05f, 0.3f, 0.05f, 0.85f));
        m_CompletionText = CreateText(m_CompletionPanel.transform, "CompletionText",
            new Vector2(380, 180), Vector2.zero, 40);
        m_CompletionText.alignment = TextAlignmentOptions.Center;
        m_CompletionPanel.SetActive(false);

        canvasGO.SetActive(false);

        Debug.Log($"{k_Tag} UI initialized");
    }

    public void ShowObjective(Color color, string shapeName, int found, int total)
    {
        m_Canvas.gameObject.SetActive(true);
        m_CompletionPanel.SetActive(false);

        string hex = ColorUtility.ToHtmlStringRGB(color);
        m_CurrentObjectiveString = $"Find: <color=#{hex}>{shapeName}</color>";
        m_ObjectiveText.text = m_CurrentObjectiveString;
        m_ProgressText.text = $"{found} / {total} found";
    }

    public void ShowWrongFeedback()
    {
        m_WrongFeedbackEndTime = Time.time + 0.8f;
        m_ObjectiveText.text = "<color=#FF4444>Wrong object!</color>";
    }

    public void ShowCompletion(int total)
    {
        m_Canvas.gameObject.SetActive(true);
        m_CompletionPanel.SetActive(true);
        m_ObjectiveText.text = "";
        m_ProgressText.text = "";
        m_CompletionText.text = $"All {total} objects found!";
    }

    public void Hide()
    {
        if (m_Canvas != null)
            m_Canvas.gameObject.SetActive(false);
    }

    void Update()
    {
        // Restore objective text after wrong feedback
        if (m_WrongFeedbackEndTime > 0f && Time.time > m_WrongFeedbackEndTime)
        {
            m_WrongFeedbackEndTime = 0f;
            if (!string.IsNullOrEmpty(m_CurrentObjectiveString))
                m_ObjectiveText.text = m_CurrentObjectiveString;
        }
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = color;

        return go;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name,
        Vector2 size, Vector2 position, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return tmp;
    }
}
