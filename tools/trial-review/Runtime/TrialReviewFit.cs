using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    IntakeJob m_CodeJob;
    string m_CheckedCode, m_CodeQuery, m_CodeError, m_AutoCode;
    bool m_CodeDuplicate, m_CodeReplace, m_CodeDevelopment;
    double m_NextCodeCheck, m_NextFitSync;
    string[] NumberingArchives => new[] { m_LiveOutput, m_LiveStatus?.archive,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Trial Review Live") };
    string IntakeRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Trial Review Intake");

    void RefreshParticipantCode(bool replace)
    {
        if (m_CaptureMode) return;
        if (m_CodeJob != null) { m_CodeReplace |= replace; return; }
        m_CodeReplace = replace; m_CodeQuery = m_IntakeCode; m_CodeDevelopment = m_IntakeDevelopment;
        try
        {
            string request = Path.Combine(Application.temporaryCachePath, "participant-code-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(request, JsonUtility.ToJson(new IntakeRequest { action = "codes", folder = IntakeRoot,
                participantCode = m_IntakeCode, development = m_IntakeDevelopment, catalog = CatalogPath,
                archives = NumberingArchives, headsetSuggestion = m_LiveStatus?.headsetState?.nextParticipantCode,
                knownCodes = new[]{m_LiveStatus?.headsetState?.participantCode} }));
            m_CodeJob = new IntakeJob { action = request, result = Path.ChangeExtension(request, ".result.json"),
                process = Process.Start(new ProcessStartInfo("/usr/bin/python3", Argument(Path.Combine(Application.streamingAssetsPath, "participant_intake.py")) + " --request " + Argument(request)) { UseShellExecute = false, CreateNoWindow = true }) };
        }
        catch (Exception e) { m_CodeError = "Could not check participant codes: " + e.Message; }
    }
    void PollParticipantCodes()
    {
        if (m_CodeJob != null && m_CodeJob.process.HasExited)
        {
            var job = m_CodeJob; m_CodeJob = null; job.process.Dispose();
            try
            {
                var result = JsonUtility.FromJson<IntakeResult>(File.ReadAllText(job.result));
                if (!result.ok) throw new IOException(result.error);
                if (m_CodeDevelopment != m_IntakeDevelopment) { RefreshParticipantCode(true); return; }
                if (m_CodeQuery != m_IntakeCode) { RefreshParticipantCode(false); return; }
                m_CodeError = null;
                if (m_CodeReplace || m_IntakeCode == m_AutoCode)
                { m_IntakeCode = m_AutoCode = result.suggested; m_CodeDuplicate = false; }
                else m_CodeDuplicate = result.duplicate;
                m_CheckedCode = m_IntakeCode;
            }
            catch (Exception e) { m_CodeError = "Numbering unavailable: " + e.Message; }
            finally { File.Delete(job.action); if (File.Exists(job.result)) File.Delete(job.result); }
        }
        if (m_IntakeOpen && m_Intake == null && m_CodeJob == null && Time.realtimeSinceStartupAsDouble >= m_NextCodeCheck)
        { m_NextCodeCheck = Time.realtimeSinceStartupAsDouble + 2; RefreshParticipantCode(false); }
    }
    bool FitLinked => m_Intake != null && m_LiveStatus?.headsetState?.participantSessionId == m_Intake.id &&
        m_LiveStatus.headsetState.participantCode == m_Intake.participantCode &&
        TrialReviewHeadsetState.Fresh(m_LiveStatus.headsetState, ReceiverConnected, UtcSeconds, m_LiveStatus.headsetStateObservedUtc);
    void SyncHeadsetFit()
    {
        if (!FitLinked || m_LiveStatus.headsetState.fit == null || m_Intake.withdrawnUtc > 0 || m_IntakeJob != null ||
            Time.realtimeSinceStartupAsDouble < m_NextFitSync) return;
        m_NextFitSync = Time.realtimeSinceStartupAsDouble + 2;
        if (JsonUtility.ToJson(m_Intake.fit) != JsonUtility.ToJson(m_LiveStatus.headsetState.fit))
            m_IntakeJob = StartIntakeJob("sync_fit");
    }
    void DrawParticipantFit()
    {
        var fit = FitLinked ? m_LiveStatus.headsetState.fit : m_Intake.fit;
        GUILayout.BeginVertical(m_Card);
        GUILayout.Label("HEADSET FIT · AUTOMATIC ESTIMATES", m_Heading);
        DrawSeparation("Headset view separation", fit?.viewSeparation);
        DrawSeparation("Wearer eye-origin separation estimate", fit?.eyeSeparation);
        GUILayout.Label("View separation reflects rendering geometry; physical lens agreement is unverified. Eye origins are an unvalidated estimate, not clinical IPD.", m_MutedLabel);
        string change = fit?.separationChange == "change_observed" ? $"Change observed: {fit.separationChangeMm:+0.00;-0.00;0} mm · {fit.separationChangeUtc}" :
            fit?.separationChange == "no_change_observed" ? "No change observed during sampled setup" : "Insufficient observation to assess change";
        GUILayout.Label(change + ". Auto-IPD status unavailable; this does not establish whether adjustment ran.", m_MutedLabel);
        if (!string.IsNullOrEmpty(fit?.fitWarning)) GUILayout.Label(fit.fitWarning, new GUIStyle(m_Label){normal={textColor=m_Warn}});
        if (fit == null || fit.viewSeparation?.recorded != true || fit.eyeSeparation?.recorded != true)
            GUILayout.Label("Automatic fit value missing. Check headset fit; the session can continue.", new GUIStyle(m_Label){normal={textColor=m_Warn}});
        if (fit?.ipdRecorded == true) GUILayout.Label($"Manual participant IPD: {fit.participantIpdMm:0.##} mm · {fit.ipdSource}", m_MutedLabel);
        if (fit?.headsetIpdRecorded == true) GUILayout.Label($"Manual lens setting: {fit.headsetIpdMm:0.##} mm · researcher readout", m_MutedLabel);
        if (!string.IsNullOrEmpty(fit?.manualRecordedUtc)) GUILayout.Label("Latest manual entry: " + fit.manualRecordedUtc + " · one researcher entry, not a sampled median",m_MutedLabel);
        bool fallback = FitLinked && !string.IsNullOrEmpty(fit?.fitEndedUtc) &&
            (fit.viewSeparation?.recorded != true || fit.eyeSeparation?.recorded != true);
        if (fallback)
        {
            GUILayout.Label("Manual fallback (optional, millimetres)", m_Label);
            if (fit.eyeSeparation?.recorded != true)
            {
                m_IntakeIpd = GUILayout.TextField(m_IntakeIpd, m_Field);
                m_IntakeIpdSource = GUILayout.SelectionGrid(m_IntakeIpdSource, new[]{"Not recorded","Pupillometer","Ruler","Participant report"}, 2, m_Button);
            }
            if (fit.viewSeparation?.recorded != true)
            { GUILayout.Label("Lens setting read from headset",m_MutedLabel); m_IntakeHeadsetIpd = GUILayout.TextField(m_IntakeHeadsetIpd,m_Field); }
            if (Button("Save manual fallback to this headset session")) SendFitFallback();
        }
        if(m_HeadsetCommand?.action=="set_fit_fallback")
            GUILayout.Label(m_LiveStatus?.headsetState?.commandId==m_HeadsetCommand.id ? "Manual fallback: "+m_LiveStatus.headsetState.commandResult : "Manual fallback: waiting for headset acknowledgment",m_MutedLabel);
        GUILayout.EndVertical();
    }
    void DrawSeparation(string label, FitSeparationMeasurement value)
    {
        GUILayout.Label(value?.recorded == true ? $"{label}: {value.millimetres:0.00} mm" : label + ": unavailable", m_Label);
        if (value?.recorded == true) GUILayout.Label($"Median of {value.samples} snapshots / {value.windowSeconds:0.00} s · range {value.rangeMm:0.00} mm\n{value.source} · {value.recordedUtc}",m_MutedLabel);
    }
    void SendFitFallback()
    {
        if (m_CaptureMode) return;
        try
        {
            var state = m_LiveStatus.headsetState;
            if (state.fit?.eyeSeparation?.recorded == true) { m_IntakeIpd="";m_IntakeIpdSource=0; }
            if (state.fit?.viewSeparation?.recorded == true) m_IntakeHeadsetIpd="";
            var command = new HeadsetCommand { id=Guid.NewGuid().ToString("N"), sessionId=state.sessionId,
                action="set_fit_fallback", participantSessionId=m_Intake.id, fit=ReadIntakeFit(),
                observedRevision=state.revision, observedUtc=state.updatedUtc, createdUtc=UtcSeconds };
            string path = Path.Combine(m_LiveOutput,"headset-command.json");
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(command));
            if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
            m_HeadsetCommand=command;
        }
        catch(Exception e) { m_IntakeError="Could not save manual fallback: "+e.Message; }
    }
}
