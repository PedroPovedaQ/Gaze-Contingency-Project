using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class StudyPreparationChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("Tools/Codex/Check Study Preparation")]
    public static void Run()
    {
        ValidationAlignmentChecks.Run();
        StudyGazeAccuracyChecks.Run();
        var host=new GameObject("Preparation readiness checks");host.SetActive(false);
        try
        {
            var preparation=host.AddComponent<StudyGazePreparation>();
            Check(!preparation.Ready,"Fresh preparation cannot launch a trial");
            var type=typeof(StudyGazePreparation);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var receipt=type.GetNestedType("Receipt",BindingFlags.NonPublic);
            type.GetField("m_Result",flags).SetValue(preparation,JsonUtility.FromJson("{\"participant\":\""+SessionConfig.ParticipantId+"\",\"outcome\":\"skipped\"}",receipt));
            Check(!preparation.Ready,"Skip receipt without completed preparation cannot launch");
            type.GetField("<Completed>k__BackingField",flags).SetValue(preparation,true);
            Check(preparation.Ready,"Explicit recorded skip is allowed for the current participant");
            type.GetField("m_Result",flags).SetValue(preparation,JsonUtility.FromJson("{\"participant\":\"different-participant\",\"outcome\":\"skipped\"}",receipt));
            Check(!preparation.Ready,"Preparation cannot carry across participants");
            type.GetField("m_Result",flags).SetValue(preparation,JsonUtility.FromJson("{\"participant\":\""+SessionConfig.ParticipantId+"\",\"outcome\":\"passed\",\"alignmentId\":\"expired\"}",receipt));
            Check(!preparation.Ready,"A historical pass without its live correction cannot launch");
        }
        finally{UnityEngine.Object.DestroyImmediate(host);}
        ReplayRecordedCheck();
        Debug.Log("[StudyPreparationChecks] PASS: calibration UI/guards, accuracy math, explicit skip, participant isolation, stale correction guard.");
    }
    [Serializable] sealed class EventRow {public string eventType;public int step;public double time;}
    static void ReplayRecordedCheck()
    {
        string path=Environment.GetEnvironmentVariable("GAZE_REPLAY_VALIDATION");
        if(string.IsNullOrEmpty(path))return;
        var starts=new double[5];
        foreach(string line in File.ReadLines(Path.Combine(path,"events.jsonl")))
        {var e=JsonUtility.FromJson<EventRow>(line);if(e.eventType=="stimulus_start")starts[e.step]=e.time;}
        var check=new StudyGazeAccuracy();int current=-1;
        foreach(string line in File.ReadLines(Path.Combine(path,"samples.jsonl")))
        {
            var row=JsonUtility.FromJson<GazeObservation>(line);
            if(row.kind!="gaze_observation" || row.validationStage=="alignment")continue;
            int step=row.validationStage=="running"?row.validationStep:-1;
            if(step>=0 && step!=current)
            {if(current>=0)check.EndPoint(current);current=step;check.StartPoint(step,starts[step]);}
            check.Observe(row,step);
        }
        if(current>=0)check.EndPoint(current);
        var result=check.Summarize();
        Check(result.passed && Math.Abs(result.meanDegrees-1.2565161246055834)<.01,"Runtime result matches independent Python audit of P033 recording");
        Check(result.points[0].observations==340 && result.points[4].observations==323,"Runtime and offline observation windows agree");
        Debug.Log("[StudyPreparationChecks] Recorded P033 parity PASS: "+result.meanDegrees+" degrees.");
    }
}
