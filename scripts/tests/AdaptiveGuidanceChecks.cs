using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SpatialVoice;

partial class SpatialVoiceChecks
{
    static void CheckAdaptiveGuidance()
    {
        foreach (string direction in new[] { DirectionalHintPolicy.LeftPhrase, DirectionalHintPolicy.RightPhrase })
        foreach (float remaining in new[] { 20f, 45f, 60f, 110f })
        {
            string explicitCue = AdaptiveGuidanceGrounding.ExplicitOrientation(direction, remaining);
            Check(explicitCue != DirectionalHintPolicy.Variant(direction, 0), "explicit orientation differs from brief cue");
            Check(new System.Collections.Generic.HashSet<string>(DirectionalHintPolicy.AllPhrases()).Contains(explicitCue), "explicit strategy uses a prepared-library phrase");
        }
        Check(AdaptiveGuidanceGrounding.Opposed(DirectionalHintPolicy.RightPhrase, DirectionalHintPolicy.LeftPhrase), "stable horizontal reversal invalidates old cue");
        Check(!AdaptiveGuidanceGrounding.Opposed(DirectionalHintPolicy.RightPhrase, DirectionalHintPolicy.UpRightPhrase), "adding an axis does not interrupt");
        Check(!AdaptiveGuidanceGrounding.Opposed(null, DirectionalHintPolicy.RightPhrase), "no invented direction for unknown audio");
        Check(AdaptiveGuidanceGrounding.BearingCorrection(DirectionalHintPolicy.UpPhrase, 0, 3.5f, .1f, false) == "Go back up a little.", "bearing correction uses verified crossing axis even inside aligned hysteresis");
        Check(AdaptiveGuidanceGrounding.BearingCorrection(DirectionalHintPolicy.UpPhrase, 0, -3.5f, .1f, false) == null, "obsolete bearing correction rejected");
        Check(AdaptiveGuidanceGrounding.BearingCorrection(DirectionalHintPolicy.UpPhrase, 0, 3.5f, .1f, true) == null, "visible-reference suppression still blocks vertical correction");
        var capabilities = new[] { "orient_brief", "orient_explicit", "encourage" };
        var payload = JevDecisionClient.AdaptivePayload(new JObject { ["behavior"] = new JObject() }, capabilities);
        var criteria = payload["questions"]["assistance"]["criteria"];
        Check(criteria["observe_4s"] != null && criteria["orient_explicit"] != null && criteria["correct"] == null,
            "strategy contract includes real observation durations and only supported capabilities");
        Check(payload["state"]["eligible_actions"] == null && payload["questions"]["transmit"] == null,
            "adaptive decision is not a phrase menu or second transmit decision");
        var observe = JevDecisionClient.ParseAdaptive("{\"answers\":{\"assistance\":{\"type\":\"choice\",\"choice\":\"observe_4s\"}}}", capabilities);
        Check(!observe.Speak && observe.ReassessSeconds == 4, "four-second observation remains silence");
        Throws<FormatException>(() => JevDecisionClient.ParseAdaptive("{\"answers\":{\"assistance\":{\"type\":\"choice\",\"choice\":\"correct\"}}}", capabilities));
        Throws<FormatException>(() => JevDecisionClient.ParseAdaptive("{\"answers\":{\"assistance\":{\"type\":\"choice\",\"choice\":\"observe_999s\"}}}", capabilities));
        var clock = new AdaptiveGuidanceSchedule();
        Check(clock.Due(0,"right"), "first observation is due");
        clock.Requested(0,"right"); clock.Decided(0.2, observe);
        Check(!clock.Due(2,"right") && clock.Due(4.2,"right"), "no one-second override of model observation");
        Check(clock.Due(2,"left"), "new geometry wakes observation early");
        Check(!clock.Due(.3,"left"), "event wakes are rate limited");
        var history = new AdaptiveGuidanceHistory();
        history.BeginTrial("participant-A", 0, 0, false);
        for (int i=0;i<=8;i++) history.Observe(i*.5, true, 70-i*5, "right");
        var s=history.Snapshot(4);
        Check((double)s["recent"]["error_reduction_degrees"]==40, "observed progress is measured from history");
        history.Spoken("d1","orient_brief",4,30);
        history.Observe(4.5,true,29,"right");
        history.Observe(5,true,27,"right");
        history.Observe(5.5,true,26,"right");
        history.Observe(6,true,24,"right");
        history.Observe(6.5,true,22,"right");
        history.Observe(7,true,20,"right");
        s=history.Snapshot(7);
        Check((double)s["interventions"][0]["error_reduction_after_3s"]==10, "only actual onset creates intervention outcome");
        history.WrongSelection(); history.EndTrial("completed"); history.BeginTrial("participant-A",1,20,false);
        Check(((JArray)history.Snapshot(20)["previous_trials"]).Count==1, "same participant retains earlier trial evidence");
        history.BeginTrial("participant-B",0,30,false);
        Check(((JArray)history.Snapshot(30)["previous_trials"]).Count==0, "participant change erases adaptive memory");
        history.Observe(30,true,70,"right"); history.Observe(30.5,true,60,"right"); history.Observe(31,false,0,null);
        history.Observe(35,true,20,"right");
        Check((double)history.Snapshot(35)["recent"]["error_reduction_degrees"]==0, "tracking gaps never manufacture progress");
        history.BeginTrial("participant-B",0,100,true);
        Check((double)history.Snapshot(100)["active_search_seconds"]<10, "pause duration excluded from active search");
        history.Spoken("a", "refine_brief", 100, 30);
        history.Spoken("b", "refine_explicit", 101, 30);
        Check((string)history.Snapshot(101)["interventions"][0]["response_status"] == "unavailable_overlapping_intervention", "overlapping cues cannot claim an isolated response");
        history.EndTrial("completed");
        Check((string)history.Snapshot(101)["previous_trials"][0]["interventions"][1]["response_status"] == "unavailable_discontinuity", "unfinished cue response is unavailable in completed trial");
        Console.WriteLine("PASS adaptive strategy contract, scheduling, behavioral evidence, outcome and participant isolation");
    }
}

partial class SpatialVoiceChecks
{
    static JObject AdaptiveScenario(bool stalled)
    {
        var h = new AdaptiveGuidanceHistory(); h.BeginTrial("synthetic",0,0,false);
        for(int i=0;i<=20;i++) h.Observe(i*.5,true,stalled?30:80-i*2.5,"right");
        if(stalled)
        {
            h.Spoken("fixture-cue","orient_brief",10,30);
            h.WrongSelection(); h.WrongSelection();
            for(int i=21;i<=28;i++) h.Observe(i*.5,true,30,"right");
        }
        var runtime = new JObject {
            ["evidence"]=new JObject { ["valid"]=true,["geometryAvailable"]=true,["insideTargetWall"]=false,
                ["yawErrorDegrees"]=30,["pitchErrorDegrees"]=0,["controllerOnTarget"]=false,["targetGazeQualified"]=false,
                ["guidanceState"]=new JObject { ["newGuidanceAllowed"]=true } },
            ["playback"]=new JObject { ["is_speaking"]=false,["is_busy"]=false },
            ["seconds_since_speech_ended"]=stalled ? new JValue(3) : JValue.CreateNull()
        };
        return new JObject { ["schema"]="adaptive-assistance-v1",["policy"]=JevDecisionClient.AdaptivePolicy,
            ["decision_id"]=stalled?"synthetic-stalled":"synthetic-progress",["runtime"]=runtime,["behavior"]=h.Snapshot(stalled?14:10) };
    }
    sealed class AdaptiveHttp : System.Net.Http.HttpMessageHandler
    {
        readonly string m_Choice; readonly bool m_Stall;
        public AdaptiveHttp(string choice,bool stall=false) { m_Choice=choice;m_Stall=stall; }
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)
        {
            var body=JObject.Parse(await request.Content.ReadAsStringAsync());
            Check(body["state"]["behavior"]["recent"]["samples"] is JArray,"real HTTP adapter sends behavioral history");
            Check(body["questions"]["assistance"]!=null && body["questions"]["navigation_action"]==null,"new contract is distinct from legacy classifier");
            if(m_Stall) await Task.Delay(10000,token);
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) {Content=new System.Net.Http.StringContent(
                "{\"answers\":{\"assistance\":{\"type\":\"choice\",\"choice\":\""+m_Choice+"\",\"probabilities\":{\""+m_Choice+"\":1}}}}")};
        }
    }
    static async Task CheckAdaptiveTransport()
    {
        var capabilities=new[]{"orient_brief","orient_explicit"};
        using(var http=new System.Net.Http.HttpClient(new AdaptiveHttp("observe_4s")))
        {
            var client=new JevDecisionClient("test",http); JObject trace=null;
            client.Trace += json => {var t=JObject.Parse(json);if((string)t["stage"]=="response")trace=t;};
            var r=await client.ChooseAssistanceAsync(AdaptiveScenario(false),capabilities,CancellationToken.None);
            Check(r.RequestId=="synthetic-progress" && !r.Speak && r.ReassessSeconds==4,"transport preserves request identity and observation decision");
            Check(trace?["answers"]?["assistance"]?["probabilities"]!=null,"provider probabilities retained in replay trace");
        }
        using(var http=new System.Net.Http.HttpClient(new AdaptiveHttp("correct")))
        {
            bool rejected=false;
            try {await new JevDecisionClient("test",http).ChooseAssistanceAsync(AdaptiveScenario(true),capabilities,CancellationToken.None);}
            catch(FormatException){rejected=true;}
            Check(rejected,"unavailable strategy cannot schedule audio");
        }
        using(var http=new System.Net.Http.HttpClient(new AdaptiveHttp("orient_explicit",true)))
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.CancelAfter(20);bool cancelled=false;
            try {await new JevDecisionClient("test",http).ChooseAssistanceAsync(AdaptiveScenario(true),capabilities,cancellation.Token);}
            catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"cancelled adaptive response cannot play");
        }
        Console.WriteLine("PASS adaptive HTTP, probabilities, unoffered strategy rejection and cancellation");
    }
    sealed class LocalAdaptiveHttp : System.Net.Http.HttpMessageHandler
    {
        readonly string version; readonly bool wrongId, stall;
        public LocalAdaptiveHttp(string version, bool wrongId=false, bool stall=false)
        { this.version=version; this.wrongId=wrongId; this.stall=stall; }
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken token)
        {
            Check(request.RequestUri.Host == "127.0.0.1" && request.RequestUri.Port == 8766, "local transport stays loopback");
            Check(request.Headers.Authorization == null, "cloud credentials never sent to local model");
            if(stall) await Task.Delay(10000,token);
            JObject response;
            if(request.Method == System.Net.Http.HttpMethod.Get)
                response=new JObject { ["status"]="ready", ["scope"]="assistance", ["model_version"]=version };
            else
            {
                var payload=JObject.Parse(await request.Content.ReadAsStringAsync());
                Check(payload["questions"]["assistance"]!=null,"local receives same assistance contract");
                response=new JObject { ["model_version"]=version, ["decision_id"]=wrongId ? "old" : (string)payload["state"]["decision_id"],
                    ["probability_semantics"]="uncalibrated_offered_choice_softmax", ["checkpoint_sha256"]="test-hash",
                    ["answers"]=new JObject { ["assistance"]=new JObject { ["type"]="choice", ["choice"]="orient_explicit",
                        ["probabilities"]=new JObject { ["orient_explicit"]=1.0 } } } };
            }
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content=new System.Net.Http.StringContent(response.ToString()) };
        }
    }
    static async Task CheckLocalAdaptiveTransport()
    {
        var capabilities=new[]{"orient_brief","orient_explicit"};
        using(var http=new System.Net.Http.HttpClient(new LocalAdaptiveHttp(JevDecisionClient.LocalModelVersion)))
        {
            var client=JevDecisionClient.CreateLocalAdaptive(http); JObject trace=null;
            client.Trace+=json=>{var t=JObject.Parse(json); if((string)t["stage"]=="response") trace=t;};
            await client.CheckLocalConnectionAsync(CancellationToken.None);
            var result=await client.ChooseAssistanceAsync(AdaptiveScenario(true),capabilities,CancellationToken.None);
            Check(result.Strategy=="orient_explicit" && result.RequestId=="synthetic-stalled", "local decision accepted with identity");
            Check((string)trace["provider"]=="local_minilm" && (string)trace["model_version"]==JevDecisionClient.LocalModelVersion &&
                (string)trace["checkpoint_sha256"]=="test-hash", "trace attributes actual local response");
            bool rejected=false;
            try { await client.WarmAsync(CancellationToken.None); } catch(InvalidOperationException) {rejected=true;}
            Check(rejected,"legacy Jev contracts cannot reach local model");
        }
        foreach(bool wrongIdentity in new[]{false,true})
        using(var http=new System.Net.Http.HttpClient(new LocalAdaptiveHttp(wrongIdentity?JevDecisionClient.LocalModelVersion:"old-checkpoint",wrongIdentity)))
        {
            bool rejected=false;
            try { await JevDecisionClient.CreateLocalAdaptive(http).ChooseAssistanceAsync(AdaptiveScenario(true),capabilities,CancellationToken.None); }
            catch(FormatException) {rejected=true;}
            Check(rejected,"mismatched local checkpoint or request rejected with no cloud fallback");
        }
        using(var http=new System.Net.Http.HttpClient(new LocalAdaptiveHttp(JevDecisionClient.LocalModelVersion,stall:true)))
        using(var cancellation=new CancellationTokenSource(20))
        {
            bool cancelled=false;
            try { await JevDecisionClient.CreateLocalAdaptive(http).ChooseAssistanceAsync(AdaptiveScenario(true),capabilities,cancellation.Token); }
            catch(OperationCanceledException) {cancelled=true;}
            Check(cancelled,"cancelled local request cannot return a decision");
        }
        Console.WriteLine("PASS local adaptive transport, credentials, identity, contract boundaries, attribution and cancellation");
    }
    static async Task LiveLocalAdaptive()
    {
        var client=JevDecisionClient.CreateLocalAdaptive();
        await client.CheckLocalConnectionAsync(CancellationToken.None);
        foreach(bool stalled in new[]{false,true})
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var r=await client.ChooseAssistanceAsync(AdaptiveScenario(stalled),new[]{"orient_brief","orient_explicit"},CancellationToken.None);
            Console.WriteLine("LOCAL production C# smoke: stalled="+stalled+";strategy="+r.Strategy+";elapsed_ms="+timer.ElapsedMilliseconds);
        }
    }

    static async Task LiveAdaptive(string keys,string output)
    {
        if(System.IO.File.Exists(output))throw new InvalidOperationException("Do not overwrite evidence");
        var client=new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(keys))["jev_key"]);
        var rows=new JArray();
        for(int repeat=0;repeat<2;repeat++)foreach(bool stalled in new[]{false,true})
        {
            var state=AdaptiveScenario(stalled);state["decision_id"]=(string)state["decision_id"]+"-"+repeat;
            var timer=System.Diagnostics.Stopwatch.StartNew();JObject response=null;
            Action<string> listener=json=>{var t=JObject.Parse(json);if((string)t["stage"]=="response")response=t;};
            client.Trace+=listener;
            try
            {
                var decision=await client.ChooseAssistanceAsync(state,new[]{"orient_brief","orient_explicit"},CancellationToken.None);
                rows.Add(new JObject{["case"]=stalled?"stalled_after_brief":"independent_progress",["state"]=state,
                    ["strategy"]=decision.Strategy,["response"]=response,["elapsed_ms"]=timer.Elapsed.TotalMilliseconds});
            }
            catch(Exception e){rows.Add(new JObject{["case"]=stalled?"stalled_after_brief":"independent_progress",["error_type"]=e.GetType().Name});}
            finally{client.Trace-=listener;}
        }
        System.IO.File.WriteAllText(output,new JObject{["measurement"]="Synthetic production C# requests; no participant or audio playback",["rows"]=rows}.ToString());
        Console.WriteLine(rows.ToString());
    }

    static void ExportAdaptive(string casesPath, string output)
    {
        if (System.IO.File.Exists(output)) throw new InvalidOperationException("Do not overwrite evidence");
        var rows = new JArray();
        foreach (JObject item in JArray.Parse(System.IO.File.ReadAllText(casesPath)))
        {
            var state = (JObject)item["state"].DeepClone();
            state["policy"] = JevDecisionClient.AdaptivePolicy;
            rows.Add(new JObject { ["name"] = item["name"],
                ["payload"] = JevDecisionClient.AdaptivePayload(state,item["capabilities"].ToObject<string[]>()) });
        }
        System.IO.File.WriteAllText(output,rows.ToString());
    }

    static async Task ReplayAdaptive(string keys, string casesPath, string output)
    {
        if (System.IO.File.Exists(output)) throw new InvalidOperationException("Do not overwrite evidence");
        var client = new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(keys))["jev_key"]);
        var cases = JArray.Parse(System.IO.File.ReadAllText(casesPath));
        var rows = new JArray(); int failures = 0;
        for (int repeat = 0; repeat < 2; repeat++) foreach (JObject item in cases)
        {
            var state = (JObject)item["state"].DeepClone();
            state["decision_id"] = "replay-" + (string)item["name"] + "-" + repeat;
            state["policy"] = JevDecisionClient.AdaptivePolicy;
            var capabilities = item["capabilities"].ToObject<string[]>();
            var row = new JObject { ["case"] = item["name"], ["repeat"] = repeat };
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await client.ChooseAssistanceAsync(state, capabilities, CancellationToken.None);
                bool matched = Array.IndexOf(item["expected"].ToObject<string[]>(), result.Strategy) >= 0;
                row["strategy"] = result.Strategy; row["matched"] = matched;
                if (!matched) failures++;
            }
            catch (Exception e) { row["error_type"] = e.GetType().Name; failures++; }
            row["elapsed_ms"] = timer.Elapsed.TotalMilliseconds; rows.Add(row);
            Console.WriteLine(row.ToString(Newtonsoft.Json.Formatting.None));
        }
        System.IO.File.WriteAllText(output, new JObject { ["policy"] = JevDecisionClient.AdaptivePolicy,
            ["measurement"] = "Desktop decision replay; no headset playback or participant benefit measured",
            ["failures"] = failures, ["rows"] = rows }.ToString());
        Check(failures == 0, "Recorded adaptive silence/departure replay expectations failed: " + failures);
    }
}
