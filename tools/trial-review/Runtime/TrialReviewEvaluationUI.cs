using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    JObject m_Evaluation, m_Judge;
    Vector2 m_EvaluationScroll;
    string m_EvaluationError="", m_JudgeStatus="", m_JudgeFolder="",m_EvaluationFolder="";
    bool m_JudgeRunning,m_JudgeRequested,m_EvaluationGenerating;
    float m_NextJudgePoll,m_EvaluationContentWidth;

    void LoadEvaluation(string folder)
    {
        m_Evaluation=null; m_Judge=null; m_EvaluationError=""; m_JudgeStatus="";
        try
        {
            var path=Path.Combine(folder,"guidance-evaluation.json");
            if(!File.Exists(path))return;
            string currentHash=TrialReviewSummary.Hash(m_Timeline.FilePath);
            if(!m_ViewLive&&!string.IsNullOrEmpty(m_Hash)&&m_Hash!=currentHash)
                throw new IOException("Recording changed since opening; reopen its stopped copy.");
            m_Evaluation=TrialReviewEvaluation.ReadReport(path,currentHash);
            m_EvaluationFolder=folder;
            m_JudgeFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents","Trial Review Evaluations",Path.GetFileName(folder));
            PollJudge();
            m_JudgeRequested=PlayerPrefs.GetInt("AutomaticAstraEvaluation",1)==1;
        }
        catch(Exception error){m_EvaluationError=error.Message;}
    }
    IEnumerator GenerateGuidanceEvaluation(TrialReplayTimeline timeline)
    {
        while(m_EvaluationGenerating)yield return null;
        if(m_Timeline!=timeline)yield break;
        m_EvaluationGenerating=true;
        string script=Path.Combine(Application.streamingAssetsPath,"Analysis","guidance_eval.py");
        string folder;
        Process process=null;
        System.Threading.Tasks.Task<string> stdout=null,stderr=null;
        try
        {
            string hash=TrialReviewSummary.Hash(timeline.FilePath);
            string version=TrialReviewSummary.Hash(script).Substring(0,16);
            folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents","Trial Review Exports","Guidance",hash+"_"+version);
            process=new Process{StartInfo=new ProcessStartInfo("/usr/bin/python3",Argument(script)+
                " --recording "+Argument(timeline.FilePath)+" --out "+Argument(folder))
                {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
            process.Start();stdout=process.StandardOutput.ReadToEndAsync();stderr=process.StandardError.ReadToEndAsync();
        }
        catch(Exception error)
        {process?.Dispose();m_EvaluationGenerating=false;m_EvaluationError="Guidance export: "+error.Message;yield break;}
        while(!process.HasExited)yield return null;
        while(!stdout.IsCompleted||!stderr.IsCompleted)yield return null;
        int exit=process.ExitCode;process.Dispose();m_EvaluationGenerating=false;
        if(m_Timeline!=timeline)yield break;
        if(exit!=0){m_EvaluationError="Guidance export failed; original recording is unchanged.";yield break;}
        LoadEvaluation(folder);
        if(m_Catalog!=null&&m_Dataset!=null)CatalogAction(()=>m_Catalog.AttachFolderFiles(m_Dataset,folder));
    }
    void UpdateEvaluation()
    {
        // A bounded judge can outlive its viewer. Reopening must keep following
        // that worker's atomic progress report, even without owning its process.
        if(m_Evaluation!=null&&(string)m_Judge?["status"]=="pending"&&Time.unscaledTime>=m_NextJudgePoll)
        {m_NextJudgePoll=Time.unscaledTime+1;PollJudge();}
        if(!m_JudgeRequested||m_JudgeRunning||m_Evaluation==null)return;
        m_JudgeRequested=false;
        StartCoroutine(EvaluateWithAstra(m_EvaluationFolder));
    }

    IEnumerator EvaluateWithAstra(string folder)
    {
        if(m_JudgeRunning||m_Evaluation==null)yield break;
        if((int?)m_Evaluation["summary"]?["benchmark_cases"]==0)
        {m_JudgeStatus="No recorded model requests to judge.";yield break;}
        m_JudgeRunning=true;
        string output=m_JudgeFolder;
        var process=new Process { StartInfo=new ProcessStartInfo("/usr/bin/python3",
            Argument(Path.Combine(Application.streamingAssetsPath,"Analysis","guidance_codex_judge.py"))+" "+
            Argument(Path.Combine(folder,"guidance-benchmark.jsonl"))+" "+Argument(output)+
            " --evaluation "+Argument(Path.Combine(folder,"guidance-evaluation.json")))
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        System.Threading.Tasks.Task<string> stdout=null,stderr=null;
        try
        {
            process.Start();
            stdout=process.StandardOutput.ReadToEndAsync();
            stderr=process.StandardError.ReadToEndAsync();
        }
        catch(Exception error)
        {process.Dispose();m_JudgeRunning=false;m_JudgeStatus="Could not start Astra evaluation: "+error.Message;yield break;}
        m_JudgeStatus="Astra evaluation pending · uses your signed-in Codex subscription.";
        while(!process.HasExited)
        {
            if(output==m_JudgeFolder&&Time.unscaledTime>=m_NextJudgePoll)
            {m_NextJudgePoll=Time.unscaledTime+1;PollJudge();}
            yield return null;
        }
        while(!stdout.IsCompleted||!stderr.IsCompleted)yield return null;
        int exit=process.ExitCode;process.Dispose();m_JudgeRunning=false;
        // A previously opened run may finish in the background; never display its
        // result against the new run or attach it to the new dataset.
        if(output!=m_JudgeFolder)yield break;
        PollJudge();
        if(exit!=0&&(string)m_Judge?["status"]=="pending")
            m_JudgeStatus="Saved evaluation is pending. Use Run / resume if progress stops.";
        else if(exit!=0)m_JudgeStatus="Evaluation did not finish successfully. See the saved status and retry when available.";
        else m_JudgeStatus="Astra evaluation finished. Review judgments and unavailable evidence below.";
        if(m_Catalog!=null&&m_Dataset!=null&&Directory.Exists(output))
            CatalogAction(()=>m_Catalog.AttachFolderFiles(m_Dataset,output));
    }

    void PollJudge()
    {
        try
        {
            string path=Path.Combine(m_JudgeFolder,"guidance-codex-judge.json");
            if(!File.Exists(path))return;
            var report=TrialReviewEvaluation.ReadJson(path);
            if((string)report["schema"]!="guidance-codex-judge-v1")
                throw new InvalidDataException("Unsupported judge report.");
            string benchmark=Path.Combine(m_EvaluationFolder,"guidance-benchmark.jsonl");
            string evaluation=Path.Combine(m_EvaluationFolder,"guidance-evaluation.json");
            if((string)report["benchmark_sha256"]!=TrialReviewSummary.Hash(benchmark)||
                (string)report["evaluation_sha256"]!=TrialReviewSummary.Hash(evaluation))
                throw new InvalidDataException("Judge report does not match this evaluation package.");
            bool wasPending=(string)m_Judge?["status"]=="pending";
            m_Judge=report;
            if(wasPending&&(string)report["status"]!="pending")
                m_JudgeStatus=(string)report["status"]=="success"?
                    "Astra evaluation finished. Review judgments and unavailable evidence below.":
                    "Evaluation did not finish successfully. See the saved status and retry when available.";
        }
        catch(Exception error){m_Judge=null;m_JudgeStatus="Judge result unavailable: "+error.Message;}
    }

    void EvaluationInspector(TrialReplayRecord trial,float width)
    {
        m_EvaluationScroll=GUILayout.BeginScrollView(m_EvaluationScroll,false,true);
        m_EvaluationContentWidth=Mathf.Max(170,width-40);
        GUILayout.BeginVertical(GUILayout.Width(m_EvaluationContentWidth));
        GUILayout.Label("GUIDANCE EVALUATION",m_Heading);
        GUILayout.Label("Post-run evidence checks and model judgments. Neither is a participant rating or proof of accurate eye tracking.",m_MutedLabel);
        bool automatic=PlayerPrefs.GetInt("AutomaticAstraEvaluation",1)==1;
        bool next=GUILayout.Toggle(automatic,"Auto-evaluate with Astra");
        if(next!=automatic)
        {PlayerPrefs.SetInt("AutomaticAstraEvaluation",next?1:0);PlayerPrefs.Save();m_JudgeRequested=next;}
        if(!string.IsNullOrEmpty(m_EvaluationError))GUILayout.Label(m_EvaluationError,m_Label);
        if(m_Evaluation==null)
        {
            GUILayout.Label(m_EvaluationGenerating?"Processing recorded guidance…":
                "Evaluation becomes available after processing the recording. Legacy evidence may be unavailable.",m_Label);
            RunExportControls();GUILayout.EndVertical();GUILayout.EndScrollView();return;
        }
        var summary=m_Evaluation["summary"];
        GUILayout.Space(10);GUILayout.Label("AUTOMATIC CHECKS · FULL RUN",m_Heading);
        EvaluationMetric("Model requests",Text(summary?["jev_requests"]));
        EvaluationMetric("State coverage",Text(summary?["central_state_requests"])+" requests with recorded central state");
        foreach(string key in new[]{"choice_checks","geometry_checks"})
        {
            var checks=summary?[key];
            EvaluationMetric(key=="choice_checks"?"Choice contract":"Direction sign",
                $"{Text(checks?["pass"])} pass · {Text(checks?["fail"])} fail · {Text(checks?["unknown"])} unknown");
        }
        EvaluationMetric("Response median",Seconds(summary?["request_to_response_seconds"]?["median"]));
        EvaluationMetric("Playback median",Seconds(summary?["request_to_playback_seconds"]?["median"]));
        EvaluationMetric("Confirmation sequence median",Seconds(summary?["qualification_to_playback_seconds"]?["median"]));
        EvaluationMetric("Confirmation sequences over review budget",Text(summary?["confirmation_latency_reviews"]?["over_budget"]));
        GUILayout.Label("Software timing, not acoustic onset. Choice membership is not an accuracy score.",m_MutedLabel);
        GUILayout.Space(10);GUILayout.Label("ASTRA REVIEW",m_Heading);
        EvaluationMetric("Status",m_Judge==null?"Not evaluated":Text(m_Judge["status"]));
        if(m_Judge!=null){EvaluationMetric("Requested model",Text(m_Judge["requested_model"]));EvaluationMetric("Rubric",Text(m_Judge["rubric_version"]));}
        if(m_Judge!=null)EvaluationMetric("Batches",Text(m_Judge["completed_batches"])+" / "+Text(m_Judge["total_batches"]));
        GUILayout.Label(m_JudgeStatus,m_MutedLabel);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!m_JudgeRunning;
        if(Button("Run / resume Astra",GUILayout.Width(m_EvaluationContentWidth)))StartCoroutine(EvaluateWithAstra(m_EvaluationFolder));
        GUI.enabled=enabled;
        if(Button("Open evaluation files",GUILayout.Width(m_EvaluationContentWidth)))Application.OpenURL(new Uri(m_EvaluationFolder).AbsoluteUri);
        if(Directory.Exists(m_JudgeFolder)&&Button("Open Astra results",GUILayout.Width(m_EvaluationContentWidth)))Application.OpenURL(new Uri(m_JudgeFolder).AbsoluteUri);
        if(m_Judge?["batches"] is JArray batches)
            foreach(var batch in batches.Where(b=>(string)b["status"]!="success"))
                GUILayout.Label("Batch "+Text(batch["status"])+": "+Text(batch["reason"]),m_MutedLabel);
        GUILayout.Space(12);GUILayout.Label("SELECTED TRIAL · HISTORICAL DECISIONS",m_Heading);
        var rows=(m_Evaluation["decisions"] as JArray)?.OfType<JObject>()
            .Where(d=>(string)d["trialId"]==trial?.trialId).ToList();
        if(rows==null||rows.Count==0)GUILayout.Label("No linked decisions recorded for this trial.",m_Label);
        else foreach(var row in rows)
        {
            double time=(double?)row["time"]??0;
            if(Button("Seek to "+Clock(time-(trial?.time??0)),GUILayout.Width(m_EvaluationContentWidth)))
            {m_Playing=false;Seek(time,(long?)row["sequence"]??long.MaxValue);}
            EvaluationMetric("Selected / proposed",Text(row["phrase"]));
            EvaluationMetric("Recorded playback",Text(row["played_phrase"]));
            EvaluationMetric("Provider outcome",Text(row["response_status"]));
            EvaluationMetric("Event",Text(row["event_type"]));
            var episode=row["sequence_context"]?["confirmation_episode"];
            if(episode!=null&&episode.Type!=JTokenType.Null)
            {
                EvaluationMetric("First qualified request → playback",Seconds(episode["qualification_to_playback_seconds"]));
                EvaluationMetric("Sequence outcome",Text(episode["outcome"])+" · "+Text(episode["wait_count"])+" wait decisions");
                EvaluationMetric("Latency review",Text(episode["latency_review"])+" · budget "+Seconds(episode["review_budget_seconds"]));
                GUILayout.Label("Budget is an engineering review target. Timing starts at recorded qualification, not sensor onset. Missing playback remains unknown.",m_MutedLabel);
            }
            if(row["checks"] is JArray checks)foreach(var check in checks)
                GUILayout.Label(new GUIContent(Text(check["name"])+": "+Text(check["status"]),Text(check["detail"])),m_MutedLabel);
            var rating=TrialReviewEvaluation.Rating(m_Judge,(string)row["case_id"]);
            if(rating==null)GUILayout.Label("Astra: not rated / unavailable.",m_MutedLabel);
            else foreach(var dimension in rating.Properties())
            {
                GUILayout.Label(dimension.Name.Replace('_',' ')+": "+Text(dimension.Value["verdict"]),m_Heading);
                GUILayout.Label(Text(dimension.Value["rationale"]),m_Label);
                GUILayout.Label("Evidence: "+dimension.Value["evidence_ids"]?.ToString(Newtonsoft.Json.Formatting.None),m_MutedLabel);
            }
            GUILayout.Space(10);
        }
        GUILayout.EndVertical();GUILayout.EndScrollView();
    }
    void EvaluationMetric(string label,string value)
    {
        GUILayout.Label(label,m_MutedLabel,GUILayout.Width(m_EvaluationContentWidth));
        GUILayout.Label(string.IsNullOrEmpty(value)?"Unavailable":value,m_Label,
            GUILayout.Width(m_EvaluationContentWidth));
        GUILayout.Space(5);
    }
    static string Text(JToken value)=>value==null||value.Type==JTokenType.Null?"Unavailable":value.ToString();
    static string Seconds(JToken value)=>value==null||value.Type==JTokenType.Null?"Unavailable":$"{(double)value:0.000} s";
}

public static class TrialReviewEvaluation
{
    public static JObject ReadJson(string path)
    {
        if(new FileInfo(path).Length>32*1024*1024)throw new InvalidDataException("Evaluation report exceeds the supported size.");
        return JObject.Parse(File.ReadAllText(path));
    }
    public static JObject ReadReport(string path,string recordingHash)
    {
        var report=ReadJson(path);
        if((string)report["schema"]!="guidance-evaluation-v1"||
            (string)report["provenance"]?["recording_sha256"]!=recordingHash)
            throw new InvalidDataException("Evaluation does not reference this exact recording.");
        return report;
    }
    public static JObject Rating(JObject report,string caseId)
    {
        if(report?["batches"] is JArray batches)
            foreach(var batch in batches.Where(b=>(string)b["status"]=="success"))
                if(batch["result"]?["cases"] is JArray cases)
                    foreach(var row in cases)
                        if((string)batch["case_map"]?[(string)row["case_id"]]==caseId)
                            return row["ratings"] as JObject;
        return null;
    }
}
