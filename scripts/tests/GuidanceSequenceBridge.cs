// JSON-lines adapter for offline experiments. Compiles production state/memory;
// audio timing and policy adapters are explicitly simulated by the Python runner.
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpatialVoice;
class GuidanceSequenceBridge
{
    static GuidanceStateMachine m = new GuidanceStateMachine();
    static SpeechDecisionMemory memory = new SpeechDecisionMemory();
    static GuidanceStateMachine.Input input;
    static bool paused, completed;
    static JObject Run(JObject q)
    {
        string op=(string)q["op"]; double now=(double?)q["now"]??0;
        if(op=="reset") { m=new GuidanceStateMachine(); memory.Clear(); paused=completed=false; m.BeginSearch(0); }
        if(op=="step") {
            bool pause=(bool?)q["paused"]??false, done=(bool?)q["selected"]??false;
            bool valid=(bool?)q["valid"]??false;
            float yaw=(float?)q["yaw"]??0, pitch=(float?)q["pitch"]??0;
            bool inside=(bool?)q["inside"]??false;
            string dir=inside?DirectionalHintPolicy.WithinArea(yaw,pitch):m.Turn.Resolve(yaw,false);
            input=new GuidanceStateMachine.Input {now=now, deltaSeconds=(double?)q["dt"]??.05,
                receiptTime=now,observationSequence=(long)Math.Round(now*1000),valid=valid,inside=inside,
                looking=(bool?)q["looking"]??false,controllerValid=(bool?)q["controllerValid"]??true,
                controllerOnTarget=(bool?)q["pointing"]??false,direction=dir,
                verticalFraction=Math.Abs(pitch)/60f,remainingTurn=Math.Abs(yaw),
                history=new GazeTargetHistory.Sample {time=now,inputTime=now,observationSequence=(long)Math.Round(now*1000),
                    deviceId=1,eyeRecordingId="synthetic",yawError=yaw,pitchError=pitch}};
            if(done && !completed) { m.SetLifecycle(GuidancePhase.Completed,"correct_trigger_selection",now); completed=true; }
            else if(!completed && pause) { if(!paused){m.SetLifecycle(GuidancePhase.Paused,"scripted_pause",now);memory.Clear();} }
            else if(!completed) {if(paused)m.ResumeSearch(now); m.Step(input);}
            paused=pause;
            memory.Observe(m.Snapshot(now,input.observationSequence,valid,input.controllerValid));
        }
        if(op=="start") {
            var a=(GuidanceAction)Enum.Parse(typeof(GuidanceAction),(string)q["action"]);
            memory.Started((string)q["text"],"simulated_tip",now,m.Snapshot(now,input.observationSequence,input.valid,input.controllerValid));
            if(((bool?)q["commit"]??true) && m.Allows(a))m.Commit(a,now,input.inside,input.controllerOnTarget,input.direction,input.remainingTurn);
        }
        if(op=="end")memory.Ended(now,(bool?)q["interrupted"]??false);
        var snapshot=m.Snapshot(now,input.observationSequence,input.valid,input.controllerValid,
            input.looking?"target":null,input.looking?"target":null,input.controllerOnTarget?"target":null,"target");
        var result=new JObject { ["state"]=JObject.FromObject(snapshot),["history"]=memory.Snapshot(now),
            ["candidate"]=m.Candidate,["lookingStable"]=m.LookingStable,["pointingStable"]=m.PointingStable,
            ["acknowledged"]=m.TargetAcknowledged,["lastVisit"]=m.LastTargetVisitTime,
            ["progressDue"]=m.Turn.ProgressDue(input.direction,input.remainingTurn)};
        if(op=="check")result["allowed"]=m.AllowsPlayback((long)q["revision"],
            (GuidanceAction)Enum.Parse(typeof(GuidanceAction),(string)q["action"]),input.controllerValid,input.controllerOnTarget);
        return result;
    }
    static int Main() {
        string line; while((line=Console.ReadLine())!=null) {
            try {Console.WriteLine(Run(JObject.Parse(line)).ToString(Formatting.None));}
            catch(Exception e){Console.WriteLine(new JObject {["error"]=e.GetType().Name,["message"]=e.Message}.ToString(Formatting.None));return 1;}
        } return 0;
    }
}
