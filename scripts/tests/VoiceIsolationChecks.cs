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
    // RMS over the manifest's speech window (words only, excluding any effect tail); checks peak safety too.
    static double SpeechRms(VoiceSynthesizer synth, string path, AudioClip clip)
    {
        int speech = -1;
        foreach (var audit in (IEnumerable)typeof(VoiceSynthesizer)
            .GetField("m_Audit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth))
            if ((string)audit.GetType().GetField("clip_id").GetValue(audit) == Path.GetFileNameWithoutExtension(path))
            {
                speech = (int)audit.GetType().GetField("speech_samples").GetValue(audit);
                bool clone = (string)audit.GetType().GetField("voice_condition").GetValue(audit) == "selfsimilar";
                int fullLength = clip.samples * clip.channels;
                int expectedWindow = clone ? fullLength - (int)Math.Round(clip.frequency * 0.4f) * clip.channels : fullLength;
                Check(speech == expectedWindow, "neutral manifest covers full clip; clone manifest excludes exactly the room tail");
            }
        Check(speech > 0, "manifest records the speech window");
        var samples = new float[clip.samples * clip.channels]; clip.GetData(samples, 0); double power = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            Check(Math.Abs(samples[i]) <= 0.901, "matched clip peak safe");
            if (i < speech) power += samples[i] * samples[i];
        }
        return Math.Sqrt(power / speech);
    }
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "gaze-voice-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            // Exercise real DSP: exact neutral bypass, no onset delay, stereo room tail,
            // determinism and silence. Library tests below check treated RMS/peaks.
            var impulse = new float[4800]; impulse[0] = 0.5f;
            var dryImpulse = (float[])impulse.Clone();
            var bypass = InnerThoughtVoice.Process(dryImpulse, 1, 48000, false);
            Check(bypass.Samples == dryImpulse && bypass.Channels == 1 && bypass.SpeechSamples == dryImpulse.Length,
                "neutral bypass returns the decoded buffer");
            for (int i = 0; i < impulse.Length; i++) Check(impulse[i] == dryImpulse[i], "neutral bypass is exact");
            var treated = InnerThoughtVoice.Process(impulse, 1, 48000, true);
            int treatedFrames = treated.Samples.Length / 2;
            Check(treated.Channels == 2 && treatedFrames == 4800 + 19200 && treated.SpeechSamples == 4800 * 2,
                "stereo output with 0.4 s room tail; speech window covers only the words");
            Check(treated.Samples[0] > 0.1f && Math.Abs(treated.Samples[0] - treated.Samples[1]) < 1e-6f,
                "words start at sample 0, centred");
            bool centredBeforeRoom = true, wideRoom = false;
            for (int f = 0; f < treatedFrames; f++)
            {
                float side = Math.Abs(treated.Samples[f * 2] - treated.Samples[f * 2 + 1]);
                if (f < 380) centredBeforeRoom &= side < 1e-6f; else wideRoom |= side > 1e-5f;
            }
            Check(centredBeforeRoom && wideRoom, "room arrives after its 8 ms gap, decorrelated per ear");
            Check(Math.Abs(treated.Samples[treated.Samples.Length - 1]) < 1e-4f, "room tail decays inside the clip");
            var again = InnerThoughtVoice.Process((float[])impulse.Clone(), 1, 48000, true);
            for (int i = 0; i < again.Samples.Length; i++) Check(again.Samples[i] == treated.Samples[i], "treatment is deterministic");
            var silence = InnerThoughtVoice.Process(new float[4800], 1, 48000, true);
            Check(Array.TrueForAll(silence.Samples, x => x == 0), "no residual audio across phrases");
            bool rejected = false;
            try { InnerThoughtVoice.Process(new float[3], 2, 48000, true); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "malformed interleaved PCM rejected");
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
            var provider = new ElevenLabsVoiceClient();
            Set(synth, "m_CloneClient", provider);
            Action run = () => {
                UnityWebRequest.Requests.Clear(); provider.Calls = 0;
                synth.Speak("test phrase");
                Check(synth.IsBusy, "queued speech is busy");
                Drain(MonoBehaviour.LastRoutine);
                Check(!synth.IsBusy, "speech clears busy after completion/failure");
            };
            string female = Cache(synth, "el-21m00Tcm4TlvDq8ikWAM");
            string male = Cache(synth, "el-cjVigY5qzO86Huf0OWal");
            string self = Cache(synth, "el-clone-participant-clone");
            Check(female != male && male != self && female != self, "voice cache isolation");
            Check(SessionConfig.TrySetPerspective(VoicePerspective.External), "perspective can change before library preparation");
            string externalCache = Cache(synth, "el-21m00Tcm4TlvDq8ikWAM");
            Check(externalCache != female, "perspective cache isolation for identical text");
            SessionConfig.ResetForNewParticipant();
            // A populated neutral cache must never rescue a failed self-similar request.
            const string introduction = "Welcome to the surrounding search. Stay seated at the center. Objects will appear around you.";
            foreach (var introGender in new[] { NeutralVoiceProfile.Male, NeutralVoiceProfile.Female })
                for (int introOption = 0; introOption < SessionConfig.NeutralVoiceCount; introOption++)
                {
                    SessionConfig.SelectNeutralVoice(introGender, introOption);
                    SessionConfig.Voice = VoiceCondition.SelfSimilar;
                    SessionConfig.SelfSimilarEnrollmentPending = true;
                    provider.Calls = 0; UnityWebRequest.Requests.Clear(); UnityWebRequest.RequestBodies.Clear();
                    synth.Speak(introduction, "intro", VoiceCondition.Generic);
                    Drain(MonoBehaviour.LastRoutine);
                    Check(string.IsNullOrEmpty(synth.LastError) && !synth.IsBusy && provider.Calls == 0 &&
                        UnityWebRequest.Requests.Exists(url => url.EndsWith(SessionConfig.NeutralId(introGender, introOption))),
                        "introduction always uses the selected neutral voice even with a pending self-similar condition");
                    Check(UnityWebRequest.RequestBodies.Exists(body => body.Contains(introduction)),
                        "introduction keeps participant-directed wording rather than first-person conversion");
                    Check(SessionConfig.Voice == VoiceCondition.SelfSimilar && SessionConfig.SelfSimilarEnrollmentPending,
                        "neutral introduction does not change the study voice condition or enrollment state");
                }
            SessionConfig.ResetForNewParticipant();
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
            Set(synth, "m_CloneClient", null);
            run();
            Check(UnityWebRequest.Requests.Count == 0, "missing provider stays silent");
            Set(synth, "m_CloneClient", provider); provider.HasKey = true;
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
            var voiceKeys = new System.Collections.Generic.HashSet<string>();
            foreach (var gender in new[] { NeutralVoiceProfile.Female, NeutralVoiceProfile.Male })
                for (int option = 0; option < SessionConfig.NeutralVoiceCount; option++)
                {
                    SessionConfig.SelectNeutralVoice(gender, option);
                    Check(voiceKeys.Add(Cache(synth, "el-" + SessionConfig.NeutralVoiceId)), "all twelve neutral voices have distinct cache keys");
                    Check(!string.IsNullOrEmpty(SessionConfig.NeutralVoiceName), "every voice choice is named");
                }
            SessionConfig.SelectNeutralVoice(NeutralVoiceProfile.Female, 0);
            Check(SessionConfig.NeutralVoiceCount == 6 && voiceKeys.Count == 12, "six distinct choices per gender");
            Check(SessionConfig.NeutralId(NeutralVoiceProfile.Male, 4) == "nPczCjzI2devNBz1zQrb" &&
                  SessionConfig.NeutralId(NeutralVoiceProfile.Female, 4) == "hpp4J3VqNfWAUOO0d1Us" &&
                  SessionConfig.NeutralName(NeutralVoiceProfile.Male, 5) == "Liam" &&
                  SessionConfig.NeutralName(NeutralVoiceProfile.Female, 5) == "Laura", "new choices append without remapping existing last indices");
            SessionConfig.ConfigureVoiceBlocks();
            Check(SessionConfig.NeutralFirst, "odd participant neutral first");
            Check(SessionConfig.VoiceForRound(39) == VoiceCondition.Generic && SessionConfig.VoiceForRound(40) == VoiceCondition.SelfSimilar, "block boundary after forty trials");
            File.WriteAllText(Path.Combine(SessionConfig.ParticipantPath, "voice-order-v1.txt"), "selfsimilar_then_neutral");
            SessionConfig.ConfigureVoiceBlocks();
            Check(!SessionConfig.NeutralFirst, "persisted assignment wins on restart");
            SessionConfig.SelectNeutralVoice(NeutralVoiceProfile.Male, 1);
            SessionConfig.ConfigureVoiceBlocks(replaceNeutralSelection: true);
            Check(SessionConfig.NeutralVoiceName == "Roger" && !SessionConfig.NeutralFirst,
                "explicit re-selection replaces saved gender/voice without changing block order");
            SessionConfig.SelectNeutralVoice(NeutralVoiceProfile.Female, 0);
            SessionConfig.ConfigureVoiceBlocks();
            Check(SessionConfig.NeutralVoiceName == "Roger", "restart restores the exact saved voice option");
            foreach (var newGender in new[] { NeutralVoiceProfile.Male, NeutralVoiceProfile.Female })
            {
                SessionConfig.SelectNeutralVoice(newGender, 5);
                string selectedId = SessionConfig.NeutralVoiceId;
                SessionConfig.ConfigureVoiceBlocks(replaceNeutralSelection: true);
                SessionConfig.SelectNeutralVoice(NeutralVoiceProfile.Female, 0);
                SessionConfig.ConfigureVoiceBlocks();
                Check(SessionConfig.NeutralVoiceOption == 5 && SessionConfig.NeutralVoiceId == selectedId,
                    "sixth voice persists and restores the exact provider identity");
                string catalog = File.ReadAllText(Path.Combine(SessionConfig.ParticipantPath, "neutral-catalog-v1.json"));
                Check(catalog.Contains(SessionConfig.NeutralCatalogVersion) && catalog.Contains(selectedId) &&
                    catalog.Contains("Liam") && catalog.Contains("Laura"),"catalog snapshot preserves additions and version");
            }
            File.Delete(Path.Combine(SessionConfig.ParticipantPath, "neutral-choice-v2.txt"));
            File.WriteAllText(Path.Combine(SessionConfig.ParticipantPath, "neutral-profile-v1.txt"), "Female");
            SessionConfig.ConfigureVoiceBlocks();
            Check(SessionConfig.NeutralVoiceOption == 0 && SessionConfig.NeutralProfile == NeutralVoiceProfile.Female,
                "legacy gender-only assignments migrate to the original voice");
            SessionConfig.ParticipantId = "P002"; SessionConfig.ConfigureVoiceBlocks();
            Check(!SessionConfig.NeutralFirst, "even participant self first");
            provider.HasKey = true; provider.Audio = new byte[120];
            bool prepared = false;
            DownloadHandlerAudioClip.AlternatePeak = true;
            Drain(synth.PrepareLibraries(new[] { "test phrase", "another phrase" }, null, ok => prepared = ok));
            Check(prepared && synth.LibraryReady, "both libraries prepared");
            var audits = (IEnumerable)typeof(VoiceSynthesizer)
                .GetField("m_Audit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth);
            foreach (var audit in audits)
            {
                var type = audit.GetType();
                bool clone = (string)type.GetField("voice_condition").GetValue(audit) == "selfsimilar";
                Check((string)type.GetField("effect_profile").GetValue(audit) == (clone ? InnerThoughtVoice.Version : "dry"),
                    "manifest identifies clone-only effect and dry neutral audio");
            }
            var loaded = (System.Collections.Generic.Dictionary<string, AudioClip>)typeof(VoiceSynthesizer)
                .GetField("m_PreparedClips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth);
            double? matchedRms = null;
            bool sawTreated = false;
            foreach (var entry in loaded)
            {
                sawTreated |= entry.Value.channels == 2;
                double rms = SpeechRms(synth, entry.Key, entry.Value);
                if (matchedRms.HasValue) Check(Math.Abs(rms - matchedRms.Value) < 0.0001, "different crest factors achieve equal RMS");
                matchedRms = rms;
            }
            Check(sawTreated, "clone clips are replaced by treated stereo clips");
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
            Check(prepared && provider.Texts.Contains("Locate the Blue Cube."),
                "self-similar synthesis receives the selected formatted target wording");
            Check(UnityWebRequest.RequestBodies.Exists(body => body.Contains("Locate the Blue Cube.")),
                "neutral synthesis retains external target wording");
            Check(provider.Texts.Contains("This is a practice round. It does not count toward the study. Locate the Blue Cube."),
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
                provider.Texts.Contains("Locate the Purple Sphere.") &&
                UnityWebRequest.Requests.TrueForAll(url => url.StartsWith("file:")),
                "first-use trial synthesizes in the assigned clone with shared wording and no neutral request");
            Check(File.Exists(SessionConfig.GetFilePath("voice-library-manifest.json")),
                "on-demand clip updates the active run manifest");
            matchedRms = null;
            foreach (var entry in loaded)
            {
                double rms = SpeechRms(synth, entry.Key, entry.Value);
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
            var hintPhrases = new[] { "Look left.", "Look right.", "You're in the correct area. Keep looking." };
            provider.Texts.Clear(); UnityWebRequest.RequestBodies.Clear();
            Drain(synth.PrepareLibraries(hintPhrases, null, ok => prepared = ok));
            Check(prepared, "all directional hints prepare in both voices");
            foreach (string phrase in hintPhrases)
            {
                string selfText = VoicePromptText.FormatForVoice(phrase, VoiceCondition.SelfSimilar);
                Check(selfText == phrase, "self-similar guidance shares the external command");
                Check(provider.Texts.Contains(selfText), "clone provider receives shared command");
                Check(UnityWebRequest.RequestBodies.Exists(body => body.Contains(phrase)), "neutral provider receives external hint");
                SessionConfig.Voice = VoiceCondition.SelfSimilar;
                provider.Calls = 0; UnityWebRequest.Requests.Clear();
                synth.Speak(phrase); Drain(MonoBehaviour.LastRoutine);
                Check(provider.Calls == 0 && UnityWebRequest.Requests.Count == 0, "runtime hint reuses exact prepared voice clip");
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
                foreach (string protectedContext in new[] { "round", "intro", "audio_check", "voice_choice" })
                {
                    Set(synth, "m_CurrentContext", protectedContext);
                    Check(!synth.TryAreaCorrection(lazyPrompt, () => true), "setup and target instructions cannot be interrupted");
                }
                Set(synth, "m_CurrentContext", "tip");
                Check(!synth.TryAreaCorrection("unprepared", () => true), "correction must be preloaded");
                Check(!synth.TryAreaCorrection(lazyPrompt, () => false), "stale gaze cannot start correction");
                var source = (AudioSource)typeof(VoiceSynthesizer).GetField("m_AudioSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth);
                source.Play();
                source.HoldPlayback = true;
                int stopsBeforeCorrection = source.StopCalls;
                bool relevant = true;
                int correctionCommits = 0;
                bool abortedDecisionLogged = false;
                Action<string,string,string,string> abortedTelemetry = (kind,context,key,detail) => {
                    if (kind=="guidance_decision_aborted" && detail.Contains("decision_id=abandoned-decision")) abortedDecisionLogged=true;
                };
                synth.Telemetry += abortedTelemetry;
                Check(synth.TryAreaCorrection(lazyPrompt, () => relevant, () => correctionCommits++, guidanceDecisionId:"abandoned-decision"), "playing hint accepts fade");
                var abandonedFade = MonoBehaviour.LastRoutine;
                abandonedFade.MoveNext();
                Time.unscaledTime += 0.02f;
                abandonedFade.MoveNext();
                Check(source.volume > 0 && source.volume < 0.7f, "ongoing hint fades gradually");
                relevant = false; Drain(abandonedFade);
                Check(Math.Abs(source.volume - 0.7f) < 0.0001f && starts == 2,
                    "leaving target zone during fade restores volume and cancels correction");
                Check(correctionCommits == 0, "abandoned fade must not commit the cue or consume cooldown");
                Check(abortedDecisionLogged,"Abandoned area request records its own decision ID without playback");
                synth.Telemetry -= abortedTelemetry;
                bool returnReasonLogged = false;
                bool correctionStartLinked = false;
                Action<string, string, string, string> correctionTelemetry = (kind, context, key, detail) =>
                {
                    if (kind == "audio_area_correction" && detail.Contains("gaze_overshoot_return") && detail.Contains("decision_id=return-decision")) returnReasonLogged = true;
                    if (kind == "audio_playback_start" && detail.Contains("decision_id=return-decision")) correctionStartLinked = true;
                };
                synth.Telemetry += correctionTelemetry;
                Check(synth.TryAreaCorrection(lazyPrompt, () => true, () => correctionCommits++, "gaze_overshoot_return","return-decision"),
                    "prepared correction can replace a hint");
                var fade = MonoBehaviour.LastRoutine;
                Check(synth.IsBusy, "pending fade blocks ordinary hints");
                Drain(fade);
                Drain(MonoBehaviour.LastRoutine);
                Check(!source.HoldPlayback && source.StopCalls > stopsBeforeCorrection, "correction cuts off unfinished speech");
                Check(starts == 3 && failures == 0, "correction plays once without failure");
                Check(correctionCommits == 1 && returnReasonLogged, "committed correction consumes cooldown once and logs return reason");
                Check(correctionStartLinked,"Audible correction retains the request decision through fade and Speak reset");
                synth.Telemetry -= correctionTelemetry;
                Check(provider.Calls == 0 && UnityWebRequest.Requests.Count == 0, "correction needs no network");
                synth.PlaybackFailed -= failure; synth.Telemetry -= telemetry;
            }
            var decisionEvents = new System.Collections.Generic.List<string>();
            Action<string,string,string,string> captureDecisionEvents = (kind,context,key,detail) => decisionEvents.Add(kind+"|"+detail);
            synth.Telemetry += captureDecisionEvents;
            synth.Speak(lazyPrompt,"tip",guidanceDecisionId:"original-decision"); Drain(MonoBehaviour.LastRoutine);
            foreach (var kind in new[] {"audio_request","audio_playback_start","audio_playback_end"})
                Check(decisionEvents.Exists(e=>e.StartsWith(kind+"|") && e.Contains("decision_id=original-decision")),
                    "Audio lifecycle preserves the original decision ID: "+kind);
            decisionEvents.Clear();
            synth.Speak(lazyPrompt,"round"); Drain(MonoBehaviour.LastRoutine);
            Check(!decisionEvents.Exists(e=>e.Contains("decision_id=")),"Non-guidance speech cannot inherit a prior decision");
            synth.Telemetry -= captureDecisionEvents;
            DownloadHandlerAudioClip.ForceSkewed = false;
            DownloadHandlerAudioClip.Quiet = false;
            Check(ChallengeSet.TotalRounds == 80 && ChallengeSet.RoundsPerBlock == 40 && ChallengeSet.BlockCount == 2, "full two-block schedule");
            var angleCounts = new int[2, 5];
            foreach (var trial in ChallengeSet.Rounds)
            {
                int targetIndex = Array.FindIndex(trial.objects, o => o.shape == trial.target.shape && o.color == trial.target.color);
                int plane = (targetIndex / 21 - trial.startPlane + 8) % 8;
                angleCounts[trial.blockIndex, Math.Min(plane, 8 - plane)]++;
            }
            for (int angle = 0; angle < 5; angle++)
            {
                Check(angleCounts[0, angle] > 0, "every absolute angle is covered");
                Check(angleCounts[0, angle] == angleCounts[1, angle], "absolute-angle counts match between blocks");
            }
            // Selection previews bypass library readiness, cancel prior prepared
            // content, and always request the chosen neutral provider (never a clone).
            foreach (var gender in new[] { NeutralVoiceProfile.Male, NeutralVoiceProfile.Female })
                for (int option = 0; option < SessionConfig.NeutralVoiceCount; option++)
                {
                    SessionConfig.SelectNeutralVoice(NeutralVoiceProfile.Female, 0);
                    SessionConfig.Voice = VoiceCondition.SelfSimilar;
                    provider.Calls = 0; UnityWebRequest.Requests.Clear();
                    int completed = 0; bool success = false; bool playbackEnded = false;
                    Action<string, string, string, string> previewTelemetry = (kind, context, key, detail) =>
                    { if (kind == "audio_playback_end" && context == "voice_choice") playbackEnded = true; };
                    synth.Telemetry += previewTelemetry;
                    synth.PreviewNeutralVoice(gender, option, ok =>
                    {
                        completed++; success = ok;
                        Check(playbackEnded, "successful audition callback occurs only after playback end");
                    });
                    Check(completed == 0, "an unplayed preview is not accepted");
                    Check(synth.IsBusy && !synth.LibraryReady, "preview waits for audio and invalidates old library");
                    Drain(MonoBehaviour.LastRoutine);
                    Check(string.IsNullOrEmpty(synth.LastError) && provider.Calls == 0 &&
                        UnityWebRequest.Requests.Exists(url => url.EndsWith(SessionConfig.NeutralId(gender, option))),
                        "preview uses the auditioned neutral identity even when a different voice is committed");
                    Check(completed == 1 && success && !synth.IsBusy, "completed audition reports success once and clears busy");
                    Check(SessionConfig.NeutralProfile == NeutralVoiceProfile.Female && SessionConfig.NeutralVoiceOption == 0 &&
                        SessionConfig.Voice == VoiceCondition.SelfSimilar, "audition never changes committed voice or condition");
                    synth.Telemetry -= previewTelemetry;
                }
            DownloadHandlerAudioClip.Corrupt = true;
            int failedPreviewCallbacks = 0;
            synth.PreviewNeutralVoice(NeutralVoiceProfile.Male, 1, ok =>
            { Check(!ok, "corrupt audition cannot report success"); failedPreviewCallbacks++; });
            Drain(MonoBehaviour.LastRoutine);
            Check(failedPreviewCallbacks == 1 && !synth.IsBusy && !string.IsNullOrEmpty(synth.LastError),
                "failed audition reports failure and releases confirmation wait");
            DownloadHandlerAudioClip.Corrupt = false;
            SessionConfig.SelectNeutralVoice(NeutralVoiceProfile.Male, 1);
            SessionConfig.ResetForNewParticipant();
            string correction = VoicePromptText.WrongSelectionPhrase("Blue", "Cube");
            Check(VoicePromptText.FormatForVoice(correction, VoiceCondition.Generic) == "Keep following the guidance and try another object.",
                "neutral wrong-selection reminder does not reveal identity");
            Check(VoicePromptText.FormatForVoice(correction, VoiceCondition.SelfSimilar) == "Keep following the guidance and try another object.",
                "self-similar wrong-selection reminder does not reveal identity");
            Check(VoicePromptText.FormatForVoice("Locate the Blue Cube.", VoiceCondition.SelfSimilar) == "Locate the Blue Cube.",
                "wrong-selection wording does not replace normal target announcements");
            foreach (var condition in new[] { VoiceCondition.Generic, VoiceCondition.SelfSimilar })
            {
                SessionConfig.Voice = condition;
                SessionConfig.SelfSimilarVoiceId = "correction-test-clone";
                SessionConfig.SelfSimilarEnrollmentPending = false;
                provider.Audio = new byte[120]; provider.Calls = 0; provider.Texts.Clear();
                UnityWebRequest.Requests.Clear(); UnityWebRequest.RequestBodies.Clear();
                synth.Speak(correction, "wrong_selection");
                Drain(MonoBehaviour.LastRoutine);
                Check(string.IsNullOrEmpty(synth.LastError) && !synth.IsBusy, "wrong-selection reminder plays successfully");
                Check(condition == VoiceCondition.Generic
                    ? provider.Calls == 0 && UnityWebRequest.RequestBodies.Exists(body => body.Contains("Keep following the guidance and try another object."))
                    : provider.Calls == 1 && provider.Texts.Contains("Keep following the guidance and try another object."),
                    "wrong-selection reminder reaches the condition's provider with exact wording");
                Check(SessionConfig.Voice == condition, "wrong-selection reminder preserves trial voice assignment");
            }
            SessionConfig.ResetForNewParticipant();
            Check(SessionConfig.NeutralVoiceOption == 0 && SessionConfig.NeutralProfile == NeutralVoiceProfile.Female,
                "new participant clears prior voice choice");
            const string audioCheck = "Hover your controller over Accept Audio and press the trigger to confirm this is an acceptable audio level.";
            Check(VoicePromptText.AudioLevelCheck == audioCheck, "audio check tells participant how to accept volume");
            foreach (var condition in new[] { VoiceCondition.Generic, VoiceCondition.SelfSimilar })
                Check(VoicePromptText.FormatForVoice(audioCheck, condition) == audioCheck, "both voices speak identical audio-check instructions");
            foreach (string phrase in DirectionalHintPolicy.AllPhrases())
            {
                string firstPerson = VoicePromptText.FormatForVoice(phrase, VoiceCondition.SelfSimilar);
                Check(firstPerson == phrase, "every assistance variant has shared imperative wording: " + phrase);
                Check(!firstPerson.Contains("I'll try looking right"), "rejected first-person phrase never returns");
            }
            Check(VoicePromptText.FormatForVoice("Go back. That was the correct area.", VoiceCondition.SelfSimilar) ==
                "Go back. That was the correct area.", "return cue uses shared commands for self-similar voice");
            Check(VoicePromptText.FormatForVoice("Look way left.", VoiceCondition.SelfSimilar) == "Look way left." &&
                VoicePromptText.FormatForVoice("Look way right.", VoiceCondition.SelfSimilar) == "Look way right.",
                "strong directional cues use shared commands for the self-similar voice");
            Check(VoicePromptText.FormatForVoice("Look way right.", VoiceCondition.Generic) == "Look way right.",
                "neutral strong cue stays external");
            string[] neutralVariants = { "Look to your left.", "Look to your right.", "Try looking left.", "Try looking right.", "Search to your left.", "Search to your right." };
            string[] selfVariants = neutralVariants;
            for (int i = 0; i < neutralVariants.Length; i++)
            {
                Check(VoicePromptText.FormatForVoice(neutralVariants[i], VoiceCondition.Generic) == neutralVariants[i], "neutral alternative remains external");
                Check(VoicePromptText.FormatForVoice(neutralVariants[i], VoiceCondition.SelfSimilar) == selfVariants[i], "self-similar alternative uses the same wording");
            }
            for (int participant = 1; participant <= 50; participant++)
            {
                SessionConfig.ParticipantId = $"P{participant:D3}";
                var schedule = ChallengeSet.Rounds;
                var firstCounts = new int[5]; var secondCounts = new int[5];
                var identities = new[] { new System.Collections.Generic.Dictionary<string, int>(), new System.Collections.Generic.Dictionary<string, int>() };
                var seeds = new System.Collections.Generic.HashSet<int>();
                foreach (var trial in schedule)
                {
                    int targetIndex = Array.FindIndex(trial.objects, o => o.shape == trial.target.shape && o.color == trial.target.color);
                    Check(targetIndex == trial.targetPlane * 21 + trial.targetSlot, "actual target obeys schedule");
                    Check(trial.startPlane == (trial.roundIndex == 0 ? ChallengeSet.PracticeRound(1).targetPlane : schedule[trial.roundIndex - 1].targetPlane),
                        "start wall follows actual previous target, including practice and block boundary");
                    Check(seeds.Add(trial.layoutSeed), "each trial has its own layout seed");
                    (trial.blockIndex == 0 ? firstCounts : secondCounts)[trial.AbsoluteTheta / 45]++;
                    string identity = trial.target.shape + ":" + trial.target.color;
                    var counts = identities[trial.blockIndex];
                    counts[identity] = counts.ContainsKey(identity) ? counts[identity] + 1 : 1;
                    if (trial.roundIndex > 0)
                        Check(identity != schedule[trial.roundIndex - 1].target.shape + ":" + schedule[trial.roundIndex - 1].target.color,
                            "no adjacent target identities, including block boundary");
                    if (trial.blockIndex == 0)
                    {
                        var mirror = Array.Find(schedule, t => t.blockIndex == 1 && t.anglePair == trial.anglePair);
                        Check((mirror.SignedTheta == -trial.SignedTheta || trial.AbsoluteTheta == 180) && mirror.AbsoluteTheta == trial.AbsoluteTheta,
                            "paired angles mirror across the two blocks");
                    }
                }
                Check(identities[0].Count == 24 && identities[1].Count == 24, "every identity appears in both voices");
                int doubled = 0;
                foreach (var identity in identities[0])
                {
                    Check(identity.Value == identities[1][identity.Key] && identity.Value >= 1 && identity.Value <= 2,
                        "both voices receive identical identity counts with at most two occurrences per block");
                    if (identity.Value == 2) doubled++;
                }
                Check(doubled == 16, "sixteen repeated and eight single identities per block");
                for (int half = 0; half < ChallengeSet.TotalRounds / 5; half++)
                {
                    var halfAngles = new System.Collections.Generic.HashSet<int>();
                    for (int i = 0; i < 5; i++) halfAngles.Add(schedule[half * 5 + i].AbsoluteTheta);
                    Check(halfAngles.Count == 5, "every five trials cover all absolute angles without early clustering");
                }
                int[] quota = { 8, 8, 8, 8, 8 };
                for (int a = 0; a < 5; a++) Check(firstCounts[a] == quota[a] && secondCounts[a] == quota[a], "fixed per-block quotas");
                var saved = new StringWriter(); ChallengeSet.WriteSchedule(saved);
                var rows = saved.ToString().Trim().Split('\n');
                Check(rows.Length == 81, "manifest documents all eighty planned trials");
                for (int r = 0; r < schedule.Length; r++)
                {
                    var cells = rows[r + 1].Trim().Split(',');
                    Check(cells.Length == 22 && int.Parse(cells[10]) == schedule[r].SignedTheta &&
                        int.Parse(cells[11]) == schedule[r].AbsoluteTheta && int.Parse(cells[13]) == schedule[r].layoutSeed && int.Parse(cells[17]) == schedule[r].startPlane,
                        "exported angle and seed match executable plan");
                    Check(cells[20] == ChallengeSet.TargetIdentityPolicy && cells[21] == ChallengeSet.HeightAssignmentPolicy,
                        "manifest distinguishes implemented identity balancing from pending signed-height quotas");
                }
                SessionConfig.ParticipantId = "P999"; var other = ChallengeSet.Rounds;
                SessionConfig.ParticipantId = $"P{participant:D3}";
                var restored = new StringWriter(); ChallengeSet.WriteSchedule(restored);
                Check(restored.ToString() == saved.ToString(), "participant restart reproduces full plan");
                Check(other[0].layoutSeed != schedule[0].layoutSeed, "participant identity changes randomization");
            }
            foreach (bool neutralFirst in new[] { true, false })
            {
                Directory.CreateDirectory(SessionConfig.ParticipantPath);
                File.WriteAllText(Path.Combine(SessionConfig.ParticipantPath, "voice-order-v1.txt"),
                    neutralFirst ? "neutral_then_selfsimilar" : "selfsimilar_then_neutral");
                SessionConfig.ConfigureVoiceBlocks();
                int neutralTrials = 0, selfTrials = 0;
                foreach (var trial in ChallengeSet.Rounds)
                {
                    if (SessionConfig.VoiceForRound(trial.roundIndex) == VoiceCondition.Generic) neutralTrials++;
                    else selfTrials++;
                }
                Check(neutralTrials == 40 && selfTrials == 40, "both saved orders assign forty trials to each voice");
            }
            ChallengeSet.DebugRoundCountOverride = 7;
            var truncated = new StringWriter(); ChallengeSet.WriteSchedule(truncated);
            int enabled = 0;
            foreach (var row in truncated.ToString().Trim().Split('\n'))
                if (row.StartsWith(SessionConfig.ParticipantId + ",") && row.Split(',')[16] == "1") enabled++;
            Check(enabled == 7 && ChallengeSet.Rounds.Length == 80, "short run preserves complete plan and flags only seven enabled trials");
            ChallengeSet.DebugRoundCountOverride = 999;
            Check(ChallengeSet.RoundCount == 80, "debug cannot exceed full study");
            ChallengeSet.DebugRoundCountOverride = 0;
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
            SessionConfig.ResetForNewParticipant();
            var beforeRun = new StringWriter(); ChallengeSet.WriteSchedule(beforeRun);
            string scheduledParticipant = SessionConfig.ParticipantId;
            SessionConfig.BeginRun();
            var afterRun = new StringWriter(); ChallengeSet.WriteSchedule(afterRun);
            Check(!string.IsNullOrEmpty(scheduledParticipant) && beforeRun.ToString() == afterRun.ToString(),
                "auto-assigned participant cannot change the schedule at run start");
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
            SessionConfig.SelfSimilarVoiceId = "old-participant-clone";
            SessionConfig.SelfSimilarEnrollmentPending = true;
            SessionConfig.ConfigureNeutralQuickTest();
            Check(SessionConfig.NeutralQuickTest && !SessionConfig.VoiceBlocksEnabled &&
                SessionConfig.Voice == VoiceCondition.Generic && SessionConfig.NeutralVoiceName == "Eric" &&
                SessionConfig.SelfSimilarVoiceId == "" && !SessionConfig.SelfSimilarEnrollmentPending,
                "quick test clears participant/clone state and uses neutral only");
            Check(SessionConfig.ParticipantId == "DEV_NEUTRAL" && ChallengeSet.RoundCount == 40,
                "quick test has a separate development identity and one 40-trial block");
            var quickPlan = ChallengeSet.Rounds;
            Check(quickPlan[0].startPlane == 0, "quick test starts at the centered wall without practice");
            string quickRun = SessionConfig.BeginRun();
            Check(quickRun.Contains("dev_quick_test") && File.ReadAllText(Path.Combine(quickRun, "session-mode-v1.txt")).Contains("study_data=false"),
                "quick test data cannot be mistaken for a coded participant run");
            var quickSchedule = new StringWriter(); ChallengeSet.WriteSchedule(quickSchedule);
            var scheduleLines = quickSchedule.ToString().Trim().Split('\n');
            for (int i = 1; i < scheduleLines.Length; i++)
                Check(scheduleLines[i].Split(',')[16] == (i <= 40 ? "1" : "0"), "schedule flags only the executed quick-test block");
            provider.Calls = 0; UnityWebRequest.Requests.Clear();
            DownloadHandlerAudioClip.Corrupt = false; DownloadHandlerAudioClip.Quiet = false;
            Drain(synth.PrepareLibraries(new[] { "Quick test neutral guidance." }, null, ok => prepared = ok, neutralOnly: true));
            Check(prepared && synth.LibraryReady && provider.Calls == 0 && SessionConfig.Voice == VoiceCondition.Generic,
                "neutral-only library becomes ready without a clone request");
            foreach (var audit in (IEnumerable)typeof(VoiceSynthesizer).GetField("m_Audit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(synth))
                Check((string)audit.GetType().GetField("voice_condition").GetValue(audit) != "selfsimilar", "quick-test manifest has no clone clips");
            Check(SessionConfig.VoiceForRound(40) == VoiceCondition.Generic, "quick test never changes to the cloned voice");
            SessionConfig.ResetForNewParticipant();
            Check(!SessionConfig.NeutralQuickTest && ChallengeSet.RoundCount == 80 && SessionConfig.ParticipantId == "",
                "new participant restores the full study schedule and disables shortcut");
            SessionConfig.ParticipantId = "DEV_NEUTRAL"; // Same seed: mode must still invalidate cached geometry.
            var studyPlan = ChallengeSet.Rounds;
            Check(studyPlan[0].startPlane == ChallengeSet.PracticeRound(1).targetPlane,
                "study still starts from the last practice target");
            for (int i = 0; i < quickPlan.Length; i++)
            {
                Check(quickPlan[i].SignedTheta == studyPlan[i].SignedTheta,
                    "quick-test origin preserves every planned signed theta");
                if (i > 0) Check(quickPlan[i].startPlane == quickPlan[i - 1].targetPlane,
                    "quick-test transitions continue from each actual prior target");
            }
            SessionConfig.ConfigureNeutralQuickTest();
            Check(ChallengeSet.Rounds[0].startPlane == 0, "re-entering quick test resets cached starting geometry");
            SessionConfig.ResetForNewParticipant();
            Console.WriteLine("PASS: voice isolation, cache, audio failures, neutral quick-test preparation, development identity and schedule/reset.");
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
        public float length = 1f; public int samples => m_Data.Length / channels; public int channels = 1; public int frequency = 48000;
        public string name = "";
        float[] m_Data = new float[1000];
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) =>
            new AudioClip { name = name, channels = channels, frequency = frequency,
                m_Data = new float[lengthSamples * channels], length = lengthSamples / (float)frequency };
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
public class ElevenLabsVoiceClient
{
    public const string Model = "eleven_turbo_v2_5";
    public bool HasKey = true; public byte[] Audio; public int Calls; public string Error = "simulated failure";
    public System.Collections.Generic.List<string> Texts = new System.Collections.Generic.List<string>();
    public void Initialize(string key) { }
    public IEnumerator Synthesize(string text, string voice, Action<byte[]> ok, Action<string> error)
    {
        Calls++; Texts.Add(text); yield return null;
        if (Audio == null) error(Error); else ok(Audio);
    }
}
