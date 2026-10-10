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
class SpatialVoiceChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    static int Main(string[] args)
    {
        try
        {
            Run().GetAwaiter().GetResult();
            if (args.Length == 0) { }
            else if (args.Length == 2 && args[0] == "--live") Live(args[1]).GetAwaiter().GetResult();
            else if (args.Length == 5 && args[0] == "--scenario-suite") RunScenarios(args);
            else if (args.Length == 3 && args[0] == "--audition") Audition(args[1], args[2]).GetAwaiter().GetResult();
            else if (args.Length == 3 && args[0] == "--navigation-suite") NavigationSuite(args[1], args[2]).GetAwaiter().GetResult();
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
            Check(candidates.Count==4,"4 authored candidates at bearing");
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
    static async Task Run()
    {
        var choices = new List<NavigationPhrase> {
            new NavigationPhrase { id="left", text="Look left.", tone=NavigationTone.Direct },
            new NavigationPhrase { id="gentle", text="Gently, look left.", tone=NavigationTone.Encouraging } };
        var payload = JObject.Parse(JevDecisionClient.Payload("Target -45 degrees.", choices));
        Check((string)payload["questions"]["navigation_phrase"]["criteria"]["Look left."]["semantics"]["horizontal"] == "left", "TypeSafe choice criteria carry authored semantics");
        Check((string)payload["state"] == "Target -45 degrees.", "state retained");
        foreach (var text in DirectionalHintPolicy.AllPhrases())
            Check(SpeechPhraseMetadata.Describe(text).intent != "unknown", "authored metadata exists: " + text);
        var arrivalMetadata = SpeechPhraseMetadata.Describe("You're in the right area. A little lower.");
        Check(arrivalMetadata.horizontal == "none" && arrivalMetadata.vertical == "down", "right area is not a right-turn command");
        Check(SpeechPhraseMetadata.Describe("Go back down and left.").horizontal == "left", "authored diagonal return direction");
        Check(SpeechPhraseMetadata.Describe("Yep, that's it!").interruptPolicy == "finish", "confirmation is protected metadata");
        Check(SpeechPhraseMetadata.Describe("unrecognized text").intent == "unknown", "unknown wording never fabricates geometry");
        var structured = JObject.Parse(SpeechInterruptionState.Create("Go back down and left.", "Yep, that's it!",
            "gaze_target_vicinity_hold", 1, 2, new SpeechGuidanceEvidence { valid = true, targetGazeQualified = true }));
        Check((string)structured["current"]["semantics"]["intent"] == "return_to_target" &&
            (bool)structured["evidence"]["targetGazeQualified"] && structured["participant"] == null, "structured semantic and identifier-free evidence");
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
