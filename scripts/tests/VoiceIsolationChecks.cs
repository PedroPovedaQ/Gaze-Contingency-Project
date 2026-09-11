// Executes the production synthesizer against deterministic Unity/provider fakes.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

class VoiceIsolationChecks
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
    static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext()) if (routine.Current is IEnumerator child) Drain(child);
    }
    static void Set(object instance, string field, object value) => instance.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    static string Cache(VoiceSynthesizer synth, string scope) => (string)typeof(VoiceSynthesizer)
        .GetMethod("GetCachePath", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(synth, new object[] { "test phrase", scope });
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "gaze-voice-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var synth = new VoiceSynthesizer();
            Set(synth, "m_CacheDir", root);
            Set(synth, "m_ApiKey", "fake-key");
            Set(synth, "m_AudioSource", new AudioSource());
            var provider = new VoxtralClient();
            Set(synth, "m_Voxtral", provider);
            Action run = () => {
                UnityWebRequest.Requests.Clear(); provider.Calls = 0;
                synth.Speak("test phrase");
                Check(synth.IsBusy, "queued speech is busy");
                Drain(MonoBehaviour.LastRoutine);
                Check(!synth.IsBusy, "speech clears busy after completion/failure");
            };
            string female = Cache(synth, "el-21m00Tcm4TlvDq8ikWAM");
            string male = Cache(synth, "el-cjVigY5qzO86Huf0OWal");
            string self = Cache(synth, "vx-participant-clone");
            Check(female != male && male != self && female != self, "voice cache isolation");
            // A populated neutral cache must never rescue a failed self-similar request.
            File.WriteAllBytes(female, new byte[120]);
            SessionConfig.Voice = VoiceCondition.SelfSimilar;
            foreach (bool pending in new[] { false, true })
            {
                SessionConfig.SelfSimilarEnrollmentPending = pending;
                SessionConfig.SelfSimilarVoiceId = "";
                run();
                Check(UnityWebRequest.Requests.Count == 0 && provider.Calls == 0, "missing clone stays silent");
            }
            SessionConfig.SelfSimilarVoiceId = "participant-clone";
            File.WriteAllBytes(self, new byte[120]);
            run();
            Check(UnityWebRequest.Requests.Count == 0, "pending enrollment cannot play even cached clone");
            SessionConfig.SelfSimilarEnrollmentPending = false;
            File.Delete(self);
            provider.HasKey = false;
            run();
            Check(UnityWebRequest.Requests.Count == 0 && provider.Calls == 0, "missing key stays silent");
            Set(synth, "m_Voxtral", null);
            run();
            Check(UnityWebRequest.Requests.Count == 0, "missing provider stays silent");
            Set(synth, "m_Voxtral", provider); provider.HasKey = true;
            foreach (byte[] result in new byte[][] { null, new byte[0], new byte[50] })
            {
                provider.Audio = result;
                run();
                Check(provider.Calls == 1 && UnityWebRequest.Requests.Count == 0, "failed/empty self audio has no neutral fallback");
                Check(!File.Exists(self), "failed audio not cached");
                if (result == null)
                    Check(synth.LastError.Contains("simulated failure"), "provider error survives empty audio guard");
            }
            provider.Audio = new byte[120];
            run();
            Check(provider.Calls == 1 && File.Exists(self), "successful clone audio cached");
            Check(UnityWebRequest.Requests.Count == 1 && UnityWebRequest.Requests[0].StartsWith("file:"), "clone plays only its file");
            provider.HasKey = false;
            run();
            Check(provider.Calls == 0 && UnityWebRequest.Requests.Count == 0, "offline clone cache keeps identity");
            SessionConfig.Voice = VoiceCondition.Generic;
            SessionConfig.NeutralProfile = NeutralVoiceProfile.Male;
            run();
            Check(UnityWebRequest.Requests[0].EndsWith(SessionConfig.NeutralVoiceId), "male neutral endpoint selected");
            Check(File.Exists(male), "male neutral has separate cache");
            SessionConfig.NeutralProfile = NeutralVoiceProfile.Female;
            run();
            Check(UnityWebRequest.Requests.Count == 1 && UnityWebRequest.Requests[0].Contains(Path.GetFileName(female)), "female cache preserved");
            SessionConfig.ParticipantId = "P001";
            Application.TestPath = root;
            SessionConfig.ConfigureVoiceBlocks();
            Check(SessionConfig.NeutralFirst, "odd participant neutral first");
            Check(SessionConfig.VoiceForRound(6) == VoiceCondition.Generic && SessionConfig.VoiceForRound(7) == VoiceCondition.SelfSimilar, "block boundary");
            File.WriteAllText(Path.Combine(SessionConfig.ParticipantPath, "voice-order-v1.txt"), "selfsimilar_then_neutral");
            SessionConfig.ConfigureVoiceBlocks();
            Check(!SessionConfig.NeutralFirst, "persisted assignment wins on restart");
            SessionConfig.ParticipantId = "P002"; SessionConfig.ConfigureVoiceBlocks();
            Check(!SessionConfig.NeutralFirst, "even participant self first");
            provider.HasKey = true; provider.Audio = new byte[120];
            bool prepared = false;
            DownloadHandlerAudioClip.AlternatePeak = true;
            Drain(synth.PrepareLibraries(new[] { "test phrase", "another phrase" }, null, ok => prepared = ok));
            Check(prepared && synth.LibraryReady, "both libraries prepared");
            var loaded = (System.Collections.Generic.Dictionary<string, AudioClip>)typeof(VoiceSynthesizer)
                .GetField("m_PreparedClips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth);
            double? matchedRms = null;
            foreach (var clip in loaded.Values)
            {
                var samples = new float[clip.samples]; clip.GetData(samples, 0); double power = 0;
                foreach (float sample in samples) { power += sample * sample; Check(Math.Abs(sample) <= 0.901, "matched clip peak safe"); }
                double rms = Math.Sqrt(power / samples.Length);
                if (matchedRms.HasValue) Check(Math.Abs(rms - matchedRms.Value) < 0.0001, "different crest factors achieve equal RMS");
                matchedRms = rms;
            }
            DownloadHandlerAudioClip.AlternatePeak = false;
            Check(File.Exists(Path.Combine(SessionConfig.ParticipantPath, "voice-library-manifest.json")), "manifest saved");
            WaitForSeconds.Count = 0; provider.Calls = 0;
            Drain(synth.PrepareLibraries(new[] { "test phrase", "another phrase" }, null, ok => prepared = ok));
            Check(prepared && provider.Calls == 0 && WaitForSeconds.Count == 0,
                "cached preparation has no synthesis requests or artificial waits");
            SessionConfig.Voice = VoiceCondition.SelfSimilar; run();
            Check(UnityWebRequest.Requests.Count == 0 && provider.Calls == 0, "prepared trial needs no provider request");
            synth.Speak("not in manifest"); Drain(MonoBehaviour.LastRoutine);
            Check(!string.IsNullOrEmpty(synth.LastError), "unprepared content stops playback");
            DownloadHandlerAudioClip.Corrupt = true;
            Drain(synth.PrepareLibraries(new[] { "test phrase" }, null, ok => prepared = ok));
            Check(!prepared && !synth.LibraryReady, "corrupt audio blocks readiness");
            Check(synth.PreparationStage.Contains("neutral"), "failure reports the actual preparation stage");
            Check(File.ReadAllText(Path.Combine(SessionConfig.ParticipantPath, "voice-preparation-errors.log"))
                .Contains(synth.LastError), "preparation error survives device log rotation");
            DownloadHandlerAudioClip.Corrupt = false;
            provider.Audio = null; provider.Error = "tts HTTP 403: guardrail_violation"; provider.Calls = 0;
            Drain(synth.PrepareLibraries(new[] { "policy test phrase" }, null, ok => prepared = ok));
            Check(!prepared && synth.ProviderPolicyBlocked && provider.Calls == 1,
                "policy rejection blocks readiness without automatic retries");
            provider.Audio = new byte[120]; provider.Texts.Clear();
            SessionConfig.Voice = VoiceCondition.SelfSimilar;
            SessionConfig.SelfSimilarEnrollmentPending = true;
            Drain(synth.SpeakRecordingIntroduction());
            Check(string.IsNullOrEmpty(synth.LastError) && SessionConfig.Voice == VoiceCondition.SelfSimilar && provider.Texts.Count == 0,
                "recording introduction uses neutral before clone enrollment and restores the assigned voice");
            SessionConfig.SelfSimilarEnrollmentPending = false;
            Drain(synth.SpeakProcessingStatus(false));
            Check(SessionConfig.Voice == VoiceCondition.SelfSimilar && provider.Texts.Count == 0,
                "setup status uses neutral and restores the assigned voice before libraries exist");
            string targetPrompt = "Locate the Blue Cube.";
            string practicePrompt = "This is a practice round. It does not count toward the study. " + targetPrompt;
            Drain(synth.PrepareLibraries(new[] { targetPrompt, practicePrompt }, null, ok => prepared = ok));
            Check(prepared && provider.Texts.Contains("Let's find the blue cube."),
                "self-similar synthesis receives first-person target wording");
            Check(provider.Texts.Contains("Let's try a practice round. This one does not count toward our study rounds. Let's find the blue cube."),
                "self-similar practice keeps its explicit practice declaration");
            provider.Calls = 0; UnityWebRequest.Requests.Clear();
            synth.Speak(targetPrompt); Drain(MonoBehaviour.LastRoutine);
            Check(string.IsNullOrEmpty(synth.LastError) && provider.Calls == 0 && UnityWebRequest.Requests.Count == 0,
                "runtime transformation resolves the prepared self-similar clip without network requests");
            Drain(synth.SpeakProcessingStatus(true));
            Check(SessionConfig.Voice == VoiceCondition.SelfSimilar && synth.LibraryReady,
                "completion status preserves voice and library readiness");
            string hintSource = File.ReadAllText("Assets/HintGenerator.cs");
            hintSource = hintSource.Substring(hintSource.IndexOf("// --- VERY CLOSE", StringComparison.Ordinal));
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(hintSource, "\"([^\"]+)\""))
            {
                string firstPerson = VoicePromptText.SelfSimilar(match.Groups[1].Value);
                Check(firstPerson.Contains("We're") || firstPerson.Contains("Let's"), "every live hint has first-person wording");
                Check(!System.Text.RegularExpressions.Regex.IsMatch(firstPerson, @"\byou(r|'re)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    "self-similar hints do not address the participant in the second person");
            }
            Check(ChallengeSet.TotalRounds == 14 && ChallengeSet.RoundsPerBlock == 7 && ChallengeSet.BlockCount == 2, "full two-block schedule");
            for (int practice = 0; practice < 2; practice++)
            {
                var round = ChallengeSet.PracticeRound(practice); int targets = 0;
                foreach (var obj in round.objects) if (obj.shape == round.target.shape && obj.color == round.target.color) targets++;
                Check(round.objects.Length == 56 && targets == 1, "practice has one target and 55 distractors");
            }
            Check(File.ReadAllText("Assets/Scenes/GazeContingencyStudyScene.unity").Contains("m_UseDebugRoundCountOverride: 0"), "build scene must not truncate voice blocks");
            var clock = new StudyTrialClock();
            Check(clock.Elapsed(999) == 0, "pre-onset delay excluded");
            clock.Begin(1000); clock.Pause(1008);
            Check(clock.Elapsed(2000) == 8, "pause excluded");
            clock.Resume(2000); clock.Resume(2001); clock.Pause(2006);
            Check(clock.Elapsed(3000) == 14, "search resumes without resetting elapsed");
            clock.Begin(4000); clock.Pause(4003);
            Check(clock.Elapsed(6000) == 3, "next trial starts a fresh search clock");
            SessionConfig.ResetForNewParticipant();
            Console.WriteLine("PASS: missing/pending clone, missing provider/key, failed/short audio, offline cache, neutral selection and busy-state isolation.");
        }
        finally { Directory.Delete(root, true); }
    }
}

namespace UnityEngine
{
    public struct Color { public float r, g, b, a; public Color(float r,float g,float b,float a=1) { this.r=r;this.g=g;this.b=b;this.a=a; } }
    public static class Mathf { public static int Clamp(int value,int min,int max) => Math.Min(max, Math.Max(min,value)); }
    public class MonoBehaviour
    {
        protected static void Destroy(object value) { }
        public static IEnumerator LastRoutine;
        public GameObject gameObject = new GameObject();
        public T GetComponent<T>() where T : new() => new T();
        protected Coroutine StartCoroutine(IEnumerator routine) { LastRoutine = routine; routine.MoveNext(); return new Coroutine(); }
        protected void StopCoroutine(Coroutine routine) { }
    }
    public class GameObject { public T AddComponent<T>() where T : new() => new T(); }
    public class Coroutine { }
    public class WaitForSeconds { public static int Count; public WaitForSeconds(float seconds) { Count++; } }
    public class AudioSource { public float spatialBlend, volume, pitch; public bool playOnAwake; int playPolls; public bool isPlaying => playPolls-- > 0; public AudioClip clip; public void Play() { playPolls = 2; } public void Stop() { playPolls = 0; } }
    public class AudioClip
    {
        public float length = 1f; public int samples => m_Data.Length; public int channels = 1;
        float[] m_Data = new float[1000];
        public AudioClip(bool skewed = false) { for (int i=0; i<m_Data.Length; i++) m_Data[i] = skewed ? (i == 0 ? 1f : 0.02f) : 0.2f; }
        public bool GetData(float[] data, int offset) { Array.Copy(m_Data, data, data.Length); return true; }
        public bool SetData(float[] data, int offset) { Array.Copy(data, m_Data, data.Length); return true; }
    }
    public static class AudioSettings { public static double dspTime => 1; }
    public enum AudioType { MPEG }
    public static class Application { public static string TestPath = Path.GetTempPath(); public static string persistentDataPath => TestPath; }
    public static class Debug { public static void Log(object x) { } public static void LogWarning(object x) { } }
    public static class JsonUtility { public static string ToJson(object x, bool pretty = false) => "{}"; }
}
namespace UnityEngine.Networking
{
    public class UploadHandlerRaw { public UploadHandlerRaw(byte[] bytes) { } }
    public class DownloadHandlerBuffer { public byte[] data = new byte[120]; public string text = ""; }
    public class UnityWebRequest : IDisposable
    {
        public static System.Collections.Generic.List<string> Requests = new System.Collections.Generic.List<string>();
        public enum Result { Success }
        public Result result; public string error; public long responseCode; public int timeout;
        public UploadHandlerRaw uploadHandler; public DownloadHandlerBuffer downloadHandler = new DownloadHandlerBuffer();
        string url;
        public UnityWebRequest(string url, string method) { this.url = url; }
        public void SetRequestHeader(string k, string v) { }
        public IEnumerator SendWebRequest() { Requests.Add(url); yield return null; }
        public void Dispose() { }
    }
    public static class UnityWebRequestMultimedia { public static UnityWebRequest GetAudioClip(string path, AudioType type) => new UnityWebRequest(path, "GET"); }
    public static class DownloadHandlerAudioClip { public static bool Corrupt, AlternatePeak; static int count; public static AudioClip GetContent(UnityWebRequest request) => Corrupt ? null : new AudioClip(AlternatePeak && count++ % 2 == 0); }
}
public class VoxtralClient
{
    public bool HasKey = true; public byte[] Audio; public int Calls; public string Error = "simulated failure";
    public System.Collections.Generic.List<string> Texts = new System.Collections.Generic.List<string>();
    public void Initialize(string key) { }
    public IEnumerator Synthesize(string text, string voice, Action<byte[]> ok, Action<string> error)
    {
        Calls++; Texts.Add(text); yield return null;
        if (Audio == null) error(Error); else ok(Audio);
    }
}
