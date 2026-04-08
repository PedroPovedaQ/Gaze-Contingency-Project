using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR;

/// <summary>
/// World-space HUD for the Find Object game.
/// Positioned statically to the left of the bookshelf so it's always visible
/// without following the player's head.
/// </summary>
public class FindObjectUI : MonoBehaviour
{
    const string k_Tag = "[FindObjectUI]";
    const int k_NasaTlxQuestionCount = 6;

    Canvas m_Canvas;
    RectTransform m_CanvasRect;
    GameObject m_CanvasGO;

    TextMeshProUGUI m_ObjectiveText;
    TextMeshProUGUI m_ProgressText;
    TextMeshProUGUI m_TimerText;
    TextMeshProUGUI m_AgentStateText;
    GameObject m_CompletionPanel;
    TextMeshProUGUI m_CompletionText;
    GameObject m_NasaTlxSurveyRoot;
    readonly Slider[] m_NasaTlxSliders = new Slider[k_NasaTlxQuestionCount];
    readonly TextMeshProUGUI[] m_NasaTlxValueTexts = new TextMeshProUGUI[k_NasaTlxQuestionCount];
    readonly TextMeshProUGUI[] m_NasaTlxLabelTexts = new TextMeshProUGUI[k_NasaTlxQuestionCount];
    TextMeshProUGUI m_NasaSubmitText;
    GameObject m_CrossCanvasGO;
    TextMeshProUGUI m_FixationCross;
    TextMeshProUGUI m_CrossGoalText;

    public event System.Action OnSurveyCompletedAcknowledged;
    public event System.Action OnStatsDismissed;
    public event System.Action<NasaTlxResult> OnNasaTlxSubmitted;

    public struct NasaTlxResult
    {
        public int mental;
        public int physical;
        public int temporal;
        public int performance;
        public int effort;
        public int frustration;
    }

    float m_WrongFeedbackEndTime;
    string m_CurrentObjectiveString;
    bool m_WaitingForSurveyAck;
    bool m_ShowingPostSurveyStats;
    bool m_ShowingNasaTlxSurvey;
    bool m_ConfirmPressedLastFrame;
    bool m_UpPressedLastFrame;
    bool m_DownPressedLastFrame;
    bool m_LeftPressedLastFrame;
    bool m_RightPressedLastFrame;
    int m_SelectedTlxRow;
    readonly int[] m_NasaTlxScores = new int[k_NasaTlxQuestionCount];
    int m_CompletionTotalRounds;
    string m_CompletionTimeText;

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
        if (m_CanvasGO.GetComponent<GraphicRaycaster>() == null)
            m_CanvasGO.AddComponent<GraphicRaycaster>();
        // Enable tracked-device UI ray dragging when the Input System type is available.
        var trackedRaycasterType = System.Type.GetType("UnityEngine.InputSystem.UI.TrackedDeviceGraphicRaycaster, Unity.InputSystem");
        if (trackedRaycasterType != null && m_CanvasGO.GetComponent(trackedRaycasterType) == null)
            m_CanvasGO.AddComponent(trackedRaycasterType);

        m_CanvasRect = m_CanvasGO.GetComponent<RectTransform>();
        m_CanvasRect.sizeDelta = new Vector2(400, 260);
        m_CanvasGO.transform.localScale = Vector3.one * 0.001f;

        // Background
        var bgGO = CreatePanel(m_CanvasGO.transform, "Background",
            new Vector2(400, 260), new Color(0f, 0f, 0f, 0.75f));
        bgGO.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // Agent state debug
        m_AgentStateText = CreateText(bgGO.transform, "AgentStateText",
            new Vector2(380, 28), new Vector2(0, 102), 20);
        m_AgentStateText.alignment = TextAlignmentOptions.Center;
        m_AgentStateText.color = new Color(0.7f, 0.95f, 1f, 1f);
        m_AgentStateText.text = "Agent: --";

        // Objective text
        m_ObjectiveText = CreateText(bgGO.transform, "ObjectiveText",
            new Vector2(380, 80), new Vector2(0, 48), 38);
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
            new Vector2(380, 78), new Vector2(0, 82), 24);
        m_CompletionText.alignment = TextAlignmentOptions.Center;
        CreateNasaTlxSurveyUi(m_CompletionPanel.transform);
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

        m_CrossGoalText = CreateText(m_CrossCanvasGO.transform, "CrossGoalText",
            new Vector2(184, 44), Vector2.zero, 20);
        m_CrossGoalText.alignment = TextAlignmentOptions.TopLeft;
        m_CrossGoalText.color = Color.black;
        m_CrossGoalText.text = "";
        var goalRect = m_CrossGoalText.rectTransform;
        goalRect.anchorMin = new Vector2(0f, 1f);
        goalRect.anchorMax = new Vector2(0f, 1f);
        goalRect.pivot = new Vector2(0f, 1f);
        goalRect.anchoredPosition = new Vector2(8f, -8f);

        m_CrossCanvasGO.SetActive(false);

        m_CanvasGO.SetActive(false);

        Debug.Log($"{k_Tag} UI initialized");
    }

    /// <summary>
    /// Places the objective panel on the table, facing the player.
    /// </summary>
    public void PositionStaticLeft(Vector3 shelfCenter, Quaternion facingRotation)
    {
        if (m_CanvasGO == null) return;

        // Position: on the table, in the gap between shelf front and player.
        Vector3 facing = facingRotation * Vector3.forward;
        Vector3 pos = shelfCenter + Vector3.up * 0.015f + facing * 0.22f;
        m_CanvasGO.transform.position = pos;

        // Lay panel flat like a page on the table.
        // World-space Canvas text is readable from local -Z, so set +Z downward.
        // This makes the readable side face upward toward the user.
        m_CanvasGO.transform.rotation = Quaternion.LookRotation(Vector3.down, -facing);

        // Position fixation cross: between the two bookcases, at mid-shelf height,
        // facing the SAME direction as the shelf fronts (toward the player)
        if (m_CrossCanvasGO != null)
        {
            // Center between the two bookcases (shelfCenter is the table center)
            // and slightly forward so it's visible
            Vector3 crossPos = shelfCenter + Vector3.up * 0.40f + facing * 0.04f;
            m_CrossCanvasGO.transform.position = crossPos;

            // Use the shelf's facing rotation directly (same as the bookcases)
            // World-space Canvas renders on -Z, so we need +Z pointing away from player.
            // facingRotation has +Z pointing toward player, so flip 180° around Y.
            m_CrossCanvasGO.transform.rotation = facingRotation * Quaternion.Euler(0f, 180f, 0f);
        }

        Debug.Log($"{k_Tag} UI positioned on table gap at {pos}");
    }

    public void ShowFixationCross(string color = null, string shape = null)
    {
        if (m_CrossGoalText != null)
        {
            bool hasGoal = !string.IsNullOrEmpty(color) && !string.IsNullOrEmpty(shape);
            m_CrossGoalText.enabled = hasGoal;
            m_CrossGoalText.text = hasGoal ? $"Goal: {color} {shape}" : "";
        }
        if (m_CrossCanvasGO != null) m_CrossCanvasGO.SetActive(true);
    }

    public void HideFixationCross()
    {
        if (m_CrossGoalText != null)
        {
            m_CrossGoalText.text = "";
            m_CrossGoalText.enabled = false;
        }
        if (m_CrossCanvasGO != null) m_CrossCanvasGO.SetActive(false);
    }

    public void HideObjectiveDuringTransition()
    {
        if (m_CanvasGO == null) return;
        m_CanvasGO.SetActive(true);
        if (m_ObjectiveText != null) m_ObjectiveText.enabled = false;
        if (m_ProgressText != null) m_ProgressText.enabled = false;
        if (m_TimerText != null) m_TimerText.enabled = false;
    }

    public void SetAgentState(bool gazeAware, string conditionLabel = null)
    {
        if (m_AgentStateText == null) return;
        string mode = gazeAware ? "GAZE-AWARE" : "GAZE-UNAWARE";
        if (!string.IsNullOrEmpty(conditionLabel))
            m_AgentStateText.text = $"Agent: {mode} ({conditionLabel})";
        else
            m_AgentStateText.text = $"Agent: {mode}";
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
        m_ObjectiveText.enabled = true;
        m_ProgressText.enabled = true;
        m_TimerText.enabled = true;

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
        MoveCanvasInFrontOfUser();
        m_CanvasGO.SetActive(true);
        m_CompletionPanel.SetActive(true);
        m_ObjectiveText.text = "";
        m_ProgressText.text = "";
        m_TimerText.text = "";
        int minutes = (int)(elapsedSeconds / 60f);
        float seconds = elapsedSeconds % 60f;
        string timeStr = minutes > 0 ? $"{minutes}:{seconds:00.0}s" : $"{seconds:F1}s";
        if (m_CompletionText != null)
        {
            m_CompletionText.fontSize = 20f;
            m_CompletionText.overflowMode = TextOverflowModes.Overflow;
            m_CompletionText.enableWordWrapping = true;
        }
        m_CompletionTotalRounds = total;
        m_CompletionTimeText = timeStr;
        if (m_CompletionText != null)
        {
            m_CompletionText.text =
                $"All {total} rounds complete!\n" +
                $"Time: {timeStr}\n" +
                "NASA-TLX: adjust each slider (0-100), then submit.";
        }
        InitializeNasaTlxSurvey();
        m_WaitingForSurveyAck = true;
        m_ShowingPostSurveyStats = false;
    }

    public void ShowPostSurveyStats(string statsText)
    {
        if (m_CanvasGO == null || m_CompletionPanel == null || m_CompletionText == null) return;
        MoveCanvasInFrontOfUser();
        m_CanvasGO.SetActive(true);
        m_CompletionPanel.SetActive(true);
        m_CompletionText.fontSize = 24f;
        m_CompletionText.overflowMode = TextOverflowModes.Overflow;
        m_CompletionText.enableWordWrapping = true;
        m_CompletionText.text = statsText + "\n\nPress trigger / A / Enter to finish.";
        if (m_NasaTlxSurveyRoot != null) m_NasaTlxSurveyRoot.SetActive(false);
        m_WaitingForSurveyAck = false;
        m_ShowingPostSurveyStats = true;
        m_ShowingNasaTlxSurvey = false;
        m_ConfirmPressedLastFrame = false;
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

        if (m_CompletionPanel != null && m_CompletionPanel.activeSelf)
        {
            bool confirmPressed = IsConfirmPressed();
            if (m_WaitingForSurveyAck && m_ShowingNasaTlxSurvey)
            {
                HandleNasaTlxSurveyInput(confirmPressed);
            }
            else
            {
                bool rising = confirmPressed && !m_ConfirmPressedLastFrame;
                m_ConfirmPressedLastFrame = confirmPressed;

                if (rising)
                {
                    if (m_WaitingForSurveyAck)
                        OnSurveyCompletedAcknowledged?.Invoke();
                    else if (m_ShowingPostSurveyStats)
                        OnStatsDismissed?.Invoke();
                }
            }
        }
    }

    void InitializeNasaTlxSurvey()
    {
        for (int i = 0; i < m_NasaTlxScores.Length; i++)
            m_NasaTlxScores[i] = 50;

        m_SelectedTlxRow = 0;
        m_ShowingNasaTlxSurvey = true;
        if (m_NasaTlxSurveyRoot != null) m_NasaTlxSurveyRoot.SetActive(true);
        m_ConfirmPressedLastFrame = false;
        m_UpPressedLastFrame = false;
        m_DownPressedLastFrame = false;
        m_LeftPressedLastFrame = false;
        m_RightPressedLastFrame = false;
        for (int i = 0; i < k_NasaTlxQuestionCount; i++)
        {
            if (m_NasaTlxSliders[i] != null)
                m_NasaTlxSliders[i].value = m_NasaTlxScores[i];
        }
        RefreshNasaTlxSurveyText();
    }

    void HandleNasaTlxSurveyInput(bool confirmPressed)
    {
        Vector2 axis = ReadPrimary2DAxis();
        bool upPressed = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || axis.y > 0.6f;
        bool downPressed = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || axis.y < -0.6f;
        bool leftPressed = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || axis.x < -0.6f;
        bool rightPressed = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || axis.x > 0.6f;

        bool upRising = upPressed && !m_UpPressedLastFrame;
        bool downRising = downPressed && !m_DownPressedLastFrame;
        bool leftRising = leftPressed && !m_LeftPressedLastFrame;
        bool rightRising = rightPressed && !m_RightPressedLastFrame;
        bool confirmRising = confirmPressed && !m_ConfirmPressedLastFrame;

        bool changed = false;

        if (upRising)
        {
            m_SelectedTlxRow = Mathf.Max(0, m_SelectedTlxRow - 1);
            changed = true;
        }
        if (downRising)
        {
            m_SelectedTlxRow = Mathf.Min(k_NasaTlxQuestionCount, m_SelectedTlxRow + 1); // last row = submit
            changed = true;
        }

        if (m_SelectedTlxRow < k_NasaTlxQuestionCount)
        {
            if (leftRising)
            {
                m_NasaTlxScores[m_SelectedTlxRow] = Mathf.Clamp(m_NasaTlxScores[m_SelectedTlxRow] - 5, 0, 100);
                if (m_NasaTlxSliders[m_SelectedTlxRow] != null)
                    m_NasaTlxSliders[m_SelectedTlxRow].value = m_NasaTlxScores[m_SelectedTlxRow];
                changed = true;
            }
            if (rightRising)
            {
                m_NasaTlxScores[m_SelectedTlxRow] = Mathf.Clamp(m_NasaTlxScores[m_SelectedTlxRow] + 5, 0, 100);
                if (m_NasaTlxSliders[m_SelectedTlxRow] != null)
                    m_NasaTlxSliders[m_SelectedTlxRow].value = m_NasaTlxScores[m_SelectedTlxRow];
                changed = true;
            }
        }

        // Also accept direct slider dragging / ray interaction updates.
        for (int i = 0; i < k_NasaTlxQuestionCount; i++)
        {
            if (m_NasaTlxSliders[i] == null) continue;
            int v = Mathf.RoundToInt(m_NasaTlxSliders[i].value);
            if (m_NasaTlxScores[i] != v)
            {
                m_NasaTlxScores[i] = v;
                changed = true;
            }
        }

        if (confirmRising && m_SelectedTlxRow == k_NasaTlxQuestionCount)
        {
            m_ShowingNasaTlxSurvey = false;
            m_WaitingForSurveyAck = false;
            OnNasaTlxSubmitted?.Invoke(new NasaTlxResult
            {
                mental = m_NasaTlxScores[0],
                physical = m_NasaTlxScores[1],
                temporal = m_NasaTlxScores[2],
                performance = m_NasaTlxScores[3],
                effort = m_NasaTlxScores[4],
                frustration = m_NasaTlxScores[5],
            });
        }
        else if (changed)
        {
            RefreshNasaTlxSurveyText();
        }

        m_ConfirmPressedLastFrame = confirmPressed;
        m_UpPressedLastFrame = upPressed;
        m_DownPressedLastFrame = downPressed;
        m_LeftPressedLastFrame = leftPressed;
        m_RightPressedLastFrame = rightPressed;
    }

    void RefreshNasaTlxSurveyText()
    {
        string[] labels =
        {
            "Mental Demand",
            "Physical Demand",
            "Temporal Demand",
            "Performance",
            "Effort",
            "Frustration"
        };

        for (int i = 0; i < labels.Length; i++)
        {
            if (m_NasaTlxLabelTexts[i] != null)
            {
                string marker = m_SelectedTlxRow == i ? ">" : " ";
                m_NasaTlxLabelTexts[i].text = $"{marker} {labels[i]}";
                m_NasaTlxLabelTexts[i].color = m_SelectedTlxRow == i
                    ? new Color(1f, 0.95f, 0.65f, 1f)
                    : Color.white;
            }
            if (m_NasaTlxValueTexts[i] != null)
                m_NasaTlxValueTexts[i].text = m_NasaTlxScores[i].ToString();
        }

        if (m_NasaSubmitText != null)
        {
            bool selected = m_SelectedTlxRow == k_NasaTlxQuestionCount;
            m_NasaSubmitText.text = selected ? "> [ SUBMIT NASA-TLX ]" : "  [ SUBMIT NASA-TLX ]";
            m_NasaSubmitText.color = selected
                ? new Color(1f, 0.95f, 0.65f, 1f)
                : Color.white;
        }
    }

    static Vector2 ReadPrimary2DAxis()
    {
        var devices = GetControllerDevices();
        Vector2 best = Vector2.zero;
        float bestMag = 0f;
        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            if (!d.isValid) continue;
            if (d.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                float mag = axis.sqrMagnitude;
                if (mag > bestMag)
                {
                    bestMag = mag;
                    best = axis;
                }
                continue;
            }

            if (d.TryGetFeatureValue(CommonUsages.secondary2DAxis, out Vector2 altAxis))
            {
                float mag = altAxis.sqrMagnitude;
                if (mag > bestMag)
                {
                    bestMag = mag;
                    best = altAxis;
                }
            }
        }
        return best;
    }

    static bool IsConfirmPressed()
    {
        if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space))
            return true;

        if (Input.GetMouseButton(0))
            return true;

        var devices = GetControllerDevices();
        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            if (!d.isValid) continue;
            if (d.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary) && primary)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary) && secondary)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.menuButton, out bool menu) && menu)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool stickClick) && stickClick)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButton) && gripButton)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.75f)
                return true;
            if (d.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue > 0.75f)
                return true;
        }

        return false;
    }

    static List<InputDevice> GetControllerDevices()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);

        var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (left.isValid && !devices.Contains(left)) devices.Add(left);
        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (right.isValid && !devices.Contains(right)) devices.Add(right);
        return devices;
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

    void CreateNasaTlxSurveyUi(Transform parent)
    {
        m_NasaTlxSurveyRoot = new GameObject("NasaTlxSurvey");
        m_NasaTlxSurveyRoot.transform.SetParent(parent, false);
        var rootRect = m_NasaTlxSurveyRoot.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(380, 156);
        rootRect.anchoredPosition = new Vector2(0f, -32f);

        var labels = new[]
        {
            "Mental Demand", "Physical Demand", "Temporal Demand",
            "Performance", "Effort", "Frustration"
        };

        for (int i = 0; i < k_NasaTlxQuestionCount; i++)
        {
            float y = 62f - i * 22f;

            var label = CreateText(m_NasaTlxSurveyRoot.transform, $"NasaLabel{i}",
                new Vector2(138, 20), new Vector2(-116, y), 15);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            m_NasaTlxLabelTexts[i] = label;

            var slider = CreateSlider(m_NasaTlxSurveyRoot.transform, $"NasaSlider{i}",
                new Vector2(170, 16), new Vector2(8, y));
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 50f;
            m_NasaTlxSliders[i] = slider;

            var value = CreateText(m_NasaTlxSurveyRoot.transform, $"NasaValue{i}",
                new Vector2(36, 20), new Vector2(96, y), 15);
            value.alignment = TextAlignmentOptions.Center;
            m_NasaTlxValueTexts[i] = value;
        }

        m_NasaSubmitText = CreateText(m_NasaTlxSurveyRoot.transform, "NasaSubmit",
            new Vector2(360, 22), new Vector2(0f, -78f), 17);
        m_NasaSubmitText.alignment = TextAlignmentOptions.Center;
        m_NasaSubmitText.text = "[ SUBMIT NASA-TLX ]";
        m_NasaTlxSurveyRoot.SetActive(false);
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

    static Slider CreateSlider(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var sliderGO = new GameObject(name);
        sliderGO.transform.SetParent(parent, false);
        var sliderRect = sliderGO.AddComponent<RectTransform>();
        sliderRect.sizeDelta = size;
        sliderRect.anchoredPosition = position;

        var slider = sliderGO.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;
        var colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.selectedColor = Color.white;
        slider.colors = colors;

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.18f, 0.2f, 0.24f, 0.98f);

        // Fill area
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(4, 4);
        fillAreaRect.offsetMax = new Vector2(-4, -4);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.09f, 0.67f, 0.97f, 1f);

        // Handle slide area
        var handleSlideGO = new GameObject("Handle Slide Area");
        handleSlideGO.transform.SetParent(sliderGO.transform, false);
        var handleSlideRect = handleSlideGO.AddComponent<RectTransform>();
        handleSlideRect.anchorMin = Vector2.zero;
        handleSlideRect.anchorMax = Vector2.one;
        handleSlideRect.offsetMin = new Vector2(4, 0);
        handleSlideRect.offsetMax = new Vector2(-4, 0);

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleSlideGO.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(10, size.y + 4f);
        var handleImage = handleGO.AddComponent<Image>();
        handleImage.color = new Color(0.99f, 0.85f, 0.28f, 1f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        var minText = CreateText(sliderGO.transform, "MinLabel",
            new Vector2(24, 14), new Vector2(-size.x * 0.5f + 14f, -12f), 10);
        minText.alignment = TextAlignmentOptions.Center;
        minText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
        minText.text = "0";

        var maxText = CreateText(sliderGO.transform, "MaxLabel",
            new Vector2(30, 14), new Vector2(size.x * 0.5f - 14f, -12f), 10);
        maxText.alignment = TextAlignmentOptions.Center;
        maxText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
        maxText.text = "100";

        return slider;
    }

    void MoveCanvasInFrontOfUser()
    {
        if (m_CanvasGO == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (m_Canvas != null) m_Canvas.worldCamera = cam;

        // Completion + survey prompts should be unmistakable and directly visible.
        Vector3 pos = cam.transform.position + cam.transform.forward * 0.7f - cam.transform.up * 0.08f;
        m_CanvasGO.transform.position = pos;

        // World-space Canvas front face is -Z, so set +Z away from the viewer.
        Vector3 toCam = (cam.transform.position - pos).normalized;
        if (toCam.sqrMagnitude > 0.0001f)
            m_CanvasGO.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
    }
}
