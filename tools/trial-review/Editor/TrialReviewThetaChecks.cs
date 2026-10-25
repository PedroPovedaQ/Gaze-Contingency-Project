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
}
