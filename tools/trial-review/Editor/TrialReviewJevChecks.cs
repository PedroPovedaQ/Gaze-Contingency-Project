using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class TrialReviewJevChecks
{
    static void Check(bool value,string message) { if(!value)throw new Exception("Jev viewer: "+message); }
    public static TrialReplayRecord Fixture(string id,string trial,bool response,double time)
    {
        var payload = new JObject {
            ["model"]="typesafe-ai/jev", ["state"]=new JObject { ["valid"]=true,["insideTargetWall"]=false,["yawErrorDegrees"]=90 },
            ["questions"]=new JObject { ["navigation_phrase"]=new JObject { ["type"]="choice", ["criteria"]=new JObject {
                ["Turn right."]="Direct",["Turn to your right."]="Casual",["A little more right."]="Small correction",
                ["Keep turning right."]="Continuation",["Look farther right."]="Larger correction" } } } };
        var data = response ? new JObject { ["request_id"]=id,["stage"]="response",["status"]="ok",["elapsed_ms"]=224,
            ["answers"]=new JObject { ["navigation_phrase"]=new JObject { ["type"]="choice",["choice"]="Turn right.",
                ["probabilities"]=new JObject { ["Turn right."]=.6,["Turn to your right."]=.2,["A little more right."]=.05,["Keep turning right."]=.1,["Look farther right."]=.05 } } } }
            : new JObject { ["request_id"]=id,["stage"]="request",["payload"]=payload };
        return new TrialReplayRecord { kind="jev_trace",trialId=trial,time=time,detail="jev;"+data.ToString(Newtonsoft.Json.Formatting.None) };
    }
    public static void Run()
    {
        var index=new TrialReviewJev();
        var request=Fixture("a","trial-a",false,1); index.Observe(request);index.Observe(request);
        Check(index.Decisions.Count==1 && index.Decisions[0].Status=="pending","request deduplicated, pending not success");
        index.Observe(Fixture("a","trial-b",true,1.224));
        var decision=index.Decisions[0];
        Check(decision.TrialId=="trial-a" && decision.Status=="ok","late response stays with originating trial");
        Check(((JObject)decision.Response["answers"]["navigation_phrase"]["probabilities"]).Count==5,"all five probabilities retained");
        index.Observe(new TrialReplayRecord {kind="audio_playback_start",guidanceDecisionId="other",time=1.3,text="Turn right."});
        Check(decision.PlaybackRecord==null,"same text never guesses playback association");
        index.Observe(new TrialReplayRecord {kind="audio_playback_start",guidanceDecisionId="a",time=1.4,text="Turn right."});
        Check(decision.PlaybackRecord.time==1.4,"explicit playback correlation");
        index.Observe(new TrialReplayRecord {kind="jev_trace",detail="jev;{broken"});
        index.Observe(new TrialReplayRecord {kind="transmission_decision",sequence=30,detail="tip;{\"outcome\":\"wait\"}"});
        Check(index.Decisions.Count==2 && index.Decisions[1].Legacy,"legacy preserves unknowns");
        index.Observe(Fixture("b","trial-a",true,2.2));index.Observe(Fixture("b","trial-a",false,2));
        Check(index.Decisions[2].Status=="ok","out of order delivery joined by ID");
        index.Observe(new TrialReplayRecord {kind="jev_trace",detail="jev;{\"request_id\":\"b\",\"stage\":\"application\",\"outcome\":\"discarded_stale\"}"});
        Check(index.Decisions[2].Discarded,"stale distinct from successful response");
        index.Observe(Fixture("c","trial-a",false,3));
        index.Observe(new TrialReplayRecord {kind="jev_trace",detail="jev;{\"request_id\":\"c\",\"stage\":\"response\",\"status\":\"timeout\",\"elapsed_ms\":450}"});
        Check(index.Decisions[3].Status=="timeout" && index.Decisions[3].PlaybackRecord==null,"timeout is not speech");
        index.Clear();Check(index.Decisions.Count==0,"switching run clears index");
        Debug.Log("TRIAL_REVIEW_JEV_PASS candidate/probability preservation, playback IDs, legacy, malformed, stale and late responses");
    }
}
