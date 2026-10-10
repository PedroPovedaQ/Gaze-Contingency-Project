using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class TrialReviewLiveChecks
{
    static void Check(bool ok, string why) { if (!ok) throw new Exception("Live replay: " + why); }
    sealed class Audio : TrialReplayAudioPreview.IOutput
    {
        public int starts, stops;
        public object Play(float[] samples, int channels, int rate, int offset, string name) { starts++; return new object(); }
        public void Stop(object handle) { stops++; }
    }
    public static void Run(string source)
    {
        var follow = new TrialReviewLiveFollow();
        follow.Observe(new TrialReplayRecord { kind="sample", time=10, headAvailable=true, headTracked=1 });
        Check(!follow.Ready(true, 100, 0, 0, true), "connected historical recording must wait");
        Check(follow.Ready(true, 100, 99.9, 0, true), "fresh tracked source follows");
        Check(!follow.Ready(true, 103, 99.9, 0, true), "stale source freezes even with Wi-Fi connected");
        Check(!follow.Ready(false, 100, 99.9, 0, true), "disconnect waits");
        Check(!follow.Ready(true, 100, 99.9, 100, true), "network backlog is not live");
        Check(!follow.Ready(true, 100, 99.9, 0, false), "parser backlog is not live");
        Check(follow.Ready(true, 100, 99.9, 0, false, true), "new chunk while following does not flash waiting");
        Check(!follow.Ready(true, 103, 99.9, 0, false, true), "following never overrides source expiry");
        follow.Observe(new TrialReplayRecord { kind="application_paused", time=10.1 });
        Check(!follow.Ready(true, 100, 99.9, 0, true), "pause marker immediately waits despite fresh flush");
        follow.Observe(new TrialReplayRecord { kind="sample", time=11, headAvailable=true, headTracked=0 });
        Check(!follow.Ready(true, 100, 99.9, 0, true), "head tracking loss waits");
        follow.Observe(new TrialReplayRecord { kind="sample", time=12, headAvailable=true, headTracked=1 });
        Check(follow.Ready(true, 100, 99.9, 0, true), "new tracked sample resumes");
        follow.Observe(new TrialReplayRecord { kind="end", time=12.1 });
        Check(!follow.Ready(true, 100, 99.9, 0, true), "completed run waits");
        Check(Math.Abs(TrialReviewLiveFollow.Cursor(2, 0, 10, true)-9.8)<.0001, "join stays only 200 ms behind received edge");
        Check(TrialReviewLiveFollow.Cursor(9.95, 0, 10, false)==9.95, "normal follow never rewinds near edge");
        Check(Math.Abs(TrialReviewLiveFollow.Cursor(2, 0, 10, false)-9.8)<.0001, "lag catches up instead of accumulating seconds");
        string folder = Path.Combine(Path.GetTempPath(), "trial-review-live-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "recording.jsonl");
            var lines = File.ReadAllLines(source);
            File.WriteAllText(path, lines[0] + "\n", new UTF8Encoding(false));
            using (var live = new TrialReviewLiveStream(path))
            {
                live.Poll(10000, 5000);
                Check(live.Timeline.Records.Count == 1 && live.Timeline.Trials.Count == 0, "header-only waits for a trial");
                // Renderer must discover meshes arriving after it was constructed.
                using (var renderer = new TrialReplayRenderer(live.Timeline) { RecordedHead = true })
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines.Skip(1)) + "\n");
                    int midpoint = bytes.Length / 2;
                    // Leave a deliberately incomplete JSON record between transfer chunks.
                    using (var f = new FileStream(path, FileMode.Append)) f.Write(bytes, 0, midpoint);
                    live.Poll(10000, 5000);
                    int prefix = live.Timeline.Records.Count;
                    Check(prefix > 1 && live.PartialTail, "partial line held without dropping preceding observations");
                    live.Poll(10000, 5000);
                    Check(prefix == live.Timeline.Records.Count, "no duplicate records while waiting");
                    renderer.Prepare(live.Timeline, 1);
                    Check(renderer.UsingRecordedHead, "live camera follows recorded pose");
                    using (var f = new FileStream(path, FileMode.Append)) f.Write(bytes, midpoint, bytes.Length - midpoint);
                    live.Poll(10000, 5000);
                    var saved = TrialReplayFile.Read(source);
                    Check(live.Complete && !live.PartialTail && live.CaughtUp, "footer and complete byte stream");
                    Check(live.Timeline.Records.Count == saved.Records.Count, "all records survive chunking");
                    for (int i = 0; i < saved.Records.Count; i++)
                        Check(JsonUtility.ToJson(saved.Records[i]) == JsonUtility.ToJson(live.Timeline.Records[i]), "record unchanged at " + i);
                    renderer.Prepare(live.Timeline, 21);
                    Check(live.Timeline.State.Trial.trialId == "demo-1", "new trial follows appended snapshot");
                    renderer.Prepare(live.Timeline, 1);
                    Check(live.Timeline.State.Trial.trialId == "demo-0", "rewind after live append");
                    // Missing WAV arrives later. Refresh must enable it and retain a playing handle.
                    var output = new Audio();
                    using (var audio = new TrialReplayAudioPreview(live.Timeline, output))
                    {
                        Check(audio.Plan.Warnings.Count > 0, "missing live assets reported");
                        Directory.CreateDirectory(Path.Combine(folder, "audio"));
                        File.Copy(Path.Combine(Path.GetDirectoryName(source), "audio/tone.wav"), Path.Combine(folder, "audio/tone.wav"));
                        audio.Refresh(live.Timeline); audio.Sync(1.2, true, 1);
                        Check(audio.ActiveCount == 1 && output.starts == 1, "late WAV plays at cursor");
                        audio.Refresh(live.Timeline); audio.Sync(1.3, true, 1);
                        Check(output.starts == 1 && output.stops == 0, "refresh does not restart speech");
                        audio.Sync(1.5, true, 1);
                        Check(audio.ActiveCount == 0 && output.stops == 1, "recorded cancellation retained");
                    }
                }
            }
            File.WriteAllText(path, lines[0] + "\n" + lines[0] + "\n");
            bool rejected = false;
            using (var live = new TrialReviewLiveStream(path))
                try { live.Poll(10000, 5000); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "duplicate header/sequence rejected");
            File.WriteAllText(path, lines[0] + "\n{broken}\n"); rejected = false;
            using (var live = new TrialReviewLiveStream(path))
                try { live.Poll(10000, 5000); } catch (Exception) { rejected = true; }
            Check(rejected, "corrupt complete record rejected");
            Debug.Log("TRIAL_REVIEW_LIVE_PASS partial tails, no duplicates, exact records, new meshes/trials, rewind, late WAV, uninterrupted speech, cancellation, malformed input");
        }
        finally { Directory.Delete(folder, true); }
    }
}
