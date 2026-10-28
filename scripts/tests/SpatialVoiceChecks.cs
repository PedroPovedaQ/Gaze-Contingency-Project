using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SpatialVoice;

// Pure networking/audio tests compile the production adapters with this DTO only.
namespace SpatialVoice
{
    public enum NavigationTone { Direct, Technical, Casual, Encouraging }
    public struct NavigationPhrase { public string id, text; public NavigationTone tone; public SpeechPhraseMetadata metadata; }
}
partial class SpatialVoiceChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    static int Main(string[] args)
    {
        try
        {
            Run().GetAwaiter().GetResult();
            CheckSpeechMemory();
            CheckActionMenu();
            CheckAdaptiveGuidance();
            CheckAdaptivePipeline().GetAwaiter().GetResult();
            CheckAdaptiveTransport().GetAwaiter().GetResult();
            CheckLocalAdaptiveTransport().GetAwaiter().GetResult();
            CheckActionTransport().GetAwaiter().GetResult();
            if (args.Length == 0) { }
            else if (args.Length == 2 && args[0] == "--export-phrase-menus") System.IO.File.WriteAllText(args[1], AdaptivePhraseMenus().ToString());
            else if (args.Length == 1 && args[0] == "--live-local-adaptive") LiveLocalAdaptive().GetAwaiter().GetResult();
            else if (args.Length == 3 && args[0] == "--live-adaptive") LiveAdaptive(args[1],args[2]).GetAwaiter().GetResult();
            else if (args.Length == 4 && args[0] == "--replay-adaptive") ReplayAdaptive(args[1],args[2],args[3]).GetAwaiter().GetResult();
            else if (args.Length == 3 && args[0] == "--export-adaptive") ExportAdaptive(args[1],args[2]);
            else if (args.Length == 3 && args[0] == "--benchmark-actions") BenchmarkActions(args[1],args[2]).GetAwaiter().GetResult();
            else if (args.Length == 2 && args[0] == "--live") Live(args[1]).GetAwaiter().GetResult();
            else if (args.Length == 2 && args[0] == "--export-memory-probes") ExportMemoryProbes(args[1]);
            else if (args.Length == 5 && args[0] == "--scenario-suite") RunScenarios(args);
            else if (args.Length == 3 && args[0] == "--audition") Audition(args[1], args[2]).GetAwaiter().GetResult();
            else if (args.Length == 3 && args[0] == "--navigation-suite") NavigationSuite(args[1], args[2]).GetAwaiter().GetResult();
            else if (args.Length == 2 && args[0] == "--live-encouragement") LiveEncouragement(args[1]).GetAwaiter().GetResult();
            else if (args.Length == 3 && args[0] == "--benchmark-guidance") BenchmarkGuidance(args[1], args[2]).GetAwaiter().GetResult();
            else if (args.Length == 2 && args[0] == "--live-transmission") LiveTransmission(args[1]).GetAwaiter().GetResult();
            else if (args.Length == 2 && args[0] == "--live-eleven") LiveEleven(args[1]).GetAwaiter().GetResult();
            else throw new ArgumentException("Unknown test mode or argument count.");
            Console.WriteLine("PASS spatial voice contracts, PCM boundaries, fragmented socket audio, limits and cancellation");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static async Task Live(string configurationPath)
    {
        var key = (string)JObject.Parse(System.IO.File.ReadAllText(configurationPath))["jev_key"];
        var candidates = new List<NavigationPhrase> {
            new NavigationPhrase { id="left_direct", text="Look left.", tone=NavigationTone.Direct },
            new NavigationPhrase { id="left_casual", text="Okay, look left.", tone=NavigationTone.Casual },
            new NavigationPhrase { id="left_technical", text="Turn left approximately 45 degrees.", tone=NavigationTone.Technical },
            new NavigationPhrase { id="left_encouraging", text="Take your time; look left.", tone=NavigationTone.Encouraging } };
        var client = new JevDecisionClient(key);
        for (int i=0;i<3;i++)
        {
            var time = System.Diagnostics.Stopwatch.StartNew();
            var choice = await client.ChooseAsync("Synthetic seated-navigation test. Target is 45 degrees left and 3m away. Prefer casual wording.",
                candidates, CancellationToken.None, 5000);
            Console.WriteLine("LIVE Jev/Vercel: candidate=" + choice.id + ";elapsed_ms=" + time.ElapsedMilliseconds);
        }
    }
    // Alternate order to reduce warm-connection/order bias. All inputs are synthetic.
    static async Task BenchmarkGuidance(string config, string output)
    {
        var client = new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(config))["jev_key"]);
        var choices = new[] { new NavigationPhrase { id="left", text="Look left.", tone=NavigationTone.Direct },
            new NavigationPhrase { id="gentle", text="Gently, look left.", tone=NavigationTone.Encouraging },
            new NavigationPhrase { id="casual", text="Okay, look left.", tone=NavigationTone.Casual },
            new NavigationPhrase { id="technical", text="Turn left approximately 45 degrees.", tone=NavigationTone.Technical } };
        var results = new JArray();
        var warm = System.Diagnostics.Stopwatch.StartNew();
        try { await client.WarmAsync(CancellationToken.None); Console.WriteLine("WARMUP ms="+warm.ElapsedMilliseconds); }
        catch(Exception e) { Console.WriteLine("WARMUP fallback="+e.GetType().Name); }
        for (int i=0; i<12; i++)
        {
            bool valid=i%4!=2, speaking=i%4==3, redundant=i%4==1;
            bool expected=valid && !speaking && !redundant;
            var runtime=new JObject { ["evidence"]=new SpeechGuidanceEvidence { valid=valid, geometryAvailable=true,
                yawErrorDegrees=-45, gazeAgeSeconds=.01, phase="search" }.Snapshot(),
                ["playback"]=new JObject { ["is_speaking"]=speaking, ["is_busy"]=speaking,
                    ["transmission_decision_pending"]=false, ["current_phrase"]=speaking?"Look right.":null,
                    ["last_spoken_phrase"]=redundant?"Look left.":"Look right.", ["pending_phrase"]=null },
                ["seconds_since_speech_ended"]=redundant?.25:5, ["maximum_deferral_seconds"]=3,
                ["deferred_seconds"]=0, ["locally_qualified"]=valid };
            string state=SpeechNavigationState.Create(-45,3,"Direct",redundant?"Look left.":"Look right.",
                "Coarse horizontal guidance; target wall not reached.",runtime.ToString());
            for(int j=0;j<2;j++)
            {
                bool joint=(i+j)%2==0;
                var timer=System.Diagnostics.Stopwatch.StartNew();
                string failure=null; bool? speak=null; int calls=0;
                try {
                    if(joint) { calls++; speak=(await client.ChooseGuidanceAsync(state,choices,CancellationToken.None)).Speak; }
                    else {
                        calls++; var phrase=await client.ChooseAsync(state,choices,CancellationToken.None);
                        var tx=new JObject { ["runtime"]=runtime, ["proposed_phrase"]=phrase.text,
                            ["proposed_semantics"]=JObject.FromObject(SpeechPhraseMetadata.Describe(phrase.text)),
                            ["seconds_since_speech_ended"]=runtime["seconds_since_speech_ended"],
                            ["locally_qualified"]=valid,["reason"]="catalog_turn" };
                        calls++; speak=await client.ShouldTransmitAsync(tx.ToString(),CancellationToken.None);
                    }
                } catch(Exception e) { failure=e.GetType().Name; }
                var row=new JObject { ["pair"]=i,["scenario"]=i%4,["path"]=joint?"joint":"sequential",
                    ["elapsed_ms"]=timer.Elapsed.TotalMilliseconds,["calls"]=calls,["failure"]=failure,
                    ["speak"]=speak,["expected"]=expected };
                results.Add(row); System.IO.File.WriteAllText(output,results.ToString());
                Console.WriteLine(row.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
        foreach (JObject result in results)
            if ((string)result["path"]=="joint" && result["failure"].Type==JTokenType.Null)
                Check((bool)result["speak"]==(bool)result["expected"], "Joint decision mismatch in latency benchmark");
    }
    static async Task LiveTransmission(string configurationPath)
    {
        var client = new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(configurationPath))["jev_key"]);
        var cases = new[] {
            new { id="new_direction", text="Look left.", reason="stable_direction", last="Look right.", age=2d, valid=true, speaking=false, expected=true },
            new { id="redundant", text="Look left.", reason="stable_direction", last="Look left.", age=.25d, valid=true, speaking=false, expected=false },
            new { id="invalid_gaze", text="Look left.", reason="stable_direction", last="Look right.", age=2d, valid=false, speaking=false, expected=false },
            new { id="already_speaking", text="Look left.", reason="stable_direction", last="Look right.", age=0d, valid=true, speaking=true, expected=false }
        };
        foreach (var c in cases)
        {
            var state=new JObject { ["schema"]="speech-transmission-v1", ["proposed_phrase"]=c.text,
                ["reason"]=c.reason, ["locally_qualified"]=true, ["seconds_since_speech_ended"]=c.age,
                ["runtime"]=new JObject { ["evidence"]=new JObject { ["valid"]=c.valid,
                    ["insideTargetWall"]=false, ["currentDirection"]="left", ["controllerOnTarget"]=false },
                    ["playback"]=new JObject { ["is_speaking"]=c.speaking, ["is_busy"]=true,
                        ["pending_phrase"]=c.text, ["last_spoken_phrase"]=c.last } } };
            var timer=System.Diagnostics.Stopwatch.StartNew();
            try {
                bool result=await client.ShouldTransmitAsync(state.ToString(),CancellationToken.None);
                Console.WriteLine($"LIVE transmission {c.id}: expected={c.expected}; speak={result}; elapsed_ms={timer.ElapsedMilliseconds}");
                Check(result==c.expected,"Live transmission mismatch: "+c.id);
            } catch(OperationCanceledException) { Console.WriteLine($"LIVE transmission {c.id}: deadline fallback; elapsed_ms={timer.ElapsedMilliseconds}"); }
        }
    }
    static void RunScenarios(string[] args) => Scenarios(args[1], args[2], args[3], int.Parse(args[4])).GetAwaiter().GetResult();
    static async Task Scenarios(string configurationPath, string casesPath, string outputPath, int repeats)
    {
        var client = new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(configurationPath))["jev_key"]);
        var cases = JArray.Parse(System.IO.File.ReadAllText(casesPath));
        var results = new JArray();
        int mismatches = 0, fallbacks = 0;
        for (int round = 0; round < repeats; round++)
        foreach (JObject c in cases)
        {
            var evidence = new SpeechGuidanceEvidence {
                valid = (bool?)c["valid"] != false,
                insideTargetWall = ((string)c["reason"]) == "gaze_target_plane",
                targetGazeQualified = ((string)c["reason"]) == "gaze_target_vicinity_hold" || ((string)c["reason"]) == "controller_target_gaze_vicinity",
                controllerOnTarget = ((string)c["reason"]) == "controller_target_gaze_vicinity",
                gazeAgeSeconds = .011
            };
            string state = SpeechInterruptionState.Create((string)c["current"], (string)c["proposed"],
                (string)c["reason"], (double)c["elapsed"], (double)c["remaining"], evidence, (string)c["context"] ?? "tip", (string)c["current_stage"] ?? "unknown");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool? fade = null; string failure = null;
            try { fade = await client.ShouldInterruptAsync(state, CancellationToken.None); }
            catch (OperationCanceledException) { failure = "deadline_finish"; }
            catch (Exception e) { failure = e.GetType().Name; }
            bool locallyAllowed = (bool?)c["valid"] != false && ((string)c["context"] ?? "tip") == "tip" &&
                SpeechPhraseMetadata.CanReplace((string)c["current"], (string)c["proposed"]) &&
                GuidanceSpeechPolicy.IsCorrectionEvent((string)c["reason"]) &&
                !GuidanceSpeechPolicy.IsRedundantArrival((string)c["reason"], (string)c["current_stage"]) && (string)c["current"] != (string)c["proposed"] &&
                GuidanceSpeechPolicy.CanConsiderInterruption(GuidanceSpeechPolicy.IsAcknowledgment((string)c["current"]) ||
                    (string)c["current"] == DirectionalHintPolicy.SelectionReminderPhrase,
                    (double)c["elapsed"], (double)c["remaining"] - timer.Elapsed.TotalSeconds, 10, -100);
            var result = new JObject { ["state"] = JObject.Parse(state), ["local_gate"] = locallyAllowed, ["effective_fade"] = locallyAllowed && fade == true,
                ["id"] = c["id"], ["round"] = round,
                ["expected_fade"] = c["expected_fade"], ["fade"] = fade,
                ["failure"] = failure, ["elapsed_ms"] = timer.ElapsedMilliseconds };
            results.Add(result);
            if (failure == "deadline_finish") fallbacks++;
            else if (failure != null || (bool)result["effective_fade"] != (bool)c["expected_fade"]) mismatches++;
            System.IO.File.WriteAllText(outputPath, results.ToString());
            Console.WriteLine("SCENARIO " + c["id"] + ";fade=" + fade + ";effective=" + (locallyAllowed && fade == true) + ";expected=" + c["expected_fade"] + ";ms=" + timer.ElapsedMilliseconds + ";failure=" + failure);
        }
        Console.WriteLine("LIVE scenario summary: " + results.Count + " calls; " + fallbacks + " provider fallbacks; " + mismatches + " effective mismatches on completed responses.");
        Check(mismatches == 0, "Unexpected interruption decision or transport failure; inspect " + outputPath);
    }
    static async Task NavigationSuite(string config, string output)
    {
        var client = new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(config))["jev_key"]);
        var catalog = (JArray)JObject.Parse(System.IO.File.ReadAllText("Assets/Resources/SpatialVoice/navigation-phrases.json"))["navigation_phrases"];
        var results = new JArray();
        foreach (int angle in new[] { -170,-100,-45,-10,10,45,100,170 })
        {
            var candidates = new List<NavigationPhrase>();
            foreach (var p in catalog)
            {
                var c=p["constraints"];
                if (angle < (float)c["min_angle"] || angle >= (float)c["max_angle"] || 3 < (float)c["min_distance_meters"] || 3 >= (float)c["max_distance_meters"]) continue;
                candidates.Add(new NavigationPhrase { id=(string)p["id"],text=(string)p["text"],
                    tone=(NavigationTone)Enum.Parse(typeof(NavigationTone),(string)p["metadata"]["tone"],true),
                    metadata=p["metadata"].ToObject<SpeechPhraseMetadata>() });
            }
            Check(candidates.Count==5,"5 authored candidates at bearing");
            string last="none";
            for(int i=0;i<2;i++)
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                string state="Seated hidden-target task. Target relative yaw "+angle+" degrees; horizontal distance 3 meters. Preferred tone Casual. Last phrase: "+last+". Coarse horizontal guidance; target wall not reached.";
                try
                {
                    var chosen=await client.ChooseAsync(state,candidates,CancellationToken.None);
                    results.Add(new JObject { ["angle"]=angle,["iteration"]=i,["phrase"]=chosen.text,["tone"]=chosen.tone.ToString(),["repeated"]=chosen.text==last,["ms"]=watch.ElapsedMilliseconds });
                    last=chosen.text;
                }
                catch(OperationCanceledException) { results.Add(new JObject { ["angle"]=angle,["failure"]="deadline_local_fallback",["ms"]=watch.ElapsedMilliseconds }); }
            }
        }
        System.IO.File.WriteAllText(output,results.ToString());
        foreach (var result in results) Check((bool?)result["repeated"] != true, "Immediate navigation repetition; inspect " + output);
        Console.WriteLine("LIVE navigation suite: "+results.Count+" selections; report="+output);
    }
    static async Task Audition(string config, string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var client = new ElevenLabsPcmClient((string)JObject.Parse(System.IO.File.ReadAllText(config))["elevenlabs_key"]);
        var lines = new[] { "Turn right toward the back.", "You're in the right area. A little lower.",
            "Go back down and left.", "Yep, that's it!", "A bit more.", "Just a little more.", "Look a little lower.", "Look lower and left.", "At your own pace, turn right toward the back." };
        var manifest = new JArray();
        for(int i=0;i<lines.Length;i++)
        {
            string path=System.IO.Path.Combine(directory,i+".wav");
            if(!System.IO.File.Exists(path))
            {
                var pcm=new PcmStreamBuffer(ElevenLabsPcmClient.SampleRate*30);
                await client.StreamAsync(lines[i],"cjVigY5qzO86Huf0OWal",pcm,CancellationToken.None);
                var samples=new float[pcm.Count];pcm.Read(samples);
                using(var writer=new System.IO.BinaryWriter(System.IO.File.Create(path)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(ElevenLabsPcmClient.SampleRate);writer.Write(ElevenLabsPcmClient.SampleRate*2);
                    writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                    foreach(float sample in samples) writer.Write((short)Math.Max(short.MinValue,Math.Min(short.MaxValue,sample*32768)));
                }
            }
            manifest.Add(new JObject { ["file"]=path,["text"]=lines[i],["voice"]="Eric",["model"]=ElevenLabsPcmClient.Model });
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"manifest.json"),manifest.ToString());
        Console.WriteLine("Created "+lines.Length+" neutral-voice audition clips. No participant audio used.");
    }
    static async Task LiveEleven(string configurationPath)
    {
        var key = (string)JObject.Parse(System.IO.File.ReadAllText(configurationPath))["elevenlabs_key"];
        var pcm = new PcmStreamBuffer(ElevenLabsPcmClient.SampleRate * 30);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var receive = new ElevenLabsPcmClient(key).StreamAsync("Look to your left.", "cjVigY5qzO86Huf0OWal", pcm, CancellationToken.None);
        while (pcm.Count == 0 && !receive.IsCompleted) await Task.Delay(5);
        long firstAudio = timer.ElapsedMilliseconds;
        await receive;
        Check(pcm.Complete && pcm.Count > 0, "live socket completes with decoded PCM");
        Console.WriteLine("LIVE ElevenLabs: first_pcm_ms=" + firstAudio + ";complete_ms=" + timer.ElapsedMilliseconds +
            ";samples=" + pcm.Count + ";sample_rate=" + ElevenLabsPcmClient.SampleRate);
    }
    sealed class FakeDecisionProvider : INavigationDecisionProvider
    {
        public bool Available => true;
        public string Name => "fake";
        public event Action<string> Trace;
        public TaskCompletionSource<GuidanceSelection> Pending;
        public NavigationPhrase Offered;
        public bool BadChoice;
        public int Calls;
        public Task<NavigationPhrase> ChooseAsync(string state, IReadOnlyList<NavigationPhrase> candidates,
            CancellationToken cancellation, int timeoutMilliseconds = GuidanceSpeechPolicy.CatalogChoiceDeadlineMilliseconds)
        { Calls++; Offered=candidates[0]; Trace?.Invoke("fake_choice"); return Task.FromResult(BadChoice?new NavigationPhrase {id="not-offered",text="wrong"}:Offered); }
        public Task<GuidanceSelection> ChooseGuidanceAsync(string state, IReadOnlyList<NavigationPhrase> candidates,CancellationToken cancellation)
        { Calls++; Offered=candidates[0]; return Pending!=null?Pending.Task:Task.FromResult(new GuidanceSelection {Phrase=Offered,Speak=true,Source=Name}); }
        public Task<bool> ShouldTransmitAsync(string state,CancellationToken cancellation)=>Task.FromResult(true);
        public Task<bool> ShouldInterruptAsync(string state,CancellationToken cancellation)=>Task.FromResult(false);
        public Task WarmAsync(CancellationToken cancellation)=>Task.CompletedTask;
    }
    static async Task CheckDecisionProviderSubstitution()
    {
        var fake=new FakeDecisionProvider();
        var offered=new List<NavigationPhrase> {new NavigationPhrase {id="right",text="Look right."}};
        var choice=await NavigationDecisionRequests.Choose(fake,"{}",offered,CancellationToken.None,true);
        Check(fake.Calls==1&&choice.Phrase.id=="right"&&choice.Speak,"Injected provider is actually called with offered choices");
        fake.BadChoice=true;
        bool rejected=false;
        try {await NavigationDecisionRequests.Choose(fake,"{}",offered,CancellationToken.None,false);}catch(FormatException){rejected=true;}
        Check(rejected,"Local offered-set validation also protects against a non-Jev provider");
        fake.BadChoice=false;fake.Pending=new TaskCompletionSource<GuidanceSelection>();
        using(var cancellation=new CancellationTokenSource())
        {
            var request=NavigationDecisionRequests.Choose(fake,"{}",offered,cancellation.Token,true);
            cancellation.Cancel(); fake.Pending.SetResult(new GuidanceSelection {Phrase=offered[0],Speak=true});
            bool cancelled=false;try {await request;}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"A provider ignoring cancellation cannot publish a stale answer");
        }
    }
    static async Task Run()
    {
        await CheckDecisionProviderSubstitution();
        var exported = (JArray)JObject.Parse(System.IO.File.ReadAllText("Assets/Resources/SpatialVoice/guidance-phrase-semantics.json"))["phrases"];
        var exportedTexts = new HashSet<string>();
        foreach (var row in exported)
        {
            string text = (string)row["text"];
            Check(exportedTexts.Add(text) && (string)row["semantics"]["intent"] != "unknown" &&
                JToken.DeepEquals(row["semantics"], JToken.FromObject(SpeechPhraseMetadata.Describe(text))),
                "Exported guidance meaning matches production: " + text);
        }
        foreach (var text in DirectionalHintPolicy.AllPhrases())
            Check(exportedTexts.Contains(text), "Every prepared phrase has exported meaning: " + text);

        await ExtendedEncouragementChecks();
        var first = new RoundPerformance { measurementsAvailable=true,searchSeconds=12,guidanceStarts=2 };
        var candidates = RoundEncouragement.Candidates(first,null,_=>true);
        Check(candidates.Count==4 && candidates.Exists(p=>p.id=="first_selection") && !candidates.Exists(p=>p.id=="recovery"),"First selection claims require no mistakes");
        var payloadRound=JObject.Parse(RoundEncouragement.Payload(first,candidates));
        Check((string)payloadRound["state"]["event"]=="round_complete" && (bool)payloadRound["state"]["correct_object_selected"] &&
            (bool)payloadRound["state"]["search_finished"] && !(bool)payloadRound["state"]["quick_completion_eligible"],
            "Completion context is explicit; a 12-second trial cannot justify quick praise");
        Check((int)payloadRound["state"]["round"]["guidanceStarts"]==2 && payloadRound["state"]["round"]["participant_id"]==null,"Round payload carries observed performance without participant identity");
        Check(RoundEncouragement.Parse("{\"answers\":{\"round_encouragement\":{\"type\":\"choice\",\"choice\":\"You selected the right object. Nice work.\"}}}",candidates).id=="nice","Round exact choice parses");
        Throws<FormatException>(()=>RoundEncouragement.Parse("{\"answers\":{\"round_encouragement\":{\"type\":\"choice\",\"choice\":\"You were faster.\"}}}",candidates));
        first.incorrectSelections=2;
        candidates=RoundEncouragement.Candidates(first,null,_=>true);
        Check(candidates.Exists(p=>p.id=="recovery") && !candidates.Exists(p=>p.id.StartsWith("first_selection")),"Mistakes disallow first-selection praise");
        first.measurementsAvailable=false;
        candidates=RoundEncouragement.Candidates(first,null,_=>true);
        Check(candidates.TrueForAll(p=>Array.IndexOf(RoundEncouragement.Phrases,p)<4),"Unknown measurements allow only generic praise");
        Check(RoundEncouragement.Candidates(first,"You selected the right object. Nice work.",t=>t=="You selected the right object. Nice work.").Count==0,"Unprepared or repeated speech never becomes eligible");
        first.measurementsAvailable=true;first.incorrectSelections=0;first.searchSeconds=40;
        Check(RoundEncouragement.Candidates(first,null,_=>true).Exists(p=>p.id=="persistence"),"Long search supports persistence without a speed comparison");
        first.searchSeconds=double.NaN;
        Check(!RoundEncouragement.Candidates(first,null,_=>true).Exists(p=>p.id=="first_selection"),"Nonfinite measurements cannot justify a performance claim");
        first.searchSeconds=12;
        candidates=RoundEncouragement.Candidates(first,null,_=>true);
        using (var http = new HttpClient(new EncouragementHttp(false)))
            Check((await new JevDecisionClient("test",http).ChooseEncouragementAsync(first,candidates,CancellationToken.None)).id=="nice",
                "Round decision uses its own typed HTTP contract");
        using (var http = new HttpClient(new EncouragementHttp(true)))
        {
            bool timeout=false;
            try { await new JevDecisionClient("test",http).ChooseEncouragementAsync(first,candidates,CancellationToken.None); }
            catch(OperationCanceledException) { timeout=true; }
            Check(timeout,"Round encouragement deadline cancels a stalled provider");
        }
        var choices = new List<NavigationPhrase> {
            new NavigationPhrase { id="left", text="Look left.", tone=NavigationTone.Direct },
            new NavigationPhrase { id="gentle", text="Gently, look left.", tone=NavigationTone.Encouraging } };
        var payload = JObject.Parse(JevDecisionClient.Payload("Target -45 degrees.", choices));
        Check((string)payload["questions"]["navigation_phrase"]["criteria"]["Look left."]["semantics"]["horizontal"] == "left", "TypeSafe choice criteria carry authored semantics");
        Check((string)payload["state"] == "Target -45 degrees.", "state retained");
        foreach (var text in DirectionalHintPolicy.AllPhrases())
            Check(SpeechPhraseMetadata.Describe(text).intent != "unknown", "authored metadata exists: " + text);
        var arrivalMetadata = SpeechPhraseMetadata.Describe("Right area. A little lower.");
        Check(arrivalMetadata.horizontal == "none" && arrivalMetadata.vertical == "down", "right area is not a right-turn command");
        Check(SpeechPhraseMetadata.Describe("Go back down and left.").horizontal == "left", "authored diagonal return direction");
        Check(SpeechPhraseMetadata.Describe("Yep, that's it!").interruptPolicy == "finish", "confirmation is protected metadata");
        Check(SpeechPhraseMetadata.Describe(DirectionalHintPolicy.PointingPhrase).intent == "controller_confirmation" &&
            Array.IndexOf(SpeechPhraseMetadata.Describe(DirectionalHintPolicy.PointingPhrase).requires, "qualified_controller_hold") >= 0 &&
            Array.IndexOf(SpeechPhraseMetadata.Describe(DirectionalHintPolicy.PointingPhrase).requires, "qualified_target_gaze") < 0,
            "Pointing confirmation describes controller hold plus vicinity, not a separate gaze hold");
        Check(SpeechPhraseMetadata.Describe("unrecognized text").intent == "unknown", "unknown wording never fabricates geometry");
        var structured = JObject.Parse(SpeechInterruptionState.Create("Go back down and left.", "Yep, that's it!",
            "gaze_target_vicinity_hold", 1, 2, new SpeechGuidanceEvidence { valid = true, targetGazeQualified = true }));
        Check((string)structured["current"]["semantics"]["intent"] == "return_to_target" &&
            (bool)structured["evidence"]["targetGazeQualified"] && structured["participant"] == null, "structured semantic and identifier-free evidence");
        var unknown = new SpeechGuidanceEvidence { yawErrorDegrees=45, verticalPlaneFraction=float.NaN }.Snapshot();
        Check(unknown["yawErrorDegrees"].Type==JTokenType.Null && unknown["verticalPlaneFraction"].Type==JTokenType.Null &&
            unknown["gazeAgeSeconds"].Type==JTokenType.Null && (string)unknown["controllerRayHit"]=="unavailable",
            "Unknown gaze/geometry/controller evidence cannot masquerade as alignment or a miss");
        var evidence = new SpeechGuidanceEvidence { valid=true, insideTargetWall=true, gazeRayHit="distractor",
            gazeWithinTargetVicinity=false, controllerEvidenceValid=true, controllerOnTarget=true,
            controllerRayHit="target", controllerTriggerHeld=1, controllerTargetHoldQualified=false };
        var runtime = new JObject { ["evidence"]=evidence.Snapshot(), ["playback"]=new JObject {
            ["is_speaking"]=true, ["current_phrase"]="Look left.", ["elapsed_seconds"]=.7, ["speech_remaining_seconds"]=1.2 } };
        var state = JObject.Parse(SpeechNavigationState.Create(-45,3,"Casual","Look right.","test",runtime.ToString()));
        evidence.controllerOnTarget=false;
        Check((bool)state["runtime"]["evidence"]["controllerOnTarget"] &&
            !(bool)state["runtime"]["evidence"]["controllerTargetHoldQualified"] &&
            (string)state["runtime"]["evidence"]["gazeRayHit"]=="distractor" &&
            (bool)state["runtime"]["playback"]["is_speaking"],
            "Navigation freezes ray-hit, hold and playback state independently before async use");
        var statePayload=JObject.Parse(JevDecisionClient.Payload(state.ToString(),choices));
        Check(JObject.Parse((string)statePayload["state"])["runtime"]["evidence"]["controllerTriggerHeld"].Value<int>()==1,
            "Structured current-state snapshot survives the real decision request contract");
        Check((string)payload["model"] == "typesafe-ai/jev", "Vercel model identifier");
        var chosen = JevDecisionClient.Parse("{\"answers\":{\"navigation_phrase\":{\"type\":\"choice\",\"choice\":\"Gently, look left.\"}}}", choices);
        Check(chosen.id == "gentle", "exact candidate mapping");
        Throws<FormatException>(() => JevDecisionClient.Parse("{\"answers\":{\"navigation_phrase\":{\"type\":\"choice\",\"choice\":\"Look right.\"}}}", choices));
        Throws<FormatException>(() => JevDecisionClient.Parse("{}", choices));
        using (var http = new HttpClient(new FakeHttp(false)))
        {
            var api = new JevDecisionClient("test-credential", http);
            var response = await api.ChooseAsync("Test state", choices, CancellationToken.None);
            Check(response.id == "left", "HTTP response routes to exact candidate");
        }
        using (var http = new HttpClient(new FakeHttp(true)))
        {
            bool timedOut = false;
            try { await new JevDecisionClient("test-credential", http).ChooseAsync("state", choices, CancellationToken.None, 20); }
            catch (OperationCanceledException) { timedOut = true; }
            Check(timedOut, "decision deadline cancels transport");
        }
        Check(JevDecisionClient.ParseInterruption("{\"answers\":{\"speech_action\":{\"type\":\"choice\",\"choice\":\"fade_out\"}}}"), "typed fade approval");
        Check(!JevDecisionClient.ParseInterruption("{\"answers\":{\"speech_action\":{\"type\":\"choice\",\"choice\":\"finish_phrase\"}}}"), "typed finish advice");
        Throws<FormatException>(() => JevDecisionClient.ParseInterruption("{\"answers\":{\"speech_action\":{\"type\":\"choice\",\"choice\":\"stop_now\"}}}"));
        Check(JevDecisionClient.ParseTransmission("{\"answers\":{\"transmit\":{\"type\":\"choice\",\"choice\":\"speak\"}}}"), "typed transmission approval");
        Check(!JevDecisionClient.ParseTransmission("{\"answers\":{\"transmit\":{\"type\":\"choice\",\"choice\":\"wait\"}}}"), "typed transmission wait");
        Throws<FormatException>(()=>JevDecisionClient.ParseTransmission("{\"answers\":{\"transmit\":{\"type\":\"choice\",\"choice\":\"interrupt\"}}}"));
        var jointPayload=JObject.Parse(JevDecisionClient.GuidancePayload(state.ToString(),choices));
        Check(jointPayload["state"].Type==JTokenType.Object && jointPayload["questions"]["transmit"]!=null &&
            jointPayload["questions"]["navigation_phrase"]!=null,"Joint request retains both typed decisions and structured evidence");
        string jointAnswer="{\"answers\":{\"navigation_phrase\":{\"type\":\"choice\",\"choice\":\"Look left.\"},\"transmit\":{\"type\":\"choice\",\"choice\":\"wait\"}}}";
        var jointResult=JevDecisionClient.ParseGuidance(jointAnswer,choices);
        Check(jointResult.Phrase.text=="Look left." && !jointResult.Speak,"Joint wait retains phrase without authorizing speech");
        Throws<FormatException>(()=>JevDecisionClient.ParseGuidance(jointAnswer.Replace("Look left.","Invented words"),choices));
        Throws<FormatException>(()=>JevDecisionClient.ParseGuidance(jointAnswer.Replace("wait","interrupt"),choices));
        Throws<FormatException>(()=>JevDecisionClient.ParseGuidance("{\"answers\":{\"navigation_phrase\":{\"type\":\"choice\",\"choice\":\"Look left.\"}}}",choices));
        var five = new List<NavigationPhrase>(choices);
        for(int i=five.Count;i<5;i++)five.Add(new NavigationPhrase { id="extra"+i,text="Alternative "+i,tone=NavigationTone.Casual });
        Check(((JObject)JObject.Parse(JevDecisionClient.GuidancePayload(state.ToString(),five))["questions"]["navigation_phrase"]["criteria"]).Count==5,"Five candidates supported");
        five.Add(new NavigationPhrase { text="Sixth",tone=NavigationTone.Direct });
        Throws<ArgumentException>(()=>JevDecisionClient.GuidancePayload(state.ToString(),five));
        var jointTransport = new JointHttp(jointAnswer);
        using (var http = new HttpClient(jointTransport))
        {
            var traced = new JevDecisionClient("secret-test-key",http);
            var traces = new List<JObject>(); traced.Trace += value => traces.Add(JObject.Parse(value));
            var decision = await traced.ChooseGuidanceAsync(state.ToString(),choices,CancellationToken.None);
            Check(traces.Count==2 && (string)traces[0]["request_id"]==decision.RequestId && (string)traces[1]["request_id"]==decision.RequestId,"Request/answer share stable correlation ID");
            Check(traces[0]["payload"]["questions"]!=null && traces[1]["answers"]!=null && !traces[0].ToString().Contains("secret-test-key"),"Trace keeps candidates and answers, never credentials");
            Check(jointTransport.Calls==1 && !decision.Speak,"Both decisions use exactly one HTTP request");
        }
        using (var http = new HttpClient(new JointHttp(jointAnswer, stall:true)))
        using (var cancel = new CancellationTokenSource())
        {
            cancel.CancelAfter(10); bool canceled=false;
            try { await new JevDecisionClient("test",http).ChooseGuidanceAsync(state.ToString(),choices,cancel.Token); }
            catch(OperationCanceledException) { canceled=true; }
            Check(canceled,"Joint request honors external session cancellation");
        }
        var pcm = new PcmStreamBuffer(8);
        pcm.Write(new byte[] { 0 }); pcm.Write(new byte[] { 128, 255, 127, 0 }); pcm.Write(new byte[] { 0 }); pcm.Finish();
        var samples = new float[4]; pcm.Read(samples);
        Check(samples[0] == -1 && samples[1] > .9999f && samples[2] == 0 && samples[3] == 0 && pcm.Drained, "PCM16 signed endian and odd chunk boundaries");
        var small = new PcmStreamBuffer(2); small.Write(new byte[]{0,0,0,0});
        Throws<InvalidOperationException>(() => small.Write(new byte[]{0,0}));
        small.Read(new float[1]); small.Write(new byte[]{0,128});
        var wrapped = new float[2]; small.Read(wrapped); Check(wrapped[1] == -1, "ring wraps without data loss");
        small.Read(new float[2]); Check(small.Underruns == 1, "underrun silence counted");
        var partial = new PcmStreamBuffer(2); partial.Write(new byte[]{0}); Throws<FormatException>(() => partial.Finish());
        var socket = new FakeSocket(new[] { "{\"audio\":\"AIA=\",", "\"isFinal\":false}", "{\"audio\":\"/38=\",\"is_final\":true}" }, new[]{false,true,true});
        var streamed = new PcmStreamBuffer(8);
        await ElevenLabsPcmClient.ReceiveAsync(socket, streamed, CancellationToken.None);
        Check(streamed.Complete && streamed.Count == 2, "fragmented JSON plus final audio decoded");
        var exact = new float[2]; streamed.Read(exact); Check(exact[0] == -1 && exact[1] > .9999, "network PCM samples preserved");
        bool failed = false;
        try { await ElevenLabsPcmClient.ReceiveAsync(new FakeSocket(new[]{"{\"error\":\"bad\"}"}, new[]{true}), new PcmStreamBuffer(8), CancellationToken.None); }
        catch (System.IO.IOException) { failed = true; } Check(failed, "provider errors rejected");
        failed = false;
        try { await ElevenLabsPcmClient.ReceiveAsync(new FakeSocket(Array.Empty<string>(),Array.Empty<bool>()), new PcmStreamBuffer(8), CancellationToken.None); }
        catch (System.IO.IOException) { failed = true; } Check(failed, "premature close rejected");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel(); failed = false;
            try { await ElevenLabsPcmClient.ReceiveAsync(socket, new PcmStreamBuffer(8), cancellation.Token); }
            catch (OperationCanceledException) { failed = true; } Check(failed, "socket cancellation honored");
        }
        Check(ElevenLabsPcmClient.Url("voice").Query.Contains("output_format=pcm_24000") &&
            ElevenLabsPcmClient.Url("voice").Query.Contains("auto_mode=true"), "explicit PCM and supported chunk policy");
        // Exercise ring publication while producer/consumer run on different threads.
        var concurrent = new PcmStreamBuffer(65536);
        var producer = Task.Run(() => { for (int i=0;i<10000;i++) concurrent.Write(new[]{(byte)i,(byte)(i>>8)}); concurrent.Finish(); });
        int read = 0; var one = new float[1];
        while (!concurrent.Drained)
        {
            if (concurrent.Count == 0) { await Task.Yield(); continue; }
            concurrent.Read(one); Check(one[0] == read / 32768f, "concurrent ordering"); read++;
        }
        await producer; Check(read == 10000, "all concurrent samples drained");
    }
    sealed class JointHttp : HttpMessageHandler
    {
        readonly string answer; readonly bool stall;
        public int Calls;
        public JointHttp(string answer, bool stall=false) { this.answer=answer;this.stall=stall; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;
            var body=JObject.Parse(await request.Content.ReadAsStringAsync());
            Check(body["questions"]["transmit"]!=null && body["questions"]["navigation_phrase"]!=null,
                "Both typed questions reach the same transport");
            if(stall) await Task.Delay(10000,token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content=new StringContent(answer) };
        }
    }
    sealed class FakeHttp : HttpMessageHandler
    {
        readonly bool delay;
        public FakeHttp(bool delay) { this.delay = delay; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Check(request.RequestUri.AbsoluteUri == "https://ai-gateway.vercel.sh/typesafe/v1/systemone", "verified vendor endpoint");
            Check(request.Headers.Authorization.Scheme == "Bearer", "Bearer authentication");
            if (delay) await Task.Delay(10000, token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(
                "{\"answers\":{\"navigation_phrase\":{\"type\":\"choice\",\"choice\":\"Look left.\"}}}") };
        }
    }
    sealed class EncouragementHttp : HttpMessageHandler
    {
        readonly bool stall;
        public EncouragementHttp(bool stall) { this.stall=stall; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var body=JObject.Parse(await request.Content.ReadAsStringAsync());
            Check((string)body["questions"]?["round_encouragement"]?["type"]=="choice" &&
                (bool)body["state"]["round"]["measurementsAvailable"],"Structured round facts and separate question are transmitted");
            if(stall) await Task.Delay(10000,token);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content=new StringContent(
                "{\"answers\":{\"round_encouragement\":{\"type\":\"choice\",\"choice\":\"You selected the right object. Nice work.\"}}}") };
        }
    }
    sealed class FakeSocket : WebSocket
    {
        readonly string[] frames; readonly bool[] final; int index;
        public FakeSocket(string[] frames, bool[] final) { this.frames=frames;this.final=final; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string SubProtocol => null;
        public override void Abort() { }
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status,string desc,CancellationToken token) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status,string desc,CancellationToken token) => Task.CompletedTask;
        public override Task SendAsync(ArraySegment<byte> bytes,WebSocketMessageType type,bool end,CancellationToken token) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(index==frames.Length) return Task.FromResult(new WebSocketReceiveResult(0,WebSocketMessageType.Close,true));
            byte[] bytes=Encoding.UTF8.GetBytes(frames[index]); Array.Copy(bytes,0,buffer.Array,buffer.Offset,bytes.Length);
            return Task.FromResult(new WebSocketReceiveResult(bytes.Length,WebSocketMessageType.Text,final[index++]));
        }
    }
}
