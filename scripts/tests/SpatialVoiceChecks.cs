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
    public struct NavigationPhrase { public string id, text; public NavigationTone tone; }
}
class SpatialVoiceChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    static int Main(string[] args) { try { Run().GetAwaiter().GetResult(); if (args.Length == 2 && args[0] == "--live") Live(args[1]).GetAwaiter().GetResult(); if (args.Length == 2 && args[0] == "--live-interruption") LiveInterruption(args[1]).GetAwaiter().GetResult(); if (args.Length == 2 && args[0] == "--live-eleven") LiveEleven(args[1]).GetAwaiter().GetResult(); Console.WriteLine("PASS spatial voice contracts, PCM boundaries, fragmented socket audio, limits and cancellation"); return 0; } catch (Exception e) { Console.Error.WriteLine(e); return 1; } }
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
    static async Task LiveInterruption(string configurationPath)
    {
        await Live(configurationPath); // Warm the same HTTP client before evaluating the 400ms budget.
        var client = new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(configurationPath))["jev_key"]);
        var scenarios = new[] {
            "Current instruction: Turn way right toward the back. Proposed correction: You're in the right area. Look down. Stable evidence: gaze_target_plane. Playback elapsed 0.8s; remaining 1.4s. User has reached the target wall and further right is wrong.",
            "Current instruction: Look down a little. Proposed correction: Look a little down. Stable evidence: same direction. Playback elapsed 0.8s; remaining 0.7s. The guidance still matches.",
            "Current instruction: Keep turning to your right. Proposed correction: Go back to your left. Stable evidence: target_bearing_overshoot. Playback elapsed 0.8s; remaining 1.4s. Further right moves away from the target.",
            "Current instruction: You're looking right at it. Proposed correction: Look left. Stable evidence: brief jitter, not sustained. Playback elapsed 0.8s; remaining 0.7s. Confirmation still relevant." };
        for (int i=0;i<scenarios.Length;i++)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            try { Console.WriteLine("LIVE interruption case="+i+";fade="+await client.ShouldInterruptAsync(scenarios[i], CancellationToken.None)+";ms="+timer.ElapsedMilliseconds); }
            catch (OperationCanceledException) { Console.WriteLine("LIVE interruption case="+i+";deadline_fallback=finish_phrase;ms="+timer.ElapsedMilliseconds); }
        }
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
        Check((string)payload["questions"]["navigation_phrase"]["criteria"]["Look left."] != null, "actual TypeSafe criteria schema");
        Check((string)payload["state"] == "Target -45 degrees.", "state retained");
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
