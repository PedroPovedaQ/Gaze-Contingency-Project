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
        while (routine.MoveNext()) { Time.unscaledTime += 0.05f; if (routine.Current is IEnumerator child) Drain(child); }
    }
    static void Set(object instance, string field, object value) => instance.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    static string Cache(VoiceSynthesizer synth, string scope) => (string)typeof(VoiceSynthesizer)
        .GetMethod("GetCachePath", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(synth, new object[] { "test phrase", scope });
    static string Formatted(string text) => VoicePromptText.Format(text, SessionConfig.Perspective);
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "gaze-voice-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            SessionConfig.ResetForNewParticipant();
            Check(SessionConfig.TrySetPerspective(VoicePerspective.External), "perspective can be selected before setup lock");
            SessionConfig.LockPerspective();
            Check(!SessionConfig.TrySetPerspective(VoicePerspective.Collaborative), "perspective change rejected after setup lock");
            SessionConfig.ResetForNewParticipant();
            Check(SessionConfig.Perspective == VoicePerspective.Collaborative && !SessionConfig.PerspectiveLocked,
                "new participant resets perspective and lock");
            Check(SessionConfig.TrySetPerspective(VoicePerspective.External), "new participant can select a new perspective");
            string externalText = Formatted("Locate the Blue Cube.");
            SessionConfig.ResetForNewParticipant();
            string collaborativeText = Formatted("Locate the Blue Cube.");
            Check(externalText != collaborativeText, "perspectives produce distinct formatted text");

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
            Check(SessionConfig.TrySetPerspective(VoicePerspective.External), "perspective can change before library preparation");
            string externalCache = Cache(synth, "el-21m00Tcm4TlvDq8ikWAM");
            Check(externalCache != female, "perspective cache isolation for identical text");
            SessionConfig.ResetForNewParticipant();
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
                "self-similar synthesis receives the selected formatted target wording");
            Check(UnityWebRequest.RequestBodies.Exists(body => body.Contains("Let's find the blue cube.")),
                "neutral synthesis receives the same formatted target wording");
            Check(provider.Texts.Contains("Let's try a practice round. This one does not count toward our study rounds. Let's find the blue cube."),
                "self-similar practice keeps its explicit practice declaration");
            provider.Calls = 0; UnityWebRequest.Requests.Clear();
            synth.Speak(targetPrompt); Drain(MonoBehaviour.LastRoutine);
            Check(string.IsNullOrEmpty(synth.LastError) && provider.Calls == 0 && UnityWebRequest.Requests.Count == 0,
                "runtime transformation resolves the prepared self-similar clip without network requests");
            Drain(synth.SpeakProcessingStatus(true));
            Check(SessionConfig.Voice == VoiceCondition.SelfSimilar && synth.LibraryReady,
                "completion status preserves voice and library readiness");
            string lazyPrompt = "Locate the Purple Sphere.";
            provider.Calls = 0;
            Drain(synth.PrepareLibraries(new[] { targetPrompt }, null, ok => prepared = ok,
                new[] { targetPrompt, lazyPrompt, "Locate the Red Cube." }));
            Check(prepared && provider.Calls == 0 && loaded.Count == 2,
                "starter preparation loads only starter clips, leaving later trials untouched");
            SessionConfig.BeginRun();
            UnityWebRequest.Requests.Clear();
            synth.Speak(lazyPrompt, "round"); Drain(MonoBehaviour.LastRoutine);
            Check(string.IsNullOrEmpty(synth.LastError) && provider.Calls == 1 &&
                provider.Texts.Contains("Let's find the purple sphere.") &&
                UnityWebRequest.Requests.TrueForAll(url => url.StartsWith("file:")),
                "first-use trial synthesizes in the assigned clone with first-person wording and no neutral request");
            Check(File.Exists(SessionConfig.GetFilePath("voice-library-manifest.json")),
                "on-demand clip updates the active run manifest");
            matchedRms = null;
            foreach (var clip in loaded.Values)
            {
                var samples = new float[clip.samples]; clip.GetData(samples, 0); double power = 0;
                foreach (float sample in samples) power += sample * sample;
                double rms = Math.Sqrt(power / samples.Length);
                if (matchedRms.HasValue) Check(Math.Abs(rms - matchedRms.Value) < 0.0001,
                    "on-demand clip matches accepted starter RMS");
                matchedRms = rms;
            }
            provider.Calls = 0; UnityWebRequest.Requests.Clear();
            synth.Speak(lazyPrompt); Drain(MonoBehaviour.LastRoutine);
            Check(provider.Calls == 0 && UnityWebRequest.Requests.Count == 0,
                "repeated on-demand phrase plays from memory without a request");
            synth.Speak("not authorized"); Drain(MonoBehaviour.LastRoutine);
            Check(!string.IsNullOrEmpty(synth.LastError) && provider.Calls == 0,
                "progressive mode still rejects phrases outside the study library");
            SessionConfig.Voice = VoiceCondition.Generic;
            synth.Speak(lazyPrompt); Drain(MonoBehaviour.LastRoutine);
            Check(string.IsNullOrEmpty(synth.LastError) && provider.Calls == 0 &&
                UnityWebRequest.Requests.Exists(url => url.EndsWith(SessionConfig.NeutralVoiceId)),
                "same phrase in neutral block gets separate neutral audio");
            SessionConfig.Voice = VoiceCondition.SelfSimilar;
            SessionConfig.SelfSimilarEnrollmentPending = true;
            synth.Speak(lazyPrompt); Drain(MonoBehaviour.LastRoutine);
            Check(!string.IsNullOrEmpty(synth.LastError) && provider.Calls == 0,
                "cached on-demand audio cannot bypass enrollment readiness");
            SessionConfig.SelfSimilarEnrollmentPending = false;
            provider.Audio = null; provider.Calls = 0; UnityWebRequest.Requests.Clear();
            synth.Speak("Locate the Red Cube."); Drain(MonoBehaviour.LastRoutine);
            Check(!string.IsNullOrEmpty(synth.LastError) && !synth.IsBusy && provider.Calls == 1 &&
                UnityWebRequest.Requests.Count == 0, "on-demand failure releases busy state without voice fallback");
            provider.Audio = new byte[120];
            synth.Speak(targetPrompt); Drain(MonoBehaviour.LastRoutine);
            int backgroundPlayback = 0;
            synth.Telemetry += (kind, context, clip, details) => {
                if (kind == "audio_playback_start" && context == "prefetch") backgroundPlayback++;
            };
            var update = typeof(VoiceSynthesizer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            provider.Calls = 0;
            synth.StartBackgroundLoading(new[] { "Locate the Red Cube." });
            update.Invoke(synth, null);
            Check(!synth.IsBusy, "background work does not gate the trial or audio-check controls");
            synth.Speak(targetPrompt); Drain(MonoBehaviour.LastRoutine);
            Check(provider.Calls == 0, "foreground playback preempts queued background synthesis");
            update.Invoke(synth, null); Drain(MonoBehaviour.LastRoutine);
            update.Invoke(synth, null); Drain(MonoBehaviour.LastRoutine);
            Check(provider.Calls == 1 && backgroundPlayback == 0 && SessionConfig.Voice == VoiceCondition.SelfSimilar,
                "background resumes, prepares both voices silently and preserves the active condition");
            provider.Calls = 0; UnityWebRequest.Requests.Clear();
            synth.Speak("Locate the Red Cube."); Drain(MonoBehaviour.LastRoutine);
            Check(string.IsNullOrEmpty(synth.LastError) && provider.Calls == 0 && UnityWebRequest.Requests.Count == 0,
                "background-prepared self clip is ready for immediate playback");
            SessionConfig.Voice = VoiceCondition.Generic;
            synth.Speak("Locate the Red Cube."); Drain(MonoBehaviour.LastRoutine);
            Check(string.IsNullOrEmpty(synth.LastError) && UnityWebRequest.Requests.Count == 0,
                "background-prepared neutral clip uses its own cache");
            string failedBackground = "Locate the Yellow Sphere.";
            Drain(synth.PrepareLibraries(new[] { targetPrompt }, null, ok => prepared = ok,
                new[] { targetPrompt, failedBackground }));
            int foregroundFailures = 0;
            synth.PlaybackFailed += error => foregroundFailures++;
            synth.StartBackgroundLoading(new[] { failedBackground });
            update.Invoke(synth, null); Drain(MonoBehaviour.LastRoutine);
            provider.Audio = null;
            update.Invoke(synth, null); Drain(MonoBehaviour.LastRoutine);
            Check(foregroundFailures == 0 && string.IsNullOrEmpty(synth.LastError) && !synth.IsBusy,
                "background failure cannot stop the trial or poison accepted audio checks");
            SessionConfig.Voice = VoiceCondition.SelfSimilar;
            synth.Speak(failedBackground); Drain(MonoBehaviour.LastRoutine);
            Check(foregroundFailures == 1 && !string.IsNullOrEmpty(synth.LastError),
                "a failed background clip still requires valid audio when requested for playback");
            provider.Audio = new byte[120];
            string hintSource = File.ReadAllText("Assets/HintGenerator.cs");
            hintSource = hintSource.Substring(hintSource.IndexOf("// --- VERY CLOSE", StringComparison.Ordinal));
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(hintSource, "\"([^\"]+)\""))
            {
                string source = match.Groups[1].Value;
                string collaborative = VoicePromptText.Format(source, VoicePerspective.Collaborative);
                string external = VoicePromptText.Format(source, VoicePerspective.External);
                Check(!string.IsNullOrEmpty(collaborative) && !string.IsNullOrEmpty(external), "every live hint has both perspective renderings");
            }
            var hintMatches = System.Text.RegularExpressions.Regex.Matches(hintSource, "\"([^\"]+)\"");
            var hintPhrases = new string[hintMatches.Count];
            for (int i = 0; i < hintMatches.Count; i++) hintPhrases[i] = hintMatches[i].Groups[1].Value;
            provider.Texts.Clear(); UnityWebRequest.RequestBodies.Clear();
            Drain(synth.PrepareLibraries(hintPhrases, null, ok => prepared = ok));
            Check(prepared, "all live hints prepare in both voices");
            foreach (string phrase in hintPhrases)
            {
                string formatted = VoicePromptText.Format(phrase, SessionConfig.Perspective);
                Check(provider.Texts.Contains(formatted), "self-similar hint uses shared formatted text");
                Check(UnityWebRequest.RequestBodies.Exists(body => body.Contains(formatted)), "neutral hint uses shared formatted text");
            }
            // Later clips may have less headroom than the accepted starter samples.
            foreach (bool quiet in new[] { false, true })
            {
                DownloadHandlerAudioClip.ForceSkewed = false;
                DownloadHandlerAudioClip.Quiet = false;
                Drain(synth.PrepareLibraries(new[] { targetPrompt }, null, ok => prepared = ok,
                    new[] { targetPrompt, lazyPrompt }));
                Check(prepared, "normal starter samples prepare before limited clip");
                DownloadHandlerAudioClip.ForceSkewed = !quiet;
                DownloadHandlerAudioClip.Quiet = quiet;
                SessionConfig.Voice = VoiceCondition.SelfSimilar;
                int failures = 0, limited = 0, starts = 0;
                Action<string> failure = reason => failures++;
                Action<string, string, string, string> telemetry = (kind, context, key, detail) => {
                    if (kind == "audio_level_limited") limited++;
                    if (kind == "audio_playback_start") starts++;
                };
                synth.PlaybackFailed += failure; synth.Telemetry += telemetry;
                synth.Speak(lazyPrompt, "round"); Drain(MonoBehaviour.LastRoutine);
                Check(string.IsNullOrEmpty(synth.LastError) && failures == 0 && starts == 1,
                    "valid later clip with limited headroom must play instead of aborting trial");
                Check(limited == 1, "limited normalization is recorded in telemetry");
                var audit = (IList)typeof(VoiceSynthesizer).GetField("m_Audit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth);
                var last = audit[audit.Count - 1]; var auditType = last.GetType();
                float gain = (float)auditType.GetField("gain").GetValue(last);
                float achieved = (float)auditType.GetField("achieved_rms").GetValue(last);
                float target = (float)typeof(VoiceSynthesizer).GetField("m_MatchedRms", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth);
                float peak = (float)auditType.GetField("peak").GetValue(last);
                Check(gain <= 4f && peak * gain <= 0.901f && achieved < target,
                    "limited clip respects gain and peak caps and records actual RMS");
                provider.Calls = 0; UnityWebRequest.Requests.Clear();
                synth.Speak(lazyPrompt); Drain(MonoBehaviour.LastRoutine);
                Check(provider.Calls == 0 && UnityWebRequest.Requests.Count == 0 && limited == 1,
                    "limited clip remains cached instead of being discarded and regenerated");
                Set(synth, "m_CurrentContext", "round");
                Check(!synth.TryAreaCorrection(lazyPrompt, () => true), "target instruction cannot be interrupted");
                Set(synth, "m_CurrentContext", "tip");
                Check(!synth.TryAreaCorrection("unprepared", () => true), "correction must be preloaded");
                Check(!synth.TryAreaCorrection(lazyPrompt, () => false), "stale gaze cannot start correction");
                var source = (AudioSource)typeof(VoiceSynthesizer).GetField("m_AudioSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth);
                source.Play();
                source.HoldPlayback = true;
                int stopsBeforeCorrection = source.StopCalls;
                bool relevant = true;
                Check(synth.TryAreaCorrection(lazyPrompt, () => relevant), "playing hint accepts fade");
                var abandonedFade = MonoBehaviour.LastRoutine;
                abandonedFade.MoveNext();
                Time.unscaledTime += 0.02f;
                abandonedFade.MoveNext();
                Check(source.volume > 0 && source.volume < 0.7f, "ongoing hint fades gradually");
                relevant = false; Drain(abandonedFade);
                Check(Math.Abs(source.volume - 0.7f) < 0.0001f && starts == 2,
                    "leaving target zone during fade restores volume and cancels correction");
                Check(synth.TryAreaCorrection(lazyPrompt, () => true), "prepared correction can replace a hint");
                var fade = MonoBehaviour.LastRoutine;
                Check(synth.IsBusy, "pending fade blocks ordinary hints");
                Drain(fade);
                Drain(MonoBehaviour.LastRoutine);
                Check(!source.HoldPlayback && source.StopCalls > stopsBeforeCorrection, "correction cuts off unfinished speech");
                Check(starts == 3 && failures == 0, "correction plays once without failure");
                Check(provider.Calls == 0 && UnityWebRequest.Requests.Count == 0, "correction needs no network");
                synth.PlaybackFailed -= failure; synth.Telemetry -= telemetry;
            }
            DownloadHandlerAudioClip.ForceSkewed = false;
            DownloadHandlerAudioClip.Quiet = false;
            Check(ChallengeSet.TotalRounds == 14 && ChallengeSet.RoundsPerBlock == 7 && ChallengeSet.BlockCount == 2, "full two-block schedule");
            foreach (var trial in ChallengeSet.Rounds)
            {
                int targetCount = 0, colorOnly = 0, shapeOnly = 0;
                foreach (var obj in trial.objects)
                {
                    bool sameColor = obj.color == trial.target.color, sameShape = obj.shape == trial.target.shape;
                    if (sameColor && sameShape) targetCount++;
                    else if (sameColor) colorOnly++;
                    else if (sameShape) shapeOnly++;
                }
                Check(trial.objects.Length == 168 && targetCount == 1 && colorOnly == 39 && shapeOnly == 39,
                    "168-object trials preserve one target and scaled conjunction distractors");
            }
            for (int practice = 0; practice < 2; practice++)
            {
                var round = ChallengeSet.PracticeRound(practice); int targets = 0;
                foreach (var obj in round.objects) if (obj.shape == round.target.shape && obj.color == round.target.color) targets++;
                Check(round.objects.Length == 168 && targets == 1, "practice has one target and 167 distractors");
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
    public static class Time { public static float unscaledTime; }
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
    public class AudioSource { public float spatialBlend, volume, pitch; public bool playOnAwake; int playPolls; public bool HoldPlayback; public int StopCalls; public bool isPlaying => HoldPlayback || playPolls-- > 0; public AudioClip clip; public void Play() { playPolls = 2; } public void Stop() { playPolls = 0; HoldPlayback = false; StopCalls++; } }
    public class AudioClip
    {
        public float length = 1f; public int samples => m_Data.Length; public int channels = 1;
        float[] m_Data = new float[1000];
        public AudioClip(bool skewed = false, bool quiet = false) { for (int i=0; i<m_Data.Length; i++) m_Data[i] = skewed ? (i == 0 ? 1f : 0.02f) : quiet ? 0.001f : 0.2f; }
        public bool GetData(float[] data, int offset) { Array.Copy(m_Data, data, data.Length); return true; }
        public bool SetData(float[] data, int offset) { Array.Copy(data, m_Data, data.Length); return true; }
    }
    public static class AudioSettings { public static double dspTime => 1; }
    public enum AudioType { MPEG }
    public static class Application { public static string TestPath = Path.GetTempPath(); public static string persistentDataPath => TestPath; }
    public static class Debug { public static void Log(object x) { } public static void LogWarning(object x) { } }
    public static class JsonUtility
    {
        public static string ToJson(object x, bool pretty = false)
        {
            var type = x.GetType();
            var text = type.GetField("text")?.GetValue(x) as string;
            var model = type.GetField("model_id")?.GetValue(x) as string;
            if (text != null || model != null)
                return "{\"text\":\"" + (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") +
                    "\",\"model_id\":\"" + (model ?? "") + "\"}";
            return "{}";
        }
    }
}
namespace UnityEngine.Networking
{
    public class UploadHandlerRaw { public byte[] Bytes; public UploadHandlerRaw(byte[] bytes) { Bytes = bytes; } }
    public class DownloadHandlerBuffer { public byte[] data = new byte[120]; public string text = ""; }
    public class UnityWebRequest : IDisposable
    {
        public static System.Collections.Generic.List<string> Requests = new System.Collections.Generic.List<string>();
        public static System.Collections.Generic.List<string> RequestBodies = new System.Collections.Generic.List<string>();
        public enum Result { Success }
        public Result result; public string error; public long responseCode; public int timeout;
        public UploadHandlerRaw uploadHandler; public DownloadHandlerBuffer downloadHandler = new DownloadHandlerBuffer();
        string url;
        public UnityWebRequest(string url, string method) { this.url = url; }
        public void SetRequestHeader(string k, string v) { }
        public IEnumerator SendWebRequest() {
            Requests.Add(url);
            if (uploadHandler != null && uploadHandler.Bytes != null)
                RequestBodies.Add(System.Text.Encoding.UTF8.GetString(uploadHandler.Bytes));
            yield return null;
        }
        public void Dispose() { }
    }
    public static class UnityWebRequestMultimedia { public static UnityWebRequest GetAudioClip(string path, AudioType type) => new UnityWebRequest(path, "GET"); }
    public static class DownloadHandlerAudioClip { public static bool Corrupt, AlternatePeak, ForceSkewed, Quiet; static int count; public static AudioClip GetContent(UnityWebRequest request) => Corrupt ? null : new AudioClip(ForceSkewed || (AlternatePeak && count++ % 2 == 0), Quiet); }
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
