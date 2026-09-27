using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// World-space HUD for the Find Object game.
/// Positioned statically to the left of the bookshelf so it's always visible
/// without following the player's head.
/// </summary>
public class FindObjectUI : MonoBehaviour
{
    const string k_Tag = "[FindObjectUI]";
    const int k_NasaTlxQuestionCount = 6;
    const float k_StartPromptDistance = 1.55f;
    const float k_StartPromptVerticalOffset = -0.12f;
    const string k_StartPromptDefaultText =
        "Tap the table to begin the simulation.\n\n" +
        "Once the round starts, the current goal will appear on the table.";
    const string k_StartPromptWaitingText = "Please wait...";

    Canvas m_Canvas;
    RectTransform m_CanvasRect;
    GameObject m_CanvasGO;

    TextMeshProUGUI m_ObjectiveText;
    TextMeshProUGUI m_ProgressText;
    TextMeshProUGUI m_TimerText;
    TextMeshProUGUI m_AgentStateText;
    GameObject m_StartPromptPanel;
    TextMeshProUGUI m_StartPromptText;
    GameObject m_CompletionPanel;
    TextMeshProUGUI m_CompletionText;
    GameObject m_NasaTlxSurveyRoot;
    readonly Slider[] m_NasaTlxSliders = new Slider[k_NasaTlxQuestionCount];
    readonly TextMeshProUGUI[] m_NasaTlxValueTexts = new TextMeshProUGUI[k_NasaTlxQuestionCount];
    readonly TextMeshProUGUI[] m_NasaTlxLabelTexts = new TextMeshProUGUI[k_NasaTlxQuestionCount];
    Button m_NasaSubmitButton;
    TextMeshProUGUI m_NasaSubmitText;
    Button m_ResetButton;
    Button m_FinishButton;
    TextMeshProUGUI m_ResetButtonText;
    GameObject m_CrossCanvasGO;
    TextMeshProUGUI m_FixationCross;
    UnityEngine.UI.Image m_ReadinessFill;
    TextMeshProUGUI m_CrossGoalText;

    public event System.Action OnStatsDismissed;
    public event System.Action<NasaTlxResult> OnNasaTlxSubmitted;
    public event System.Action OnResetRequested;

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
    bool m_ShowingPostSurveyStats;
    bool m_ShowingNasaTlxSurvey;
    readonly int[] m_NasaTlxScores = new int[k_NasaTlxQuestionCount];

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
        m_CanvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        m_CanvasRect = m_CanvasGO.GetComponent<RectTransform>();
        m_CanvasRect.sizeDelta = new Vector2(640, 460);
        m_CanvasGO.transform.localScale = Vector3.one * 0.00065f;

        // Background
        var bgGO = CreatePanel(m_CanvasGO.transform, "Background",
            new Vector2(640, 460), new Color(0f, 0f, 0f, 0.75f));
        bgGO.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // Agent state debug
        m_AgentStateText = CreateText(bgGO.transform, "AgentStateText",
            new Vector2(608, 34), new Vector2(0, 188), 24);
        m_AgentStateText.alignment = TextAlignmentOptions.Center;
        m_AgentStateText.color = new Color(0.7f, 0.95f, 1f, 1f);
        m_AgentStateText.text = "Agent: --";

        // Objective text
        m_ObjectiveText = CreateText(bgGO.transform, "ObjectiveText",
            new Vector2(608, 118), new Vector2(0, 106), 52);
        m_ObjectiveText.alignment = TextAlignmentOptions.Center;

        // Progress text
        m_ProgressText = CreateText(bgGO.transform, "ProgressText",
            new Vector2(608, 52), new Vector2(0, 28), 34);
        m_ProgressText.alignment = TextAlignmentOptions.Center;
        m_ProgressText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // Timer text
        m_TimerText = CreateText(bgGO.transform, "TimerText",
            new Vector2(608, 52), new Vector2(0, -28), 30);
        m_TimerText.alignment = TextAlignmentOptions.Center;
        m_TimerText.color = new Color(1f, 0.9f, 0.5f, 1f);

        m_StartPromptPanel = CreatePanel(bgGO.transform, "StartPromptPanel",
            new Vector2(560f, 180f), new Color(0.07f, 0.12f, 0.18f, 0.96f));
        m_StartPromptPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 20f);
        m_StartPromptText = CreateText(m_StartPromptPanel.transform, "StartPromptText",
            new Vector2(500f, 136f), Vector2.zero, 34f);
        m_StartPromptText.alignment = TextAlignmentOptions.Center;
        m_StartPromptText.color = new Color(0.86f, 0.96f, 1f, 1f);
        m_StartPromptText.overflowMode = TextOverflowModes.Overflow;
        m_StartPromptText.text = k_StartPromptDefaultText;
        m_StartPromptPanel.SetActive(false);

        // Completion panel
        m_CompletionPanel = CreatePanel(m_CanvasGO.transform, "CompletionPanel",
            new Vector2(640, 460), new Color(0.08f, 0.12f, 0.16f, 0.94f));
        m_CompletionText = CreateText(m_CompletionPanel.transform, "CompletionText",
            new Vector2(608, 118), new Vector2(0, 164), 24);
        m_CompletionText.alignment = TextAlignmentOptions.Center;
        CreateNasaTlxSurveyUi(m_CompletionPanel.transform);
        CreateResetButton(m_CompletionPanel.transform);
        m_FinishButton = RayMenuFeedback.CreateButton(m_CompletionPanel.transform, "Finish",
            new Vector2(0, -100), new Vector2(360, 64), FinishStats);
        m_FinishButton.gameObject.SetActive(false);
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

        var charge = new GameObject("ReadinessCharge");
        charge.transform.SetParent(m_CrossCanvasGO.transform, false);
        m_ReadinessFill = charge.AddComponent<UnityEngine.UI.Image>();
        m_ReadinessFill.color = new Color(0.15f, 0.85f, 0.75f, 0.85f);
        m_ReadinessFill.raycastTarget = false;
        m_ReadinessFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        m_ReadinessFill.rectTransform.anchoredPosition = new Vector2(-100f, 0f);
        SetReadinessProgress(0);

        m_FixationCross = CreateText(m_CrossCanvasGO.transform, "FixationCross",
            new Vector2(200, 200), Vector2.zero, 140);
        m_FixationCross.alignment = TextAlignmentOptions.Center;
        m_FixationCross.color = Color.black;
        m_FixationCross.text = "+";

        m_CrossGoalText = CreateText(m_CrossCanvasGO.transform, "CrossGoalText",
            new Vector2(240, 80), Vector2.zero, 20);
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
        ShowStartPrompt();

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

    public void PositionForRotationalSearch()
    {
        var camera = Camera.main;
        if (camera == null) return;
        // Keep the goal below the search field while the participant turns.
        m_CanvasGO.transform.SetParent(camera.transform, false);
        m_CanvasGO.transform.localPosition = new Vector3(0f, -0.38f, 0.9f);
        m_CanvasGO.transform.localRotation = Quaternion.identity;
        m_CanvasGO.transform.localScale = Vector3.one * 0.00065f;
        // The readiness cross has its own fixed world pose; only the goal panel follows the head.
    }

    public void PositionReadinessCross(Vector3 position, Quaternion rotation)
    {
        if (m_CrossCanvasGO == null) return;
        m_CrossCanvasGO.transform.SetParent(null, true);
        m_CrossCanvasGO.transform.SetPositionAndRotation(position, rotation);
        m_CrossCanvasGO.transform.localScale = Vector3.one * 0.002f;
    }
    public void SetReadinessProgress(float progress)
    {
        if (m_ReadinessFill != null)
            m_ReadinessFill.rectTransform.sizeDelta = new Vector2(200f * Mathf.Clamp01(progress), 200f);
    }
    public bool IsGazeInsideReadinessCross(Vector3 origin, Vector3 direction, float paddingMetres)
    {
        if (m_CrossCanvasGO == null || !m_CrossCanvasGO.activeInHierarchy) return false;
        var cross = m_CrossCanvasGO.transform;
        float denominator = Vector3.Dot(direction, cross.forward);
        if (denominator <= 0.0001f) return false;
        float distance = Vector3.Dot(cross.position - origin, cross.forward) / denominator;
        if (distance <= 0 || distance > 10) return false;
        Vector3 local = cross.InverseTransformPoint(origin + direction * distance);
        float halfWidth = 100f + paddingMetres / cross.lossyScale.x;
        float halfHeight = 100f + paddingMetres / cross.lossyScale.y;
        return Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.y) <= halfHeight;
    }
    void OnDestroy()
    {
        if (m_CrossCanvasGO == null) return;
        if (Application.isPlaying) Destroy(m_CrossCanvasGO);
        else DestroyImmediate(m_CrossCanvasGO);
    }

    public void ShowFixationCross(string color = null, string shape = null, bool isPractice = false)
    {
        if (m_CrossGoalText != null)
        {
            bool hasGoal = !string.IsNullOrEmpty(color) && !string.IsNullOrEmpty(shape);
            m_CrossGoalText.enabled = hasGoal;
            m_CrossGoalText.text = hasGoal
                ? $"{(isPractice ? "PRACTICE\n" : "")}Goal: {color} {shape}"
                : "";
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
        HideStartPrompt();
        m_CanvasGO.SetActive(true);
        if (m_ObjectiveText != null) m_ObjectiveText.enabled = false;
        if (m_ProgressText != null) m_ProgressText.enabled = false;
        if (m_TimerText != null) m_TimerText.enabled = false;
        if (m_AgentStateText != null) m_AgentStateText.enabled = true;
    }

    public void SetAgentState(bool gazeAware, string conditionLabel = null)
    {
        if (m_AgentStateText == null) return;
        string participantId = SessionConfig.ParticipantId;
        m_AgentStateText.text = !string.IsNullOrEmpty(participantId)
            ? $"Participant: {participantId}"
            : "Participant: --";
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

    public void ShowObjective(Color color, string shapeName, int found, int total, bool isPractice = false)
    {
        HideStartPrompt();
        m_CanvasGO.SetActive(true);
        m_CompletionPanel.SetActive(false);
        m_ObjectiveText.enabled = true;
        m_ProgressText.enabled = true;
        m_TimerText.enabled = true;
        if (m_AgentStateText != null) m_AgentStateText.enabled = true;

        string hex = ColorUtility.ToHtmlStringRGB(color);
        m_CurrentObjectiveString = $"Locate: <color=#{hex}>{shapeName}</color>";
        m_ObjectiveText.text = m_CurrentObjectiveString;
        m_ProgressText.fontSize = isPractice ? 26f : 34f;
        m_ProgressText.text = isPractice
            ? $"PRACTICE {found + 1} / {total} — not counted"
            : $"Round {found + 1} / {total}";
    }

    public void ShowWrongFeedback()
    {
        m_WrongFeedbackEndTime = Time.time + 0.8f;
        m_ObjectiveText.text = "<color=#FF4444>Wrong object!</color>";
    }

    public void ShowRoundAudioWait(bool waiting)
    {
        if (m_ObjectiveText != null)
            m_ObjectiveText.text = m_CurrentObjectiveString +
                (waiting ? "\n<size=24>Listen to the target. Look at the cross until it fills.</size>" : "");
    }

    public void ShowCompletion(int total, float elapsedSeconds)
    {
        HideStartPrompt();
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
            m_CompletionText.fontSize = 24f;
            m_CompletionText.overflowMode = TextOverflowModes.Overflow;
            m_CompletionText.enableWordWrapping = true;
            m_CompletionText.rectTransform.sizeDelta = new Vector2(608f, 118f);
            m_CompletionText.rectTransform.anchoredPosition = new Vector2(0f, 164f);
        }
        if (m_CompletionText != null)
        {
            m_CompletionText.text =
                $"All {total} rounds complete!\n" +
                $"Time: {timeStr}\n" +
                "Complete NASA-TLX below.";
        }
        SetResetButtonVisible(false);
        InitializeNasaTlxSurvey();
        m_ShowingPostSurveyStats = false;
    }

    public void ShowBlockSurvey(int blockNumber)
    {
        ShowCompletion(ChallengeSet.RoundsPerBlock, 0);
        SetSurveyBlockLabel(blockNumber);
    }

    public void SetSurveyBlockLabel(int blockNumber)
    {
        if (m_CompletionText != null) m_CompletionText.text = $"Block {blockNumber} complete.\nRate workload for this block only.";
    }

    public void HideBlockSurvey()
    {
        m_ShowingNasaTlxSurvey = false;
        if (m_CompletionPanel != null) m_CompletionPanel.SetActive(false);
        if (m_NasaTlxSurveyRoot != null) m_NasaTlxSurveyRoot.SetActive(false);
    }

    public void ShowPostSurveyStats(string statsText)
    {
        if (m_CanvasGO == null || m_CompletionPanel == null || m_CompletionText == null) return;
        HideStartPrompt();
        MoveCanvasInFrontOfUser();
        m_CanvasGO.SetActive(true);
        m_CompletionPanel.SetActive(true);
        m_CompletionText.fontSize = 24f;
        m_CompletionText.overflowMode = TextOverflowModes.Overflow;
        m_CompletionText.enableWordWrapping = true;
        m_CompletionText.rectTransform.sizeDelta = new Vector2(608f, 118f);
        m_CompletionText.rectTransform.anchoredPosition = new Vector2(0f, 164f);
        m_CompletionText.text =
            statsText +
            "\n\nSelect Finish, or Reset to Start.";
        if (m_NasaTlxSurveyRoot != null) m_NasaTlxSurveyRoot.SetActive(false);
        SetResetButtonVisible(true);
        m_ShowingPostSurveyStats = true;
        m_ShowingNasaTlxSurvey = false;
    }

    public void ShowThankYouMessage()
    {
        if (m_CanvasGO == null || m_CompletionPanel == null || m_CompletionText == null) return;
        HideStartPrompt();
        MoveCanvasInFrontOfUser();
        m_CanvasGO.SetActive(true);
        m_CompletionPanel.SetActive(true);
        m_CompletionText.fontSize = 28f;
        m_CompletionText.overflowMode = TextOverflowModes.Overflow;
        m_CompletionText.enableWordWrapping = true;
        m_CompletionText.rectTransform.sizeDelta = new Vector2(608f, 180f);
        m_CompletionText.rectTransform.anchoredPosition = new Vector2(0f, 80f);
        m_CompletionText.text =
            "Thank you for your participation in this experiment.\n\n" +
            "Please remove the headset now and have a great day.";
        if (m_NasaTlxSurveyRoot != null) m_NasaTlxSurveyRoot.SetActive(false);
        SetResetButtonVisible(false);
        m_ShowingPostSurveyStats = false;
        m_ShowingNasaTlxSurvey = false;
    }

    public void Hide()
    {
        if (m_CanvasGO != null)
            m_CanvasGO.SetActive(false);
        if (m_StartPromptPanel != null)
            m_StartPromptPanel.SetActive(false);
        SetResetButtonVisible(false);
    }

    public void ShowStartPrompt()
    {
        if (m_CanvasGO == null) return;

        MoveCanvasInFrontOfUser(k_StartPromptDistance, k_StartPromptVerticalOffset);
        m_CanvasGO.SetActive(true);
        if (m_CompletionPanel != null) m_CompletionPanel.SetActive(false);
        if (m_StartPromptPanel != null) m_StartPromptPanel.SetActive(true);
        if (m_StartPromptText != null) m_StartPromptText.text = GetComponent<FindObjectGameManager>()?.RotationalBetaEnabled == true
            ? "360° search beta\nComplete voice setup, then center and begin while seated."
            : k_StartPromptDefaultText;
        if (m_ObjectiveText != null) m_ObjectiveText.enabled = false;
        if (m_ProgressText != null) m_ProgressText.enabled = false;
        if (m_TimerText != null) m_TimerText.enabled = false;
        if (m_AgentStateText != null) m_AgentStateText.enabled = false;
        HideFixationCross();
    }

    public void ShowStartPromptWaiting()
    {
        ShowStartPrompt();
        if (m_StartPromptText != null) m_StartPromptText.text = k_StartPromptWaitingText;
    }

    public void HideStartPrompt()
    {
        if (m_StartPromptPanel != null)
            m_StartPromptPanel.SetActive(false);
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

    void InitializeNasaTlxSurvey()
    {
        for (int i = 0; i < m_NasaTlxScores.Length; i++)
            m_NasaTlxScores[i] = 50;

        m_ShowingNasaTlxSurvey = true;
        if (m_NasaTlxSurveyRoot != null) m_NasaTlxSurveyRoot.SetActive(true);
        for (int i = 0; i < k_NasaTlxQuestionCount; i++)
        {
            if (m_NasaTlxSliders[i] != null)
                m_NasaTlxSliders[i].value = m_NasaTlxScores[i];
        }
        RefreshNasaTlxSurveyText();
    }

    void SubmitNasaTlxSurvey()
    {
        if (!m_ShowingNasaTlxSurvey) return;
        m_ShowingNasaTlxSurvey = false;
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

    void RefreshNasaTlxSurveyText()
    {
        string[] labels = { "Mental Demand", "Physical Demand", "Temporal Demand", "Performance", "Effort", "Frustration" };
        for (int i = 0; i < labels.Length; i++)
        {
            if (m_NasaTlxLabelTexts[i] != null) m_NasaTlxLabelTexts[i].text = labels[i];
            if (m_NasaTlxValueTexts[i] != null) m_NasaTlxValueTexts[i].text = m_NasaTlxScores[i].ToString();
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

    void CreateNasaTlxSurveyUi(Transform parent)
    {
        m_NasaTlxSurveyRoot = new GameObject("NasaTlxSurvey");
        m_NasaTlxSurveyRoot.transform.SetParent(parent, false);
        var rootRect = m_NasaTlxSurveyRoot.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(604, 332);
        rootRect.anchoredPosition = new Vector2(0f, -52f);

        var surveyBg = CreatePanel(m_NasaTlxSurveyRoot.transform, "SurveyCard",
            new Vector2(604f, 332f), new Color(0.06f, 0.09f, 0.12f, 0.98f));
        surveyBg.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        var title = CreateText(m_NasaTlxSurveyRoot.transform, "NasaTitle",
            new Vector2(580f, 28f), new Vector2(0f, 144f), 21f);
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.82f, 0.92f, 1f, 1f);
        title.text = "NASA-TLX (0 to 100)";

        var labels = new[]
        {
            "Mental Demand", "Physical Demand", "Temporal Demand",
            "Performance", "Effort", "Frustration"
        };

        for (int i = 0; i < k_NasaTlxQuestionCount; i++)
        {
            float y = 98f - i * 40f;

            var rowBg = CreatePanel(m_NasaTlxSurveyRoot.transform, $"NasaRowBg{i}",
                new Vector2(576f, 34f), new Color(0.12f, 0.16f, 0.2f, 0.72f));
            rowBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);

            var label = CreateText(m_NasaTlxSurveyRoot.transform, $"NasaLabel{i}",
                new Vector2(216, 30), new Vector2(-184, y), 19);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            m_NasaTlxLabelTexts[i] = label;

            var slider = CreateSlider(m_NasaTlxSurveyRoot.transform, $"NasaSlider{i}",
                new Vector2(282, 22), new Vector2(36, y));
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 50f;
            m_NasaTlxSliders[i] = slider;
            int row = i;
            RayMenuFeedback.Attach(slider);
            slider.onValueChanged.AddListener(value =>
            {
                m_NasaTlxScores[row] = Mathf.RoundToInt(value);
                RefreshNasaTlxSurveyText();
            });

            var value = CreateText(m_NasaTlxSurveyRoot.transform, $"NasaValue{i}",
                new Vector2(64, 30), new Vector2(254, y), 20);
            value.alignment = TextAlignmentOptions.Center;
            m_NasaTlxValueTexts[i] = value;
        }

        m_NasaSubmitButton = CreateButton(m_NasaTlxSurveyRoot.transform, "NasaSubmitButton",
            new Vector2(326f, 40f), new Vector2(0f, -138f), new Color(0.16f, 0.2f, 0.24f, 1f));
        m_NasaSubmitButton.onClick.AddListener(SubmitNasaTlxSurvey);
        m_NasaSubmitText = CreateText(m_NasaSubmitButton.transform, "NasaSubmitText",
            new Vector2(300f, 30f), Vector2.zero, 20f);
        m_NasaSubmitText.alignment = TextAlignmentOptions.Center;
        m_NasaSubmitText.text = "Submit NASA-TLX";
        m_NasaTlxSurveyRoot.SetActive(false);
    }

    void CreateResetButton(Transform parent)
    {
        m_ResetButton = CreateButton(parent, "ResetButton",
            new Vector2(326f, 44f), new Vector2(0f, -178f), new Color(0.42f, 0.2f, 0.12f, 1f));
        m_ResetButton.onClick.AddListener(HandleResetButtonClicked);
        m_ResetButtonText = CreateText(m_ResetButton.transform, "ResetButtonText",
            new Vector2(300f, 30f), Vector2.zero, 20f);
        m_ResetButtonText.alignment = TextAlignmentOptions.Center;
        m_ResetButtonText.text = "Reset to Start";
        SetResetButtonVisible(false);
    }

    void HandleResetButtonClicked()
    {
        if (m_ShowingPostSurveyStats) OnResetRequested?.Invoke();
    }

    public void FinishStats()
    {
        if (m_ShowingPostSurveyStats) OnStatsDismissed?.Invoke();
    }

    void SetResetButtonVisible(bool visible)
    {
        if (m_ResetButton != null)
            m_ResetButton.gameObject.SetActive(visible);
        if (m_FinishButton != null) m_FinishButton.gameObject.SetActive(visible);
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
        tmp.raycastTarget = false;
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
        slider.transition = Selectable.Transition.ColorTint;
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

        return slider;
    }

    static Button CreateButton(Transform parent, string name, Vector2 size, Vector2 position, Color normalColor)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = buttonGO.AddComponent<Image>();
        image.color = normalColor;
        var button = buttonGO.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = new Color(0.2f, 0.55f, 0.9f, 1f);
        colors.pressedColor = new Color(0.15f, 0.45f, 0.78f, 1f);
        colors.selectedColor = new Color(0.2f, 0.55f, 0.9f, 1f);
        colors.disabledColor = new Color(0.32f, 0.32f, 0.32f, 0.8f);
        button.colors = colors;
        RayMenuFeedback.Attach(button);
        return button;
    }

    void MoveCanvasInFrontOfUser()
    {
        MoveCanvasInFrontOfUser(0.7f, -0.08f);
    }

    void MoveCanvasInFrontOfUser(float distance, float verticalOffset)
    {
        if (m_CanvasGO == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (m_Canvas != null) m_Canvas.worldCamera = cam;

        // Completion + survey prompts should be unmistakable and directly visible.
        Vector3 pos = cam.transform.position + cam.transform.forward * distance + cam.transform.up * verticalOffset;
        m_CanvasGO.transform.position = pos;

        // World-space Canvas front face is -Z, so set +Z away from the viewer.
        Vector3 toCam = (cam.transform.position - pos).normalized;
        if (toCam.sqrMagnitude > 0.0001f)
            m_CanvasGO.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
    }
}
