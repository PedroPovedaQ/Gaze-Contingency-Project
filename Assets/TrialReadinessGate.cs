using UnityEngine;

/// <summary>World-fixed, generously padded gaze gate. Never captures a search object.</summary>
[DefaultExecutionOrder(250)]
public sealed class TrialReadinessGate : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float m_ChargeSeconds = 1f;
    [SerializeField, Min(0f)] float m_PaddingMetres = 0.15f;
    readonly StudyReadinessCharge m_Charge = new();
    readonly StudyPromptDelay m_Reminder = new(5);
    VoiceSynthesizer m_Voice;
    string m_ReminderPhrase;
    FindObjectGameManager m_Game;
    FindObjectUI m_UI;
    string m_Color, m_Shape;
    bool m_Practice;
    public bool Active { get; private set; }
    public bool Completed { get; private set; }
    public bool ValidInside { get; private set; }
    public float Progress => m_Charge.Progress;
    public int Wall { get; private set; }
    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; } = Quaternion.identity;

    public void Begin(int wall, Vector3 position, Quaternion rotation, string color, string shape, bool practice)
    {
        m_Game = GetComponent<FindObjectGameManager>(); m_UI = GetComponent<FindObjectUI>();
        m_Voice = GetComponent<VoiceSynthesizer>(); m_Reminder.Reset(); m_ReminderPhrase = null;
        Wall = wall; Position = position; Rotation = rotation;
        m_Color = color; m_Shape = shape; m_Practice = practice;
        m_Charge.Reset(); Completed = false; ValidInside = false; Active = true;
        Show(); m_Game.RecordReadinessEvent("readiness_started");
    }
    public void Show()
    {
        m_UI.PositionReadinessCross(Position, Rotation);
        m_UI.ShowFixationCross(m_Color, m_Shape, m_Practice);
        m_UI.SetReadinessProgress(Progress);
    }
    public void ResetCharge()
    {
        StopReminder(); m_Reminder.Reset();
        if (Progress > 0) { m_Charge.Reset(); m_Game.RecordReadinessEvent("readiness_reset"); }
        ValidInside = false; Completed = false;
        m_UI?.SetReadinessProgress(0);
    }
    public void Cancel()
    {
        ResetCharge(); Active = false; Completed = false; m_UI?.HideFixationCross();
    }
    public void Finish() { StopReminder(); Active = false; m_UI?.HideFixationCross(); }
    void StopReminder() { m_Voice?.InterruptIfAbout("readiness_reminder"); m_ReminderPhrase = null; }

    // A cross is a known world position. Use the same corrected, validity-aware
    // eye ray as readiness itself; never substitute a head direction during loss.
    public static bool TryReminderDirection(Vector3 origin, Vector3 direction, Vector3 cross,
        out float yaw, out float pitch)
    {
        yaw = pitch = 0;
        Vector3 delta = cross - origin;
        Vector3 flatGaze = new(direction.x, 0, direction.z), flatCross = new(delta.x, 0, delta.z);
        if (flatGaze.sqrMagnitude < .04f || flatCross.sqrMagnitude < .0001f) return false;
        yaw = Vector3.SignedAngle(flatGaze, flatCross, Vector3.up);
        pitch = (Mathf.Atan2(delta.y, flatCross.magnitude) - Mathf.Atan2(direction.y, flatGaze.magnitude)) * Mathf.Rad2Deg;
        return !float.IsNaN(yaw) && !float.IsNaN(pitch) && !float.IsInfinity(yaw) && !float.IsInfinity(pitch);
    }

    void UpdateReminder(bool valid, GazeObservation gaze)
    {
        bool waiting = !Completed && !m_Game.ResearcherPaused && m_Game.AnnouncementReady && !ValidInside;
        bool due = m_Reminder.Step(waiting, Time.unscaledDeltaTime);
        if (!waiting || !valid || !TryReminderDirection(gaze.worldOrigin, gaze.worldDirection, Position, out float yaw, out float pitch))
        { StopReminder(); return; }
        string phrase = DirectionalHintPolicy.ReadinessReminder(yaw, pitch);
        if (m_ReminderPhrase != null && m_ReminderPhrase != phrase) StopReminder();
        if (!due || m_Voice == null || m_Voice.IsBusy || !m_Voice.LibraryReady) return;
        var decision = GuidanceDecision.Capture(System.Guid.NewGuid().ToString("N"), "requested",
            "readiness_cross_wait", "readiness_reminder", phrase, gaze, Time.realtimeSinceStartupAsDouble,
            true, false, false, true);
        m_Voice.RecordGuidanceDecision(JsonUtility.ToJson(decision));
        m_Game.RecordReadinessEvent("readiness_reminder");
        m_Reminder.MarkSpoken(); m_ReminderPhrase = phrase;
        m_Voice.Speak(phrase, "readiness_reminder", guidanceDecisionId: decision.decisionId);
    }

    void Update()
    {
        if (!Active || m_Game == null) return;
        GazeObservation gaze = null;
        bool valid = SharedGazeStream.Instance != null && SharedGazeStream.Instance.TryGetValid(out gaze);
        ValidInside = valid && m_UI.IsGazeInsideReadinessCross(gaze.worldOrigin, gaze.worldDirection, m_PaddingMetres);
        if (Completed) return; // Continue reporting live cross intersection during the opening.
        UpdateReminder(valid, gaze);
        float previous = Progress;
        bool complete = m_Charge.Step(ValidInside, m_Game.ResearcherPaused, m_Game.AnnouncementReady,
            Time.unscaledDeltaTime, m_ChargeSeconds);
        if (previous > 0 && Progress == 0) m_Game.RecordReadinessEvent("readiness_reset");
        else if (previous == 0 && Progress > 0) m_Game.RecordReadinessEvent("readiness_charge_started");
        m_UI.SetReadinessProgress(Progress);
        if (!complete) return;
        Completed = true; // Remain in readiness until the opening direction finishes.
        m_Game.RecordReadinessEvent("readiness_completed");
        m_Game.BeginOpeningDirection(m_Game.CurrentObjectiveIndex);
    }
}
