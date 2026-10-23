using System;
using Newtonsoft.Json.Linq;
using SpatialVoice;
partial class SpatialVoiceChecks
{
    static void CheckActionMenu()
    {
        Check(!NavigationActionMenu.CanUse(false,"Direction",true,false,false,true,true), "participant policy is never experimental");
        foreach(var action in new[]{"ConfirmGaze","ConfirmPointing","SelectionReminder","Arrival","Progress","None"})
            Check(!NavigationActionMenu.CanUse(true,action,true,false,false,true,true),"local priority stays local: "+action);
        Check(!NavigationActionMenu.CanUse(true,"Direction",true,false,true,true,true),"controller target blocks directions before hold qualifies");
        Check(!NavigationActionMenu.CanUse(true,"Direction",false,false,false,true,true),"invalid tracking blocks requests");
        Check(!NavigationActionMenu.CanUse(true,"Direction",true,true,false,true,true),"target gaze blocks search");
        Check(!NavigationActionMenu.CanUse(true,"Direction",true,false,false,true,false),"busy/paused gate blocks requests");
        var still=NavigationActionMenu.Build("Direction","Look right.","Look right.","Keep going right.",false,false,_=>true);
        Check(still.Count==1 && still[0].id=="orient","stationary user is not encouraged to keep moving");
        var moving=NavigationActionMenu.Build("Direction","Look right.","Look right.","Keep going right.",false,true,_=>true);
        Check(moving.Count==2 && moving[1].id=="encourage","measured progress offers meaningful action choice");
        var returning=NavigationActionMenu.Build("ReturnTarget","Go back down.","Look down.","Keep looking down.",true,true,_=>true);
        Check(returning.Count==2 && returning[0].id=="correct" && returning[1].id=="refine","verified return can correct or refine; no misleading encouragement");
        Check(NavigationActionMenu.Build("Direction","Look right.","Look right.","Keep going right.",false,true,_=>false).Count==0,"unprepared audio is never offered");
        var payload=JObject.Parse(JevDecisionClient.ActionPayload("{\"runtime\":{}}",moving));
        Check(payload["questions"]["navigation_action"]["criteria"]["wait"]!=null,"wait is explicit, not fake speech");
        Check(JevDecisionClient.ParseAction("{\"answers\":{\"navigation_action\":{\"type\":\"choice\",\"choice\":\"encourage\"}}}",moving).Phrase.text=="Keep going right.","selection resolves exactly to offered prepared phrase");
        Check(!JevDecisionClient.ParseAction("{\"answers\":{\"navigation_action\":{\"type\":\"choice\",\"choice\":\"wait\"}}}",moving).Speak,"wait cannot emit a phrase");
        Throws<FormatException>(()=>JevDecisionClient.ParseAction("{\"answers\":{\"navigation_action\":{\"type\":\"choice\",\"choice\":\"correct\"}}}",moving));
        var progress=new GuidanceTurnState();
        progress.MarkSpoken(DirectionalHintPolicy.RightPhrase,90,0);
        progress.Observe(true,DirectionalHintPolicy.RightPhrase,90,3.0);
        progress.Observe(true,DirectionalHintPolicy.RightPhrase,86,3.1);
        Check(progress.BriefProgressAllowed(DirectionalHintPolicy.RightPhrase,3.1),"fresh measured progress qualifies encouragement");
        progress.Observe(true,DirectionalHintPolicy.RightPhrase,89,3.2);
        Check(!progress.BriefProgressAllowed(DirectionalHintPolicy.RightPhrase,3.2),"reversal withdraws encouragement before playback");
        progress.Observe(false,null,0,3.3);
        progress.Observe(true,DirectionalHintPolicy.RightPhrase,84,3.4);
        progress.Observe(true,DirectionalHintPolicy.RightPhrase,80,3.5);
        Check(!progress.BriefProgressAllowed(DirectionalHintPolicy.RightPhrase,3.5),"recovery needs fresh explicit directional context");
        progress.MarkSpoken(DirectionalHintPolicy.RightPhrase,80,3.5);
        Check(progress.BriefProgressAllowed(DirectionalHintPolicy.RightPhrase,3.5),"fresh instruction restores progress context");
        Check(!progress.BriefProgressAllowed(DirectionalHintPolicy.RightPhrase,4.0),"delayed response cannot reuse stale progress");
        Console.WriteLine("PASS development action menus, local overrides, evidence gates, prepared audio, exact typed choices");
    }

    sealed class ActionHttp : System.Net.Http.HttpMessageHandler
    {
        readonly string answer; readonly bool stall;
        public ActionHttp(string answer, bool stall=false) { this.answer=answer;this.stall=stall; }
        protected override async System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken token)
        {
            var body=JObject.Parse(await request.Content.ReadAsStringAsync());
            Check(body["questions"]["navigation_action"]!=null && body["questions"]["transmit"]==null,
                "action and wait are one decision, never two model calls");
            if(stall) await System.Threading.Tasks.Task.Delay(10000,token);
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content=new System.Net.Http.StringContent(answer) };
        }
    }

    static async System.Threading.Tasks.Task CheckActionTransport()
    {
        var choices=NavigationActionMenu.Build("Direction","Look right.","Look right.","Keep going right.",false,true,_=>true);
        var transport=new ActionHttp("{\"answers\":{\"navigation_action\":{\"type\":\"choice\",\"choice\":\"encourage\"}}}");
        using(var http=new System.Net.Http.HttpClient(transport))
        {
            var result=await new JevDecisionClient("test",http).ChooseActionAsync("{\"decision_id\":\"action-1\",\"runtime\":{}}",choices,System.Threading.CancellationToken.None);
            Check(result.Speak && result.Phrase.id=="encourage" && result.RequestId=="action-1","action HTTP preserves decision identity and exact selection");
        }
        using(var http=new System.Net.Http.HttpClient(new ActionHttp("{}",stall:true)))
        using(var cancellation=new System.Threading.CancellationTokenSource())
        {
            cancellation.CancelAfter(20);
            bool cancelled=false;
            try { await new JevDecisionClient("test",http).ChooseActionAsync("{\"runtime\":{}}",choices,cancellation.Token); }
            catch(OperationCanceledException) { cancelled=true; }
            Check(cancelled,"cancelled action request cannot deliver late advice");
        }
        using(var http=new System.Net.Http.HttpClient(new ActionHttp("{\"answers\":{\"navigation_action\":{\"type\":\"choice\",\"choice\":\"invented\"}}}")))
        {
            bool rejected=false;
            try { await new JevDecisionClient("test",http).ChooseActionAsync("{\"runtime\":{}}",choices,System.Threading.CancellationToken.None); }
            catch(FormatException) { rejected=true; }
            Check(rejected,"unoffered HTTP action is rejected before scheduling");
        }
        Console.WriteLine("PASS action HTTP identity, cancellation and invalid-response rejection");
    }

    static async System.Threading.Tasks.Task BenchmarkActions(string keys, string output)
    {
        if (System.IO.File.Exists(output)) throw new InvalidOperationException("Do not overwrite pilot evidence");
        var client = new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(keys))["jev_key"]);
        var rows = new JArray();
        var cases = new[]{
            ("right_progress","Direction",DirectionalHintPolicy.RightPhrase,false,true,90f,0f,4.0),
            ("left_progress","Direction",DirectionalHintPolicy.LeftPhrase,false,true,-90f,0f,4.0),
            ("up_progress","Direction",DirectionalHintPolicy.UpPhrase,true,true,0f,20f,4.0),
            ("down_progress","Direction",DirectionalHintPolicy.DownPhrase,true,true,0f,-20f,4.0),
            ("diagonal_progress","Direction",DirectionalHintPolicy.DownLeftPhrase,true,true,-15f,-20f,4.0),
            ("return_after_ack","ReturnTarget",DirectionalHintPolicy.DownPhrase,true,false,0f,-20f,1.5),
            ("stationary","Direction",DirectionalHintPolicy.RightPhrase,false,false,90f,0f,4.0),
            ("recent_repeat","Direction",DirectionalHintPolicy.RightPhrase,false,false,90f,0f,0.4),
            ("left_stationary_inside","Direction",DirectionalHintPolicy.LeftPhrase,true,false,-12f,0f,5.0),
            ("return_up_right","ReturnTarget",DirectionalHintPolicy.UpRightPhrase,true,false,15f,20f,1.5)
        };
        for(int repeat=0;repeat<2;repeat++) foreach(var c in cases)
        {
            string direct=DirectionalHintPolicy.Variant(c.Item3,0,verticalFraction:.3f);
            string primary=c.Item2=="ReturnTarget"?DirectionalHintPolicy.ReturnToTarget(c.Item3):direct;
            var choices=NavigationActionMenu.Build(c.Item2,primary,direct,DirectionalHintPolicy.Progress(c.Item3,0),c.Item4,c.Item5,_=>true);
            // Same bounded history serializer used by the headset; no invented model memory.
            var memory = new SpeechDecisionMemory();
            var snapshot = new GuidanceStateSnapshot { phase = c.Item4 ? "Refining" : "Turning",
                allowedAction = c.Item2, capturedAtSeconds = 100, newGuidanceAllowed = true };
            memory.Observe(snapshot);
            memory.Started(c.Item2=="ReturnTarget" ? "Yep, that's it!" : direct, "tip", 99-c.Rest.Item1, snapshot);
            memory.Ended(100-c.Rest.Item1, false);
            var runtime=new JObject {
                ["evidence"]=new JObject { ["valid"]=true,["geometryAvailable"]=true,["insideTargetWall"]=c.Item4,
                    ["yawErrorDegrees"]=c.Item6,["pitchErrorDegrees"]=c.Item7,
                    ["controllerOnTarget"]=false,["targetGazeQualified"]=false,
                    ["targetReturnPending"]=c.Item2=="ReturnTarget",["targetAcknowledged"]=c.Item2=="ReturnTarget",
                    ["guidanceState"]=new JObject { ["phase"]=c.Item4?"Refining":"Turning",["allowedAction"]=c.Item2,["newGuidanceAllowed"]=true } },
                ["playback"]=new JObject { ["is_speaking"]=false,["is_busy"]=false,
                    ["recent_history"]=memory.Snapshot(100) },
                ["seconds_since_speech_ended"]=c.Rest.Item1,["locally_qualified"]=true
            };
            foreach(string arm in repeat==0?new[]{"baseline","actions"}:new[]{"actions","baseline"})
            {
                var state=new JObject { ["schema"]="development-action-v1",["runtime"]=runtime.DeepClone(),["proposed_phrase"]=primary, ["seconds_since_speech_ended"]=c.Rest.Item1, ["locally_qualified"]=true, ["reason"]=c.Item2=="ReturnTarget" ? "recent_target_departure" : "stable_direction" };
                var timer=System.Diagnostics.Stopwatch.StartNew();
                string choice=null,error=null; bool? speak=null;
                try {
                    if(arm=="baseline") { speak=await client.ShouldTransmitAsync(state.ToString(),System.Threading.CancellationToken.None);choice=speak.Value?primary:"wait"; }
                    else { var result=await client.ChooseActionAsync(state.ToString(),choices,System.Threading.CancellationToken.None);speak=result.Speak;choice=result.Speak?result.Phrase.id:"wait"; }
                } catch(Exception e) {error=e.GetType().Name;}
                rows.Add(new JObject { ["case"]=c.Item1,["repeat"]=repeat,["arm"]=arm,["elapsed_ms"]=timer.Elapsed.TotalMilliseconds,
                    ["choice"]=choice,["speak"]=speak,["error"]=error,["state"]=state,
                    ["action_payload"]=arm=="actions" ? JObject.Parse(JevDecisionClient.ActionPayload(state.ToString(),choices)) : null,
                    ["offered"]=JArray.FromObject(choices),["actual_playback"]=false });
            }
        }
        System.IO.File.WriteAllText(output,new JObject { ["measurement"]="Mac HTTP via production C# adapter; synthetic states; no audio or participant behavior",["rows"]=rows }.ToString());
        Console.WriteLine("Saved "+rows.Count+" paired action requests to "+output);
    }
}
