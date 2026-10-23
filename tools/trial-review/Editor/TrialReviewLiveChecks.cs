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
        public int starts, stops, offsetFrames, sampleRate;
        public readonly System.Collections.Generic.Dictionary<string,int> offsets=new System.Collections.Generic.Dictionary<string,int>();
        public object Play(float[] samples, int channels, int rate, int offset, string name)
        { starts++; offsetFrames=offset; sampleRate=rate; if(!offsets.ContainsKey(name))offsets[name]=offset; return new object(); }
        public void Stop(object handle) { stops++; }
    }
    static void CheckRecordedSpeechTransport()
    {
        string path=Environment.GetEnvironmentVariable("TRIAL_REVIEW_AUDIO_RECORDING");
        if(string.IsNullOrEmpty(path))return;
        var timeline=TrialReplayFile.Read(path);
        var before=new Audio();var after=new Audio();var clock=new TrialReviewLiveAudioClock();
        double a=0,b=0,wall=0,step=1.0/60;
        using(var oldAudio=new TrialReplayAudioPreview(timeline,before))
        using(var newAudio=new TrialReplayAudioPreview(timeline,after))
        {
            // Deterministic 1.5-second delivery batches on real recorded speech timings.
            // Audio output is an instrumented sink, not a headset/acoustic measurement.
            while(wall<timeline.Duration+60 && b<timeline.Duration)
            {
                wall+=step;
                double received=Math.Min(timeline.Duration,Math.Floor(wall/1.5)*1.5);
                if(received<=0)continue;
                a=Math.Max(TrialReviewLiveFollow.Cursor(a,0,received,false),Math.Min(received,a+step));
                oldAudio.Sync(a,true,1);
                double requested=Math.Max(TrialReviewLiveFollow.Cursor(b,0,received,false),Math.Min(received,b+step));
                b=clock.Advance(b,requested,step,wall,timeline,newAudio.Plan);newAudio.Sync(b,true,1);
            }
            var expected=newAudio.Plan.Segments.Where(s=>s.End>s.Start.time).Select(s=>"Replay "+s.Start.sequence).Distinct().ToArray();
            int oldHeard=expected.Count(before.offsets.ContainsKey),newHeard=expected.Count(after.offsets.ContainsKey);
            int oldWhole=expected.Count(id=>before.offsets.ContainsKey(id)&&before.offsets[id]==0);
            int newWhole=expected.Count(id=>after.offsets.ContainsKey(id)&&after.offsets[id]==0);
            Debug.Log($"TRIAL_REVIEW_RECORDED_AUDIO_COMPARE expected={expected.Length}; before_started={oldHeard}; after_started={newHeard}; before_from_start={oldWhole}; after_from_start={newWhole}; delivery_batch_seconds=1.5; synthetic_transport=true");
            Check(newHeard==expected.Length && newWhole==expected.Length,"all available real-recording phrases survive batched delivery from their beginning");
        }
    }

    public static void Run(string source)
    {
        CheckRecordedSpeechTransport();
        var captions=new TrialReviewSubtitles();
        captions.Observe(new TrialReplayRecord{kind="audio_playback_start",time=1,sequence=1,text="Look left."});
        captions.Observe(new TrialReplayRecord{kind="audio_playback_start",time=2,sequence=2,text="success_chime",detail="success_effect;software_one_shot_start"});
        captions.Observe(new TrialReplayRecord{kind="audio_cancelled",time=3,sequence=3});
        captions.Observe(new TrialReplayRecord{kind="audio_playback_start",time=3,sequence=4,text="Look up."});
        captions.Observe(new TrialReplayRecord{kind="application_paused",time=4,sequence=5});
        Check(captions.At(.9,long.MaxValue)==null,"no caption before speech");
        Check(captions.At(2.5,long.MaxValue)=="Look left.","captions need no WAV and ignore overlapping chimes");
        Check(captions.At(3,3)==null && captions.At(3,4)=="Look up.","same-time cancellation/replacement respects sequence");
        Check(captions.At(4,long.MaxValue)==null,"pause clears caption");
        Check(captions.At(1.5,long.MaxValue)=="Look left.","rewinding restores caption");
        captions.Clear();Check(captions.At(2,long.MaxValue)==null,"run change clears captions");
        Debug.Log("TRIAL_REVIEW_SUBTITLES_PASS start, cancellation, overlap, pause, rewind, run reset, no WAV dependency");
        var loading=new TrialReviewHeadsetState{schema=1,sessionId="session",phase="preparing",progressKnown=true,progress01=.4f};
        Check(TrialReviewHeadsetState.Fresh(loading,true,100,99),"pre-trial preparation heartbeat is available without a replay");
        Check(!TrialReviewHeadsetState.Fresh(loading,true,105,99),"stale preparation cannot masquerade as live loading");
        Check(!TrialReviewHeadsetState.Fresh(loading,false,100,99),"disconnect invalidates loading");
        Check(!TrialReviewHeadsetState.Fresh(null,true,100,99),"legacy headset missing telemetry remains unknown");
        var follow = new TrialReviewLiveFollow();
        Check(TrialReviewLiveFollow.AudioEnabled(false,true,true),"short buffer underrun preserves live audio");
        Check(!TrialReviewLiveFollow.AudioEnabled(false,true,false),"stale/disconnected/paused live stream stops audio");
        Check(!TrialReviewLiveFollow.AudioEnabled(false,false,true),"paused archive does not inherit live audio permission");
        Check(TrialReviewLiveFollow.AudioEnabled(true,false,false),"ordinary recorded playback remains audible");
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
        Check(follow.WaitingReason(true, 100, 99.9, 0, true) == "Headset app paused",
            "A connected paused headset must not be labeled a connection failure");
        Check(follow.WaitingReason(false, 100, 99.9, 0, true) == "Waiting for Wi-Fi connection",
            "A real transport loss still identifies the connection problem");
        follow.Observe(new TrialReplayRecord { kind="sample", time=11, headAvailable=true, headTracked=0 });
        Check(!follow.Ready(true, 100, 99.9, 0, true), "head tracking loss waits");
        follow.Observe(new TrialReplayRecord { kind="sample", time=12, headAvailable=true, headTracked=1 });
        Check(follow.Ready(true, 100, 99.9, 0, true), "new tracked sample resumes");
        follow.Observe(new TrialReplayRecord { kind="end", time=12.1 });
        Check(!follow.Ready(true, 100, 99.9, 0, true), "completed run waits");
        Check(follow.CanDrain(12) && !follow.CanDrain(12.1),"buffered final speech drains only up to recorded end, never beyond");
        Check(Math.Abs(TrialReviewLiveFollow.Cursor(2, 0, 10, true)-9.8)<.0001, "join stays only 200 ms behind received edge");
        Check(TrialReviewLiveFollow.Cursor(9.95, 0, 10, false)==9.95, "normal follow never rewinds near edge");
        Check(Math.Abs(TrialReviewLiveFollow.Cursor(2, 0, 10, false)-9.8)<.0001, "lag catches up instead of accumulating seconds");
        string folder = Path.Combine(Path.GetTempPath(), "trial-review-live-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "recording.jsonl");
            var lines = File.ReadAllLines(source);
            // Preparation publishes WAVs before the first replay header/start.
            File.WriteAllText(path, "");
            Directory.CreateDirectory(Path.Combine(folder, "audio"));
            string cached = Path.Combine(folder, "audio/tone.wav");
            File.Copy(Path.Combine(Path.GetDirectoryName(source), "audio/tone.wav"), cached);
            using (var preparing = new TrialReviewLiveStream(path))
            {
                Check(preparing.Poll()==0, "empty preparation stream waits for header");
                var output = new Audio();
                using (var audio = new TrialReplayAudioPreview(preparing.Timeline, output))
                {
                    audio.Sync(0,true,1);
                    Check(output.starts==0,"cached clips do not play until an event arrives");
                    var start = new TrialReplayRecord {kind="audio_playback_start",sequence=1,time=1,clip="audio/tone.wav",text="Look right."};
                    File.AppendAllText(path,lines[0]+"\n"+JsonUtility.ToJson(start)+"\n");
                    preparing.Poll(10000,5000); audio.Refresh(preparing.Timeline); audio.Sync(1.2,true,1);
                    Check(output.starts==1 && Math.Abs((double)output.offsetFrames/output.sampleRate-.2)<.001,
                        "prepared clip joins at the session cursor, never at an invented onset");
                    audio.Refresh(preparing.Timeline);audio.Sync(1.3,true,1);
                    Check(output.starts==1,"cached speech is not restarted by receiver updates");
                    audio.Sync(1.3,TrialReviewLiveFollow.AudioEnabled(false,true,true),1);
                    audio.Sync(1.3,TrialReviewLiveFollow.AudioEnabled(false,true,true),1);
                    Check(output.starts==1 && output.stops==0,"buffer-edge waits do not chop or restart the live sentence");
                    File.AppendAllText(path,JsonUtility.ToJson(new TrialReplayRecord{kind="audio_cancelled",sequence=2,time=1.4})+"\n");
                    preparing.Poll(10000,5000);audio.Refresh(preparing.Timeline);audio.Sync(1.5,true,1);
                    Check(output.stops==1 && audio.ActiveCount==0,"live interruption stops cached speech");
                }
            }
            File.Delete(cached);
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
            // A received chunk spans an entire short utterance. Catch-up must visit its
            // start and play it at normal speed rather than silently leap over it.
            var complete = TrialReplayFile.Read(source);
            var clock = new TrialReviewLiveAudioClock();
            var speechOutput = new Audio();
            using (var audio = new TrialReplayAudioPreview(complete, speechOutput))
            {
                double jumped = TrialReviewLiveFollow.Cursor(.9, 0, 2, false);
                double next = clock.Advance(.9, jumped, .016, 100, complete, audio.Plan);
                audio.Sync(next, true, 1);
                Check(speechOutput.starts == 1 && speechOutput.offsetFrames == 0,
                    "catch-up must not skip an entire recorded spoken phrase");
                double during = clock.Advance(next, 1.8, .016, 100.016, complete, audio.Plan);
                audio.Sync(during, true, 1);
                Check(Math.Abs(during-1.016)<.00001 && speechOutput.starts==1,
                    "catch-up cannot cut or restart the phrase after its first frame");
                Check(clock.Advance(1.5,1.8,.016,100.5,complete,audio.Plan)==1.8,
                    "recorded cancellation releases the speech clock");
            }
            // Restore the live fixture with a temporarily unavailable asset; the scene
            // and caption cursor waits at the start even if the network is already ahead.
            string wav=Path.Combine(folder,"audio/tone.wav");
            byte[] retained=File.ReadAllBytes(wav); File.Delete(wav);
            using (var late = new TrialReviewLiveStream(path))
            {
                late.Poll(10000,5000);
                var lateClock=new TrialReviewLiveAudioClock(); var lateOutput=new Audio();
                using(var audio=new TrialReplayAudioPreview(late.Timeline,lateOutput))
                {
                    Check(lateClock.Advance(1,2,.016,200,late.Timeline,audio.Plan)==1 && lateClock.WaitingForAudio,"late WAV holds scene/caption at speech start");
                    Check(lateClock.Advance(1,2,.016,201,late.Timeline,audio.Plan)==1,"late WAV is not silently skipped");
                    File.WriteAllBytes(wav,retained); audio.Refresh(late.Timeline);
                    double at=lateClock.Advance(1,2,.016,202,late.Timeline,audio.Plan);audio.Sync(at,true,1);
                    Check(at==1 && lateOutput.starts==1 && lateOutput.offsetFrames==0 && !lateClock.WaitingForAudio,"asset arriving after original interval plays from beginning at buffered cursor");
                }
                File.Delete(wav);
                var absentClock=new TrialReviewLiveAudioClock();var absentPlan=new TrialReplayAudioPlan(late.Timeline);
                absentClock.Advance(1,2,.016,300,late.Timeline,absentPlan);
                Check(absentClock.Advance(1,2,.016,306,late.Timeline,absentPlan)>1 && absentClock.UnavailableCount==1,"permanently missing audio times out with a counted gap");
                absentClock.Advance(1,2,.016,307,late.Timeline,absentPlan);
                Check(absentClock.UnavailableCount==1,"missing audio counted once, not each tick");
                File.WriteAllBytes(wav,retained);
            }
            string snapshot=TrialReviewDetachedWindow.Snapshot(complete,Path.Combine(folder,"snapshots"));
            var detached=TrialReplayFile.Read(snapshot);
            Check(detached.Records.Count==complete.Records.Count && detached.Header.recordingId==complete.Header.recordingId,"review snapshot preserves parsed identity and complete records");
            Check(File.ReadAllText(snapshot)==File.ReadAllText(source),"snapshot preserves raw records rather than reserializing research data");
            Check(File.Exists(Path.Combine(Path.GetDirectoryName(snapshot),"audio/tone.wav")),"snapshot owns its copied played audio");
            complete.Seek(1); detached.Seek(21);
            Check(complete.State.Trial.trialId!=detached.State.Trial.trialId,"review cursor is independent of live timeline");
            string growing=Path.Combine(folder,"growing.jsonl");File.Copy(source,growing);
            var frozenPrefix=TrialReplayFile.Read(growing);
            File.AppendAllText(growing,"{partial");
            string frozen=TrialReviewDetachedWindow.Snapshot(frozenPrefix,Path.Combine(folder,"snapshots"));
            Check(File.ReadAllText(frozen)==File.ReadAllText(source),"snapshot never copies the unparsed growing tail");
            File.WriteAllText(path, lines[0] + "\n" + lines[0] + "\n");
            bool rejected = false;
            using (var live = new TrialReviewLiveStream(path))
                try { live.Poll(10000, 5000); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "duplicate header/sequence rejected");
            File.WriteAllText(path, lines[0] + "\n{broken}\n"); rejected = false;
            using (var live = new TrialReviewLiveStream(path))
                try { live.Poll(10000, 5000); } catch (Exception) { rejected = true; }
            Check(rejected, "corrupt complete record rejected");
            Debug.Log("TRIAL_REVIEW_LIVE_PASS speech catch-up fences, delayed/missing WAVs, snapshot isolation, partial tails, no duplicates, exact records, new meshes/trials, rewind, late WAV, uninterrupted speech, cancellation, malformed input");
        }
        finally { Directory.Delete(folder, true); }
    }
}
