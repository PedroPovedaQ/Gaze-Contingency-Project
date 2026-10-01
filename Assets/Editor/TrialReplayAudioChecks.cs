using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TrialReplayAudioChecks
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Output : TrialReplayAudioPreview.IOutput
    {
        public sealed class Clip { public float[] Samples; public int Rate, Channels, Offset; }
        public readonly List<Clip> Played = new List<Clip>();
        public readonly HashSet<object> Active = new HashSet<object>();
        public object Play(float[] samples, int channels, int rate, int offsetFrames, string name)
        {
            var clip = new Clip { Samples=samples, Rate=rate, Channels=channels, Offset=offsetFrames };
            Played.Add(clip); Active.Add(clip); return clip;
        }
        public void Stop(object handle) { Assert(Active.Remove(handle), "Only owned active audio is stopped once."); }
    }
    [MenuItem("Tools/Codex/Trial Replay/Verify Audio Reconstruction")]
    public static void Run()
    {
        string root = Path.Combine(Application.temporaryCachePath, "ReplayAudioChecks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var samples = new float[16000]; for (int i=0;i<samples.Length;i++) samples[i]=.4f;
            string path=Path.Combine(root,"clip.wav"); byte[] bytes=TrialReplayAudio.Encode(samples,1,8000);
            File.WriteAllBytes(path,bytes);
            var info=TrialReplayAudio.Inspect(path);
            Assert(info.Frames==16000 && info.Rate==8000 && info.Channels==1,"PCM metadata preserves frame units.");
            foreach (int offset in new[]{4,28,32,40})
            {
                var corrupt=(byte[])bytes.Clone(); corrupt[offset]^=1; File.WriteAllBytes(path,corrupt);
                Reject(()=>TrialReplayAudio.Decode(path,out _,out _),"Contradictory WAV field at "+offset+" must fail.");
            }
            File.WriteAllBytes(path,new byte[12]); Reject(()=>TrialReplayAudio.Inspect(path),"Truncated WAV header.");
            File.WriteAllBytes(path,TrialReplayAudio.Encode(new float[0],1,8000));
            Reject(()=>TrialReplayAudio.Inspect(path),"Empty WAV has no playback evidence.");
            File.WriteAllBytes(path,bytes);
            string recording=Path.Combine(root,"recording.jsonl");
            var records=new[]{
                new TrialReplayRecord{kind="header",sequence=0,recordingId="synthetic-audio",sourceType="synthetic_demo"},
                new TrialReplayRecord{kind="audio_playback_start",sequence=1,time=0,clip="clip.wav",audioVolume=.25f},
                new TrialReplayRecord{kind="audio_playback_start",sequence=2,time=.25,clip="clip.wav",audioVolume=.75f,detail="success_effect;software_one_shot_start;duration=2"},
                new TrialReplayRecord{kind="audio_cancelled",sequence=3,time=.5},
                new TrialReplayRecord{kind="audio_playback_start",sequence=4,time=.5,clip="clip.wav",audioVolume=.5f},
                new TrialReplayRecord{kind="audio_playback_end",sequence=5,time=.75},
                new TrialReplayRecord{kind="stop",sequence=6,time=1.2},
                new TrialReplayRecord{kind="end",sequence=7,time=2,complete=true}
            };
            var lines=new List<string>(); foreach(var r in records) lines.Add(JsonUtility.ToJson(r));
            File.WriteAllLines(recording,lines);
            var timeline=TrialReplayFile.Read(recording); var output=new Output();
            using(var preview=new TrialReplayAudioPreview(timeline,output))
            {
                var segments=preview.Plan.Segments;
                Assert(segments.Count==3 && segments[0].End==.5 && segments[1].End==1.2 && segments[2].End==.75,"Speech cancellation preserves overlapping effect; stop bounds both channels.");
                Assert(segments[0].Contains(.5,2) && !segments[0].Contains(.5,3) && !segments[2].Contains(.5,3) && segments[2].Contains(.5,4),"Equal-time audio boundaries honor source sequence.");
                preview.Sync(.3,true,1);
                Assert(output.Active.Count==2 && output.Played.Count==2,"Speech and effect play concurrently.");
                Assert(Math.Abs(output.Played[0].Samples[0]-.1f)<.0001 && Math.Abs(output.Played[1].Samples[0]-.3f)<.0001,"Reused WAV gets each event's own volume.");
                Assert(output.Played[0].Samples.Length==4000 && output.Played[0].Offset==2400,"Clip is cut at recorded stop and seeks in frames.");
                preview.Sync(.31,true,1); Assert(output.Played.Count==2,"Normal playback never restarts unchanged audio.");
                preview.Sync(.6,true,1,force:true);
                Assert(output.Active.Count==2 && output.Played.Count==4,"Seek reconstructs both active channels.");
                Assert(Math.Abs(output.Played[3].Samples[0]-.2f)<.0001 && Math.Abs(output.Played[3].Offset-800)<=1,"Later use preserves new gain and seek offset.");
                preview.Sync(.8,true,1); Assert(output.Active.Count==1,"Speech end does not stop the chime.");
                preview.Sync(1.3,true,1); Assert(output.Active.Count==0,"Recording stop silences owned previews.");
                preview.Sync(.3,true,2); Assert(output.Active.Count==0,"Non-1x playback is silent.");
                preview.Sync(.3,true,1); preview.Sync(.3,false,1); Assert(output.Active.Count==0,"Pause releases all owned previews.");
                preview.Sync(.3,true,1);
            }
            Assert(output.Active.Count==0,"Window disposal releases owned clips.");
            Assert(Convert.ToBase64String(File.ReadAllBytes(path))==Convert.ToBase64String(bytes),"Playback never modifies source WAV.");
            File.Delete(path);
            var missing=new TrialReplayAudioPlan(timeline);
            Assert(missing.Segments.Count==0 && missing.Warnings.Count==3,"Missing assets remain explicit, never regenerated.");
            records[1].audioVolume=2; lines[1]=JsonUtility.ToJson(records[1]);File.WriteAllLines(recording,lines);
            Reject(()=>TrialReplayFile.Read(recording),"Out-of-range saved volume is not accepted.");
            Debug.Log("[TrialReplayAudioChecks] PASS: WAV integrity, overlapping effects/speech, per-event gain, exact stop/seek, pause/speed/disposal and missing assets. Acoustic listening remains unverified.");
        }
        finally { Directory.Delete(root,true); }
    }
    static void Reject(Action action,string message)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception(message);
    }

    // Separate from the synchronous suite: a silent real-audio-engine check needs
    // Editor updates. Invoke with -batchmode -executeMethod ...RunEngine, no -quit.
    static TrialReplayAudioPreview.EditorOutput s_Output;
    static AudioSource s_First, s_Second;
    static double s_Start;
    static int s_Stage;
    static bool s_SceneWasDirty;
    public static void RunEngine()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this exit-on-completion check in a separate batch Editor.");
        EditorApplication.LockReloadAssemblies();
        s_SceneWasDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;
        s_Output = new TrialReplayAudioPreview.EditorOutput();
        s_First = (AudioSource)s_Output.Play(new float[48000*10],1,48000,24000,"Synthetic silent A");
        s_Second = (AudioSource)s_Output.Play(new float[48000*10],1,48000,0,"Synthetic silent B");
        s_Start = EditorApplication.timeSinceStartup; s_Stage = 0;
        EditorApplication.update += CheckEngine;
    }
    static void CheckEngine()
    {
        if (EditorApplication.timeSinceStartup-s_Start < .3) return;
        try
        {
            if (s_Stage==0)
            {
                Assert(s_First.isPlaying && s_Second.isPlaying && s_First.timeSamples>24000 && s_Second.timeSamples>0,
                    "Two owned Editor sources advance from their distinct seek positions.");
                s_Output.Stop(s_First); s_Stage=1; s_Start=EditorApplication.timeSinceStartup;
            }
            else
            {
                Assert(s_First==null && s_Second.isPlaying,"Stopping one source leaves the overlapping source playing.");
                s_Output.Dispose();
                Assert(s_Second==null,"Preview-scene disposal releases active audio.");
                Assert(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty==s_SceneWasDirty,
                    "Replay audio does not dirty the study scene.");
                Debug.Log("[TrialReplayAudioChecks] ENGINE PASS: silent sources advance independently, seek, stop and dispose without dirtying the scene. Listening is not verified.");
                FinishEngine(0);
            }
        }
        catch(Exception error) { Debug.LogException(error); FinishEngine(1); }
    }
    static void FinishEngine(int code)
    {
        EditorApplication.update -= CheckEngine; s_Output?.Dispose();
        EditorApplication.UnlockReloadAssemblies(); EditorApplication.Exit(code);
    }
}
