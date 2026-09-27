using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>World-fixed, generously padded gaze gate. Never captures a search object.</summary>
[DefaultExecutionOrder(250)]
public sealed class TrialReadinessGate : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float m_ChargeSeconds = 1f;
    [SerializeField, Min(0f)] float m_PaddingMetres = 0.15f;
    readonly StudyReadinessCharge m_Charge = new();
    readonly List<InputDevice> m_Eyes = new();
    XRGazeInteractor m_Gaze;
    FindObjectGameManager m_Game;
    FindObjectUI m_UI;
    InputDevice m_Eye;
    float m_NextResolve;
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
        if (Progress > 0) { m_Charge.Reset(); m_Game.RecordReadinessEvent("readiness_reset"); }
        ValidInside = false;
        m_UI?.SetReadinessProgress(0);
    }
    public void Cancel()
    {
        ResetCharge(); Active = false; Completed = false; m_UI?.HideFixationCross();
    }
    void Update()
    {
        if (!Active || m_Game == null) return;
        if (Time.unscaledTime >= m_NextResolve)
        {
            if (m_Gaze == null) m_Gaze = FindFirstObjectByType<XRGazeInteractor>();
            if (!m_Eye.isValid)
            {
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, m_Eyes);
                foreach (var eye in m_Eyes) if (eye.isValid) { m_Eye = eye; break; }
            }
            m_NextResolve = Time.unscaledTime + 1f;
        }
        ValidInside = Application.isFocused && m_Gaze != null && m_Gaze.isActiveAndEnabled &&
            m_Eye.isValid && m_Eye.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked &&
            m_UI.IsGazeInsideReadinessCross(m_Gaze.transform.position, m_Gaze.transform.forward, m_PaddingMetres);
        float previous = Progress;
        bool complete = m_Charge.Step(ValidInside, m_Game.ResearcherPaused, m_Game.AnnouncementReady,
            Time.unscaledDeltaTime, m_ChargeSeconds);
        if (previous > 0 && Progress == 0) m_Game.RecordReadinessEvent("readiness_reset");
        else if (previous == 0 && Progress > 0) m_Game.RecordReadinessEvent("readiness_charge_started");
        m_UI.SetReadinessProgress(Progress);
        if (!complete) return;
        Completed = true; Active = false;
        m_Game.RecordReadinessEvent("readiness_completed");
        m_Game.BeginSearch(m_Game.CurrentObjectiveIndex);
    }
}
