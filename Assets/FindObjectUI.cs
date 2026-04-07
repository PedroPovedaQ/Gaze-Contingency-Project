using UnityEngine;
using TMPro;

/// <summary>
/// World-space HUD for the Find Object game.
/// Positioned statically to the left of the bookshelf so it's always visible
/// without following the player's head.
/// </summary>
public class FindObjectUI : MonoBehaviour
{
    const string k_Tag = "[FindObjectUI]";

    Canvas m_Canvas;
    RectTransform m_CanvasRect;
    GameObject m_CanvasGO;

    TextMeshProUGUI m_ObjectiveText;
    TextMeshProUGUI m_ProgressText;
    TextMeshProUGUI m_TimerText;
    GameObject m_CompletionPanel;
    TextMeshProUGUI m_CompletionText;
    GameObject m_CrossCanvasGO;
    TextMeshProUGUI m_FixationCross;

    float m_WrongFeedbackEndTime;
    string m_CurrentObjectiveString;

    public bool IsTimerRunning => m_TimerRunning;
    public float TimerStartTime => m_TimerStartTime;

    bool m_TimerRunning;
    float m_TimerStartTime;
    float m_FinalTime;
    bool m_TimerPaused;
    float m_TimerPauseStart;
    float m_TotalPausedTime;

    public void Initialize()
    {
        m_CanvasGO = new GameObject("FindObjectCanvas");
        m_CanvasGO.transform.SetParent(transform, false);

        m_Canvas = m_CanvasGO.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.WorldSpace;

        m_CanvasRect = m_CanvasGO.GetComponent<RectTransform>();
        m_CanvasRect.sizeDelta = new Vector2(400, 260);
        m_CanvasGO.transform.localScale = Vector3.one * 0.001f;

        // Background
        var bgGO = CreatePanel(m_CanvasGO.transform, "Background",
            new Vector2(400, 260), new Color(0f, 0f, 0f, 0.75f));
        bgGO.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // Objective text
        m_ObjectiveText = CreateText(bgGO.transform, "ObjectiveText",
            new Vector2(380, 80), new Vector2(0, 60), 38);
        m_ObjectiveText.alignment = TextAlignmentOptions.Center;

        // Progress text
        m_ProgressText = CreateText(bgGO.transform, "ProgressText",
            new Vector2(380, 40), new Vector2(0, -10), 28);
        m_ProgressText.alignment = TextAlignmentOptions.Center;
        m_ProgressText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // Timer text
        m_TimerText = CreateText(bgGO.transform, "TimerText",
            new Vector2(380, 40), new Vector2(0, -60), 26);
        m_TimerText.alignment = TextAlignmentOptions.Center;
        m_TimerText.color = new Color(1f, 0.9f, 0.5f, 1f);

        // Completion panel
        m_CompletionPanel = CreatePanel(m_CanvasGO.transform, "CompletionPanel",
            new Vector2(400, 260), new Color(0.05f, 0.3f, 0.05f, 0.85f));
        m_CompletionText = CreateText(m_CompletionPanel.transform, "CompletionText",
            new Vector2(380, 200), Vector2.zero, 40);
        m_CompletionText.alignment = TextAlignmentOptions.Center;
        m_CompletionPanel.SetActive(false);

        // Fixation cross — separate canvas, positioned later between the bookshelves
        m_CrossCanvasGO = new GameObject("FixationCrossCanvas");
        m_CrossCanvasGO.transform.SetParent(transform, false);
        var crossCanvas = m_CrossCanvasGO.AddComponent<Canvas>();
        crossCanvas.renderMode = RenderMode.WorldSpace;
        var crossRect = m_CrossCanvasGO.GetComponent<RectTransform>();
        crossRect.sizeDelta = new Vector2(200, 200);
        m_CrossCanvasGO.transform.localScale = Vector3.one * 0.002f;

        // White background panel behind the cross so the black "+" is visible
        var crossBg = new GameObject("CrossBg");
        crossBg.transform.SetParent(m_CrossCanvasGO.transform, false);
        var crossBgRect = crossBg.AddComponent<RectTransform>();
        crossBgRect.sizeDelta = new Vector2(200, 200);
        var crossBgImg = crossBg.AddComponent<UnityEngine.UI.Image>();
        crossBgImg.color = Color.white;

        m_FixationCross = CreateText(m_CrossCanvasGO.transform, "FixationCross",
            new Vector2(200, 200), Vector2.zero, 140);
        m_FixationCross.alignment = TextAlignmentOptions.Center;
        m_FixationCross.color = Color.black;
        m_FixationCross.text = "+";
        m_CrossCanvasGO.SetActive(false);

        m_CanvasGO.SetActive(false);

        Debug.Log($"{k_Tag} UI initialized");
    }

    /// <summary>
    /// Places the UI panel above the bookshelf, facing the player.
    /// </summary>
    public void PositionStaticLeft(Vector3 shelfCenter, Quaternion facingRotation)
    {
        if (m_CanvasGO == null) return;

        // Position: above the top of the shelf, centered
        Vector3 pos = shelfCenter + Vector3.up * 0.85f;
        m_CanvasGO.transform.position = pos;

        // Face the canvas text toward the player.
        // Unity world-space Canvas: text is readable from the -Z side.
        // So canvas +Z must point AWAY from the camera.
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 awayFromCam = pos - cam.transform.position;
            awayFromCam.y = 0;
            if (awayFromCam.sqrMagnitude > 0.01f)
                m_CanvasGO.transform.rotation = Quaternion.LookRotation(awayFromCam.normalized, Vector3.up);
        }

        // Position fixation cross: between the two bookcases, at mid-shelf height,
        // facing the SAME direction as the shelf fronts (toward the player)
        if (m_CrossCanvasGO != null)
        {
            // Center between the two bookcases (shelfCenter is the table center)
            // and slightly forward so it's visible
            Vector3 facing = facingRotation * Vector3.forward;
            Vector3 crossPos = shelfCenter + Vector3.up * 0.40f + facing * 0.04f;
            m_CrossCanvasGO.transform.position = crossPos;

            // Use the shelf's facing rotation directly (same as the bookcases)
            // World-space Canvas renders on -Z, so we need +Z pointing away from player.
            // facingRotation has +Z pointing toward player, so flip 180° around Y.
            m_CrossCanvasGO.transform.rotation = facingRotation * Quaternion.Euler(0f, 180f, 0f);
        }

        Debug.Log($"{k_Tag} UI positioned above shelf at {pos}");
    }

    public void ShowFixationCross()
    {
        if (m_CrossCanvasGO != null) m_CrossCanvasGO.SetActive(true);
    }

    public void HideFixationCross()
    {
        if (m_CrossCanvasGO != null) m_CrossCanvasGO.SetActive(false);
    }

    public void StartTimer()
    {
        m_TimerStartTime = Time.time;
        m_TimerRunning = true;
        m_TimerPaused = false;
        m_TotalPausedTime = 0f;
    }

    public float StopTimer()
    {
        m_TimerRunning = false;
        if (m_TimerPaused)
        {
            m_TotalPausedTime += Time.time - m_TimerPauseStart;
            m_TimerPaused = false;
        }
        m_FinalTime = Time.time - m_TimerStartTime - m_TotalPausedTime;
        return m_FinalTime;
    }

    public void PauseTimer()
    {
        if (m_TimerRunning && !m_TimerPaused)
        {
            m_TimerPaused = true;
            m_TimerPauseStart = Time.time;
        }
    }

    public void ResumeTimer()
    {
        if (m_TimerRunning && m_TimerPaused)
        {
            m_TotalPausedTime += Time.time - m_TimerPauseStart;
            m_TimerPaused = false;
        }
    }

    public void ShowObjective(Color color, string shapeName, int found, int total)
    {
        m_CanvasGO.SetActive(true);
        m_CompletionPanel.SetActive(false);

        string hex = ColorUtility.ToHtmlStringRGB(color);
        m_CurrentObjectiveString = $"Find: <color=#{hex}>{shapeName}</color>";
        m_ObjectiveText.text = m_CurrentObjectiveString;
        m_ProgressText.text = $"Round {found + 1} / {total}";
    }

    public void ShowWrongFeedback()
    {
        m_WrongFeedbackEndTime = Time.time + 0.8f;
        m_ObjectiveText.text = "<color=#FF4444>Wrong object!</color>";
    }

    public void ShowCompletion(int total, float elapsedSeconds)
    {
        m_CanvasGO.SetActive(true);
        m_CompletionPanel.SetActive(true);
        m_ObjectiveText.text = "";
        m_ProgressText.text = "";
        m_TimerText.text = "";
        int minutes = (int)(elapsedSeconds / 60f);
        float seconds = elapsedSeconds % 60f;
        string timeStr = minutes > 0 ? $"{minutes}:{seconds:00.0}s" : $"{seconds:F1}s";
        m_CompletionText.text = $"All {total} rounds complete!\nTime: {timeStr}";
    }

    public void Hide()
    {
        if (m_CanvasGO != null)
            m_CanvasGO.SetActive(false);
    }

    void Update()
    {
        if (m_WrongFeedbackEndTime > 0f && Time.time > m_WrongFeedbackEndTime)
        {
            m_WrongFeedbackEndTime = 0f;
            if (!string.IsNullOrEmpty(m_CurrentObjectiveString))
                m_ObjectiveText.text = m_CurrentObjectiveString;
        }

        if (m_TimerRunning && m_TimerText != null)
        {
            float pausedAdjust = m_TimerPaused ? (Time.time - m_TimerPauseStart) : 0f;
            float elapsed = Time.time - m_TimerStartTime - m_TotalPausedTime - pausedAdjust;
            int minutes = (int)(elapsed / 60f);
            float seconds = elapsed % 60f;
            m_TimerText.text = minutes > 0 ? $"{minutes}:{seconds:00.0}s" : $"{seconds:F1}s";
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
