using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class TrialReviewThetaChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Theta review: "+message);}
    public static void Run(TrialReplayTimeline demo)
    {
        CheckScheduleVersions(demo);
        // Use real parser-validated demo meshes/objects, but isolate the new timeline.
        var header=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(demo.Header));
        header.sequence=0;header.time=0;header.meshes=null;
        var trial=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(demo.Trials[0]));
        trial.sequence=1;trial.time=1;trial.meshes=demo.Meshes.Values.ToArray();trial.round=0;
        var target=trial.objects.First(o=>o.target);
        var snapshot=new TrialReviewTheta.Snapshot {reference="pre_cue_eye_origin_to_readiness_cross",round=0,
            gazeSequence=44,time=100,gazeReceiptTime=99.99,eyeOrigin=new Vector3(10,1,10),
            readinessPosition=new Vector3(10,1,12),targetPosition=new Vector3(12,1.4f,10),
            assignedSignedDegrees=90,realizedSignedDegrees=90,absoluteDegrees=90,assignmentErrorDegrees=0,targetHeightRelativeToCross=.4f};
        var ready=new TrialReplayRecord {kind="readiness_completed",sequence=2,time=2,round=0,trialId=trial.trialId,
            detail=JsonUtility.ToJson(snapshot),changes=new[]{new TrialReplayTransform{id=target.id,position=new Vector3(2,1,0),rotation=target.rotation,scale=target.scale}}};
        var sample=new TrialReplayRecord {kind="sample",sequence=3,time=3,trialId=trial.trialId};
        var next=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(trial));
        next.sequence=4;next.time=4;next.trialId="theta-next-trial";next.meshes=null;
        string root=Path.Combine(Path.GetTempPath(),"trial-review-theta-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file=Path.Combine(root,"recording.jsonl");
            File.WriteAllLines(file,new[]{header,trial,ready,sample,next}.Select(r=>JsonUtility.ToJson(r)));
            var timeline=TrialReplayFile.Read(file);
            timeline.Seek(1.5);Check(timeline.State.ThetaSnapshot==null,"no future snapshot before readiness");
            timeline.Seek(2);Check(TrialReviewTheta.Read(timeline.State.ThetaSnapshot)?.gazeSequence==44,"snapshot available at exact readiness event");
            Check(timeline.State.Transforms[target.id].position==ready.changes[0].position,"finalized positions before first sample");
            timeline.Seek(3);Check(TrialReviewTheta.Read(timeline.State.ThetaSnapshot)?.realizedSignedDegrees==90,"sample does not discard snapshot");
            timeline.Seek(1.5);Check(timeline.State.ThetaSnapshot==null&&timeline.State.Transforms[target.id].position==target.position,"rewind restores initial geometry without future data");
            timeline.Seek(4);Check(timeline.State.ThetaSnapshot==null,"next trial clears prior evidence");
            var tracks=new TrialReviewTracks();tracks.Refresh(timeline);
            Check(tracks.For(timeline.Trials[0]).Events.Any(r=>r.kind=="readiness_completed"),"event is navigable");
            string folder=TrialReviewExport.Write(root,timeline,timeline.Trials[0],TrialReviewSummary.Hash(file),null);
            Check(File.ReadAllText(Path.Combine(folder,"pre-cue-theta.jsonl")).TrimEnd()==ready.detail,"exact world-frame JSON exported");
            foreach(string key in new[]{"eyeOrigin","readinessPosition","targetPosition"})
            {
                var json=Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(snapshot));
                json[key]=new Newtonsoft.Json.Linq.JObject();ready.detail=json.ToString();
                Check(TrialReviewTheta.Read(ready)==null,"missing nested coordinates cannot become zeros");
            }
            foreach(string key in new[]{"gazeSequence","time","absoluteDegrees"})
            {
                var json=Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(snapshot));
                json[key]=Newtonsoft.Json.Linq.JValue.CreateNull();ready.detail=json.ToString();
                Check(TrialReviewTheta.Read(ready)==null,"null scalar cannot become zero");
            }
            var overflow=Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(snapshot));
            overflow["eyeOrigin"]["x"]=1e100;ready.detail=overflow.ToString();
            Check(TrialReviewTheta.Read(ready)==null,"float overflow cannot become plausible geometry");
            ready.detail="{}";Check(TrialReviewTheta.Read(ready)==null,"absent fields cannot become zeros");
            ready.detail=null;Check(TrialReviewTheta.Read(ready)==null,"legacy absence remains unavailable");
            ready.changes[0].id="missing-object";
            var validator=new TrialReplayFile.Validator(root);validator.Accept(header);validator.Accept(trial);
            bool rejected=false;try{validator.Accept(ready);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"reject unknown moved target at readiness");
        }
        finally{Directory.Delete(root,true);}
        Debug.Log("TRIAL_REVIEW_THETA_PASS exact snapshot, cursor boundaries, rewind, finalized transforms, legacy absence, export and parser guards");
    }

    static void CheckScheduleVersions(TrialReplayTimeline demo)
    {
        foreach (var version in new[] { ("readiness-target-angle-v7-one-practice",40),
            ("readiness-target-angle-v8-signed-steps-32-one-practice",32) })
        {
            string root=Path.Combine(Path.GetTempPath(),"review-schedule-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var header=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(demo.Header));
                header.sequence=0;header.time=0;header.meshes=null;
                var first=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(demo.Trials[0]));
                first.sequence=1;first.time=1;first.round=version.Item2-1;first.block=0;
                first.trialId="last-first-block";first.scheduleVersion=version.Item1;
                first.meshes=demo.Meshes.Values.ToArray();
                var second=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(first));
                second.sequence=2;second.time=2;second.round=version.Item2;second.block=1;
                second.trialId="first-second-block";second.meshes=null;
                string file=Path.Combine(root,"recording.jsonl");
                File.WriteAllLines(file,new[]{header,first,second}.Select(r=>JsonUtility.ToJson(r)));
                var timeline=TrialReplayFile.Read(file);
                foreach(var trial in timeline.Trials)
                {
                    timeline.Seek(trial.time);
                    Check(timeline.State.Trial.round==trial.round && timeline.State.Trial.block==trial.block,
                        "recorded block boundary survives seek for "+version.Item1);
                    string folder=TrialReviewExport.Write(root,timeline,trial,TrialReviewSummary.Hash(file),null);
                    var exported=JsonUtility.FromJson<TrialReplayRecord>(File.ReadLines(Path.Combine(folder,"observations.jsonl")).First());
                    Check(exported.scheduleVersion==version.Item1 && exported.round==trial.round && exported.block==trial.block,
                        "observation export retains original schedule version and block");
                }
            }
            finally{Directory.Delete(root,true);}
        }
        Debug.Log("TRIAL_REVIEW_SCHEDULE_PASS v7 39/40 and v8 31/32 boundaries retain recorded versions in replay and export");
    }
}
