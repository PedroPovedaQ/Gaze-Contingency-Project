using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>Passive setup metadata. Never changes tracking, calibration, guidance or lens settings.</summary>
public sealed class ParticipantFitCapture : MonoBehaviour
{
    [Serializable] sealed class Sample
    {
        public string utc, captureId;
        public string viewSource, eyeSource = "XR_HTC_eye_tracker_gazePose_position";
        public double receiptSeconds;
        public bool eyesValid, viewValid, viewPlausible, eyePlausible;
        public float viewMm, eyeMm;
    }
    FitSeparationWindow m_View = new FitSeparationWindow(), m_Eye = new FitSeparationWindow();
    FitSeparationChange m_Change = new FitSeparationChange();
    string m_Session, m_LogPath;
    double m_Next, m_NextSave;
    long m_LastSequence = -1;
    bool m_Finished;
    readonly List<XRDisplaySubsystem> m_Displays = new List<XRDisplaySubsystem>();
    string m_ViewSource;
    bool ReadRenderViews(out float mm)
    {
        mm = 0;
        var camera = Camera.main;
        if (camera == null || !camera.stereoEnabled || Vector3.Distance(camera.transform.lossyScale, Vector3.one) > .001f) return false;
        SubsystemManager.GetSubsystems(m_Displays);
        Vector3 first = default; int count = 0;
        foreach (var display in m_Displays)
        {
            if (!display.running) continue;
            for (int p = 0; p < display.GetRenderPassCount(); p++)
            {
                display.GetRenderPass(p, out var pass);
                for (int i = 0; i < pass.GetRenderParameterCount(); i++)
                {
                    pass.GetRenderParameter(camera, i, out var parameter);
                    Vector3 origin = parameter.view.inverse.MultiplyPoint3x4(Vector3.zero);
                    if (count++ == 0) first = origin;
                    else { mm = Vector3.Distance(first, origin) * 1000; return true; }
                }
            }
        }
        return false;
    }
    public void Finish()
    {
        if (m_Finished || string.IsNullOrEmpty(m_Session)) return;
        m_Finished = true;
        SessionConfig.ParticipantFit.fitEndedUtc = DateTime.UtcNow.ToString("O");
        Save();
    }
    void LateUpdate()
    {
        string session = SessionConfig.DesktopSessionId;
        if (string.IsNullOrEmpty(session)) return;
        if (session != m_Session)
        {
            m_Session = session; m_Finished = false; m_LastSequence = -1; m_Change = new FitSeparationChange();
            m_View = new FitSeparationWindow(); m_Eye = new FitSeparationWindow();
            var fit = SessionConfig.ParticipantFit;
            fit.fitCaptureId = Guid.NewGuid().ToString("N"); fit.fitStartedUtc = DateTime.UtcNow.ToString("O");
            fit.fitEndedUtc = null; fit.viewSeparation = fit.eyeSeparation = null;
            fit.separationChange = "insufficient_observation"; fit.separationChangeMm = 0;
            fit.separationChangeUtc = fit.separationChangeSource = null; fit.setupSampleCount = 0;
            m_LogPath = Path.Combine(SessionConfig.RootPath, SessionConfig.ParticipantId, "fit-setup-" + fit.fitCaptureId + ".jsonl");
        }
        if (m_Finished) return;
        if (GetComponent<VoiceModeSelector>()?.IsComplete == true) { Finish(); return; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < m_Next) return;
        m_Next = now + .05; // At most 20 acquisition snapshots/s, not claimed sensor samples.
        var observation = SharedGazeStream.Instance?.Latest;
        bool valid = Application.isFocused && observation != null && observation.focused && observation.headValid &&
            observation.htcGazeAvailable && observation.leftValid && observation.rightValid && observation.sequence != m_LastSequence &&
            now - observation.htcQueryReceiptTime >= 0 && now - observation.htcQueryReceiptTime < .1;
        if (observation != null) m_LastSequence = observation.sequence;
        float eyeMm = valid ? Vector3.Distance(observation.leftNativeOrigin, observation.rightNativeOrigin) * 1000 : 0;
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        Vector3 left = default, right = default;
        if (head.TryGetFeatureValue(CommonUsages.userPresence, out bool present) && !present) valid = false;
        bool views = Application.isFocused && head.isValid && head.TryGetFeatureValue(CommonUsages.leftEyePosition, out left) &&
            head.TryGetFeatureValue(CommonUsages.rightEyePosition, out right);
        float viewMm = views ? Vector3.Distance(left, right) * 1000 : 0;
        string source = "Unity_OpenXR_head_leftEyePosition_rightEyePosition";
        if (Application.isFocused && (!views || !FitSeparationWindow.Plausible(viewMm)))
        { views = ReadRenderViews(out viewMm); source = "Unity_XRDisplaySubsystem_render_view_matrices"; }
        if (source != m_ViewSource) { m_View = new FitSeparationWindow(); m_ViewSource = source; }
        string utc = DateTime.UtcNow.ToString("O");
        var f = SessionConfig.ParticipantFit;
        var view = m_View.Add(now, valid ? viewMm : 0, source, utc);
        var eye = m_Eye.Add(now, valid ? eyeMm : 0, "XR_HTC_eye_tracker_gazePose_position", utc);
        if (view != null)
        {
            f.viewSeparation = view;
            m_Change.Observe(f, view, now);
        }
        if (eye != null) f.eyeSeparation = eye;
        f.setupSampleCount++;
        f.fitWarning = !valid ? "Eye tracking unavailable; automatic fit values may be missing or earlier setup readings." :
            !FitSeparationWindow.Plausible(viewMm) || !FitSeparationWindow.Plausible(eyeMm) ? "Missing or implausible separation; inspect fit or use manual fallback." :
            view == null || eye == null ? "Waiting for a stable two-second window; earlier readings, if any, are retained." : "";
        try
        {
            File.AppendAllText(m_LogPath, JsonUtility.ToJson(new Sample { utc = utc, captureId = f.fitCaptureId,
                receiptSeconds = now, eyesValid = valid, viewValid = views, viewSource = source,
                viewPlausible = FitSeparationWindow.Plausible(viewMm), eyePlausible = FitSeparationWindow.Plausible(eyeMm),
                eyeMm = float.IsNaN(eyeMm) || float.IsInfinity(eyeMm) ? 0 : eyeMm,
                viewMm = float.IsNaN(viewMm) || float.IsInfinity(viewMm) ? 0 : viewMm }) + "\n");
            if (now >= m_NextSave) { m_NextSave = now + .5; Save(); }
        }
        catch (IOException) { f.fitWarning = "Fit log could not be saved; session may continue."; }
        catch (UnauthorizedAccessException) { f.fitWarning = "Fit log permission denied; session may continue."; }
    }
    void Save()
    {
        try { SessionConfig.SaveParticipantFit(); }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { SessionConfig.ParticipantFit.fitWarning = "Fit record could not be saved; session may continue."; }
    }
}
