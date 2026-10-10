using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Export recorded trial observations, without manufacturing performance or questionnaire results.</summary>
public static class TrialReviewExport
{
    [Serializable] sealed class Manifest
    {
        public string schema="trial-review-observations-v1",recordingId,trialId,sourcePath,sourceSha256,exportedUtc;
        public int records;public bool includesMatchedFinalSummary;
    }
    static string Number(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
    static string Cell(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
    static string Row(params string[] cells)=>string.Join(",",cells.Select(Cell));
    static StreamWriter Writer(string path)=>new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write),new UTF8Encoding(false));
    public static string Write(string root,TrialReplayTimeline timeline,TrialReplayRecord trial,string sourceHash,Dictionary<string,string> summary)
    {
        if(timeline==null||trial==null||string.IsNullOrEmpty(sourceHash))throw new InvalidOperationException("Open a stopped saved recording before exporting.");
        if(!timeline.Trials.Contains(trial))throw new InvalidDataException("Trial does not belong to this recording.");
        if(TrialReviewSummary.Hash(timeline.FilePath)!=sourceHash)throw new IOException("Source changed since it was opened. Reopen the stopped recording.");
        int index=timeline.Trials.IndexOf(trial);
        long end=index+1<timeline.Trials.Count?timeline.Trials[index+1].sequence:long.MaxValue;
        var records=timeline.Records.Where(r=>r.sequence>=trial.sequence&&r.sequence<end).ToArray();
        string folder=Path.Combine(root,$"trial-{trial.round+1:00}-{DateTime.UtcNow:yyyyMMddTHHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        using(var output=Writer(Path.Combine(folder,"observations.jsonl")))
            foreach(var record in records)output.WriteLine(JsonUtility.ToJson(record));
        using(var output=Writer(Path.Combine(folder,"samples.csv")))
        {
            output.WriteLine(Row("sequence","session_time_s","trial_elapsed_s","kind","gaze_available","gaze_tracked","head_tracked","gaze_x","gaze_y","gaze_z","gaze_dx","gaze_dy","gaze_dz","head_x","head_y","head_z","right_tracked","right_trigger_held","right_hit","gaze_hit","raw_record_json"));
            foreach(var r in records.Where(r=>r.kind=="sample"||r.kind=="selection"&&r.schema>=2))
                output.WriteLine(Row(r.sequence.ToString(),Number(r.time),Number(r.time-trial.time),r.kind,r.gazeAvailable?"1":"0",r.gazeTracked.ToString(),r.headTracked.ToString(),
                    r.gazeAvailable?Number(r.gazeOrigin.x):"",r.gazeAvailable?Number(r.gazeOrigin.y):"",r.gazeAvailable?Number(r.gazeOrigin.z):"",
                    r.gazeAvailable?Number(r.gazeDirection.x):"",r.gazeAvailable?Number(r.gazeDirection.y):"",r.gazeAvailable?Number(r.gazeDirection.z):"",
                    r.headAvailable?Number(r.headPosition.x):"",r.headAvailable?Number(r.headPosition.y):"",r.headAvailable?Number(r.headPosition.z):"",
                    r.rightController?.tracked.ToString(),r.rightController?.triggerHeld.ToString(),r.rightController?.hoveredId,r.hoveredId,JsonUtility.ToJson(r)));
        }
        using(var output=Writer(Path.Combine(folder,"events.csv")))
        {
            output.WriteLine(Row("sequence","session_time_s","trial_elapsed_s","kind","text","detail","object_id","correct_selection","raw_record_json"));
            foreach(var r in records.Where(r=>r.kind!="sample"&&r.kind!="trial"))
                output.WriteLine(Row(r.sequence.ToString(),Number(r.time),Number(r.time-trial.time),r.kind,r.text,r.detail,r.objectId,r.kind=="selection"?(r.correct?"1":"0"):"",JsonUtility.ToJson(r)));
        }
        using(var output=Writer(Path.Combine(folder,"objects.csv")))
        {
            output.WriteLine(Row("object_id","shape","color","is_target","plane","x","y","z","raw_snapshot_json"));
            foreach(var o in trial.objects??Array.Empty<TrialReplayObject>())output.WriteLine(Row(o.id,o.shape,o.color,o.target?"1":"0",o.planeId.ToString(),Number(o.position.x),Number(o.position.y),Number(o.position.z),JsonUtility.ToJson(o)));
        }
        if(summary!=null)using(var output=Writer(Path.Combine(folder,"final-summary.csv")))
        {output.WriteLine(Row(summary.Keys.ToArray()));output.WriteLine(Row(summary.Values.ToArray()));}
        var manifest=new Manifest{recordingId=timeline.Header.recordingId,trialId=trial.trialId,sourcePath=timeline.FilePath,sourceSha256=sourceHash,exportedUtc=DateTime.UtcNow.ToString("O"),records=records.Length,includesMatchedFinalSummary=summary!=null};
        using(var output=Writer(Path.Combine(folder,"manifest.json")))output.Write(JsonUtility.ToJson(manifest,true));
        using(var output=Writer(Path.Combine(folder,"README.txt")))output.Write(
            "Recorded trial observation export. Times are session elapsed seconds and trial-relative elapsed seconds, not search-only durations.\n"+
            "samples.csv contains replay observations, not the separate full eye-acquisition stream. Gaze validity is retained; no fixation detection is performed.\n"+
            "objects.csv is the initial snapshot; subsequent object changes are preserved inside observations.jsonl and raw_record_json.\n"+
            "This is a data subset, not a standalone replay package: retain the original recording and sibling audio/ folder.\n"+
            (summary==null?"No final performance/subjective CSV was joined. Use the study analysis exporter and load its matching results under Final data.\n":"final-summary.csv contains the previously loaded hash-matched final results, including available survey context.\n"));
        return folder;
    }
}
