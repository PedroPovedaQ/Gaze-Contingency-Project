using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    string m_ParticipantName="",m_RunCsvFolder="",m_RunCsvStatus="",m_StudyDataFolder="";
    bool m_ChooseStudyData,m_RunCsvRequested;
    Process m_RunCsvProcess;
    bool RunCsvBusy=>m_RunCsvProcess!=null;
    string ParticipantId=>m_Timeline?.Trials.FirstOrDefault(t=>!string.IsNullOrEmpty(t.participantCode))?.participantCode
        ??m_Timeline?.Header.participantCode??"";
    string NameKey=>"ParticipantName:"+Uri.EscapeDataString(ParticipantId);

    void InitializeRunExport()
    {
        m_ParticipantName=PlayerPrefs.GetString(NameKey,"");
        m_RunCsvFolder="";m_StudyDataFolder="";m_RunCsvStatus="";
        m_Evaluation=null;m_Judge=null;m_JudgeFolder="";m_JudgeStatus="";m_EvaluationError="";m_JudgeRequested=false;m_EvaluationFolder="";
        InitializeCatalog();
        m_RunCsvRequested=true;
    }
    void UpdateRunExport()
    {
        if(!m_RunCsvRequested||RunCsvBusy||m_Loading||m_Timeline==null)return;
        if(m_ViewLive && (m_Timeline.Records.LastOrDefault()?.kind!="end" ||
            !ReceiverConnected || m_LiveStatus.pendingBytes>0 || m_LiveStatus.verifiedFiles<m_LiveStatus.files))return;
        m_RunCsvRequested=false;
        StartCoroutine(GenerateRunCsv());
    }
    IEnumerator GenerateRunCsv()
    {
        var timeline=m_Timeline;string participantName=m_ParticipantName,expectedHash=m_ViewLive?null:m_Hash;
        string assignedId=AssignedParticipantId,datasetId=m_Dataset?.id??"",datasetName=DatasetName;
        m_RunCsvStatus="Generating condensed, round and full-run CSVs…";
        string output=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents","Trial Review Exports","Runs");
        var process=new Process{StartInfo=new ProcessStartInfo("/usr/bin/python3",
            Argument(Path.Combine(Application.streamingAssetsPath,"export_run.py"))+" --recording "+Argument(timeline.FilePath)+
            " --output "+Argument(output)+" --name "+Argument(participantName)+
            " --assigned-id "+Argument(assignedId)+" --dataset-id "+Argument(datasetId)+" --dataset-name "+Argument(datasetName)+
            (string.IsNullOrEmpty(m_StudyDataFolder)?"":" --run "+Argument(m_StudyDataFolder)))
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        try{process.Start();m_RunCsvProcess=process;}
        catch(Exception error){process.Dispose();m_RunCsvStatus="CSV export: "+error.Message;yield break;}
        while(!process.HasExited)yield return null;
        string folder=process.StandardOutput.ReadToEnd().Trim(),errorText=process.StandardError.ReadToEnd().Trim();
        int code=process.ExitCode;process.Dispose();m_RunCsvProcess=null;
        if(m_Timeline!=timeline||m_ParticipantName!=participantName||AssignedParticipantId!=assignedId||DatasetName!=datasetName)yield break;
        if(code!=0)
        {
            m_RunCsvStatus="CSV export: "+errorText;
            // Guidance depends on the recording itself, not a complete legacy
            // schedule/summary pair. Keep the CSV failure visible and independent.
            yield return GenerateGuidanceEvaluation(timeline);yield break;
        }
        try
        {
            string hash=TrialReviewSummary.Hash(timeline.FilePath);
            if(expectedHash!=null&&hash!=expectedHash)throw new IOException("Recording changed since opening; reopen its stopped copy.");
            var summary=new TrialReviewSummary(folder,timeline,hash);
            AttachGeneratedFiles(folder);
            m_RunCsvFolder=folder;m_RunCsvStatus="Ready: condensed whiteboard CSV, full run and per-round CSVs.";
            LoadEvaluation(folder);
            if(!m_ViewLive){m_Hash=hash;m_Summary=summary;m_ExportPath=folder;}
        }
        catch(Exception error){m_RunCsvStatus="CSV validation: "+error.Message;}
    }
    void RunExportControls()
    {
        GUILayout.Label("PARTICIPANT & RUN FILES",m_Heading);
        GUILayout.Label(string.IsNullOrEmpty(m_ParticipantName)?AssignedParticipantId:m_ParticipantName+" · "+AssignedParticipantId,m_Label);
        if(Button("Edit participant & dataset…"))OpenDatasetPanel();
        GUILayout.Label(m_ViewLive&&m_RunCsvRequested?"CSV generation waits for run end and verified file transfer.":m_RunCsvStatus,m_MutedLabel);
        string condensed=Path.Combine(m_RunCsvFolder,"trials_condensed.csv");
        if(!string.IsNullOrEmpty(m_RunCsvFolder)&&File.Exists(condensed)&&Button("Open condensed CSV"))
            Application.OpenURL(new Uri(condensed).AbsoluteUri);
        if(!string.IsNullOrEmpty(m_RunCsvFolder)&&Button("Open round + run CSVs"))Application.OpenURL(new Uri(m_RunCsvFolder).AbsoluteUri);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!RunCsvBusy;
        if(Button("Regenerate CSVs")){m_RunCsvRequested=true;m_RunCsvFolder="";}
        if(Button("Choose study data folder…")){Browse(false);m_ChooseStudyData=true;}
        GUI.enabled=enabled;GUILayout.Space(12);
    }
    void StopRunExport()
    {
        if(m_RunCsvProcess==null)return;
        if(!m_RunCsvProcess.HasExited)m_RunCsvProcess.Kill();
        m_RunCsvProcess.Dispose();m_RunCsvProcess=null;
    }
}
