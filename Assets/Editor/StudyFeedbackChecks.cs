using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class StudyFeedbackChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    [MenuItem("Tools/Codex/Check Study Feedback")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before checks.");
        var origin = new Vector3(4, 1, 3);
        Check(TrialReadinessGate.TryReminderDirection(origin, Vector3.right, origin + Vector3.back * 2 + Vector3.up,
            out float yaw, out float pitch) && yaw > 80 && pitch > 20,
            "Cross direction uses gaze-relative yaw and pitch at a translated eye origin.");
        Check(!TrialReadinessGate.TryReminderDirection(origin, Vector3.up, origin + Vector3.forward, out _, out _),
            "Near-vertical gaze cannot invent a horizontal cross direction.");
        CheckVerticalWallDistance();
        CheckRoundPerformance();
        foreach (string phrase in DirectionalHintPolicy.AllPhrases())
            Check(Array.IndexOf(VoiceAssistantController.StarterPhrases(), phrase) >= 0 &&
                Array.IndexOf(VoiceAssistantController.PhraseLibrary(), phrase) >= 0,
                "Reminder audio exists in both preparation paths.");

        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        TargetCrumble effect = null;
        var oldRandom = UnityEngine.Random.state;
        try
        {
            target.transform.position = new Vector3(1, 2, 3);
            target.transform.localScale = Vector3.one * .1f;
            Physics.SyncTransforms();
            effect = TargetCrumble.Play(target, null);
            Check(effect != null && !target.GetComponent<Renderer>().enabled && target.activeSelf,
                "Only the original surface is hidden; trial metadata remains alive.");
            Check(effect.GetComponentsInChildren<Collider>().Length == 0 && effect.GetComponentsInChildren<Rigidbody>().Length == 0,
                "Fragments cannot become selection targets or physics bodies.");
            var mesh = effect.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.vertexCount == 1200 && mesh.triangles.Length == 1200,
                "Exactly 100 independent four-face fragments share one mesh.");
            var velocities = (Vector3[])typeof(TargetCrumble)
                .GetField("m_Velocities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                Check(Array.Exists(velocities, velocity => Vector3.Dot(velocity.normalized, axis) > .9f),
                    "Burst covers every spatial direction, including down and behind.");
            var confetti = effect.transform.Find("Success confetti").GetComponent<MeshFilter>().sharedMesh;
            var colors = effect.transform.Find("Success confetti").GetComponent<MeshRenderer>().sharedMaterials;
            Check(confetti.vertexCount == TargetCrumble.ConfettiCount * 8 && confetti.subMeshCount == 5,
                "120 double-sided confetti pieces use five colors.");
            var confettiBefore = confetti.vertices;
            var before = mesh.vertices;
            typeof(TargetCrumble).GetMethod("UpdateGeometry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, new object[] { .6f });
            var after = mesh.vertices;
            Check(Vector3.Distance(before[0], after[0]) > .01f, "Fragments move during the crumble.");
            Check(Vector3.Distance(confettiBefore[0], confetti.vertices[0]) > .1f, "Confetti bursts outward.");
            for (int i = 0; i < after.Length; i++)
                Check(!float.IsNaN(after[i].x) && !float.IsInfinity(after[i].y), "Crumble geometry stays finite.");
            float next = UnityEngine.Random.value;
            UnityEngine.Random.state = oldRandom;
            Check(next == UnityEngine.Random.value, "Animation does not perturb stimulus randomization.");
            UnityEngine.Object.DestroyImmediate(target); target = null;
            Check(effect != null && mesh != null, "Final-trial object cleanup does not erase the animation.");
            // Ordinary MonoBehaviours do not receive play-mode OnDestroy in this
            // edit-mode fixture. Exercise the owned-resource lifecycle explicitly.
            typeof(TargetCrumble).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, null);
            UnityEngine.Object.DestroyImmediate(effect.gameObject); effect = null;
            Check(mesh == null && confetti == null && Array.TrueForAll(colors, color => color == null),
                "Animation releases both meshes and its confetti materials.");
        }
        finally
        {
            UnityEngine.Random.state = oldRandom;
            if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }
        Debug.Log("[StudyFeedbackChecks] PASS: gaze-relative cross direction, prepared reminders, spherical 100-piece burst, 120 confetti pieces, no physics/RNG interference and cleanup.");
    }

    static void CheckRoundPerformance()
    {
        var host = new GameObject("RoundPerformanceFixture"); host.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(object owner,string field,object value) => owner.GetType().GetField(field,flags).SetValue(owner,value);
        object Call(object owner,string method,params object[] args) => owner.GetType().GetMethod(method,flags).Invoke(owner,args);
        try
        {
            var game=host.AddComponent<FindObjectGameManager>();
            var voice=host.AddComponent<VoiceAssistantController>();
            Set(voice,"m_GameManager",game);
            Set(game,"<PresentedRoundIndex>k__BackingField",3);
            Set(game,"<SearchActive>k__BackingField",true);
            Call(voice,"BeginRoundPerformance",3);
            Call(voice,"TrackRoundSpeech","audio_request","tip","","");
            Call(voice,"TrackRoundSpeech","audio_playback_start","tip","","");
            Call(voice,"TrackRoundSpeech","audio_playback_start","wrong_selection","","");
            Call(voice,"TrackRoundSpeech","guidance_repeat_requested","guidance_repeat","","");
            Call(voice,"HandleWrongCapture","red","blue");
            Set(game,"<SearchActive>k__BackingField",false);
            Call(voice,"TrackRoundSpeech","audio_playback_start","tip","","");
            Set(voice,"m_LastEncouragement","You got it. Round complete.");
            var facts=(SpatialVoice.RoundPerformance)Call(voice,"CaptureRoundPerformance");
            Check(facts.trialNumber==4 && facts.previousFeedback=="You got it. Round complete.",
                "Completion snapshot includes the presented trial number and last feedback");
            Check(facts.measurementsAvailable && facts.guidanceStarts==1 && facts.repeatRequests==1 && facts.incorrectSelections==1,
                "Count actual guidance onsets and repeat/wrong-selection events only during active search");
            Check(Math.Abs(facts.searchSeconds-game.CurrentSearchSeconds)<.001,"Use authoritative pause-aware search clock");
            // Current objective advances before completion subscribers; presented identity stays fixed.
            Set(game,"m_CurrentRound",4);
            Check(((SpatialVoice.RoundPerformance)Call(voice,"CaptureRoundPerformance")).measurementsAvailable,
                "Capture uses presented trial, not the already-incremented next objective");
            Set(game,"<PresentedRoundIndex>k__BackingField",4);
            Check(!((SpatialVoice.RoundPerformance)Call(voice,"CaptureRoundPerformance")).measurementsAvailable,
                "Old measurements cannot describe a new presented trial");
            Call(voice,"BeginRoundPerformance",4);
            Check(((SpatialVoice.RoundPerformance)Call(voice,"CaptureRoundPerformance")).incorrectSelections==0,"New search resets performance");
            Check(facts.incorrectSelections==1 && facts.guidanceStarts==1,"Completed snapshot remains frozen after next-round reset");
            // Practice and measured round zero are different identities despite sharing an index.
            Set(game,"<PresentedRoundIsPractice>k__BackingField",true);
            Check(!((SpatialVoice.RoundPerformance)Call(voice,"CaptureRoundPerformance")).measurementsAvailable,
                "Practice/measurement transition invalidates stale facts even with the same round number");
            Call(voice,"BeginRoundPerformance",4);
            Set(game,"<SearchActive>k__BackingField",true);
            Call(voice,"TrackRoundSpeech","audio_playback_start","guidance_repeat","","");
            Call(voice,"TrackRoundSpeech","audio_playback_end","tip","","");
            Call(voice,"TrackRoundSpeech","audio_playback_start","congrats","","");
            var practice=(SpatialVoice.RoundPerformance)Call(voice,"CaptureRoundPerformance");
            Check(practice.practice && practice.guidanceStarts==1 && practice.plannedAbsoluteTurnDegrees==null,
                "Actual repeat playback counts once; congratulations/end events do not; practice has no planned study angle");
            var synth=host.AddComponent<VoiceSynthesizer>();Set(voice,"m_VoiceSynthesizer",synth);
            string recorded=null;
            synth.Telemetry+=(kind,context,clip,detail)=> { if(kind=="round_encouragement_decision") recorded=detail; };
            Call(voice,"RecordEncouragement",practice,SpatialVoice.RoundEncouragement.Phrases[9],"jev_selected");
            var logged=Newtonsoft.Json.Linq.JObject.Parse(recorded);
            Check((string)logged["phrase_id"]=="practice" && (int)logged["round_performance"]["guidanceStarts"]==1,
                "Decision telemetry preserves the phrase and frozen performance for replay/analysis");
            Call(voice,"RecordEncouragement",practice,null,"local_fallback");
            Check((string)Newtonsoft.Json.Linq.JObject.Parse(recorded)["status"]=="no_prepared_phrase",
                "Missing audio is explicitly logged instead of claiming spoken reinforcement");
            foreach(int count in new[] {0,1,4})
            {
                var choices=new System.Collections.Generic.List<SpatialVoice.EncouragementPhrase>();
                for(int i=0;i<count;i++) choices.Add(SpatialVoice.RoundEncouragement.Phrases[i]);
                var request=(System.Threading.Tasks.Task<SpatialVoice.EncouragementPhrase>)Call(voice,"RequestRoundEncouragement",
                    practice,choices,System.Threading.CancellationToken.None);
                Check(request.IsCompleted && request.Result==null,"Missing client/key bypasses remote advice without delaying progress");
            }
            var pending=new System.Threading.CancellationTokenSource();var token=pending.Token;
            Set(voice,"m_SuccessCancellation",pending);Set(voice,"<SuccessFeedbackBusy>k__BackingField",true);
            Call(voice,"StopRoundEncouragement","test_stop");
            Check(token.IsCancellationRequested && !voice.SuccessFeedbackBusy,"Session stop cancels advice and releases transition wait");
            Check(!((SpatialVoice.RoundPerformance)Call(voice,"CaptureRoundPerformance")).measurementsAvailable,"Stopped session clears facts");
            Call(voice,"StopRoundEncouragement","repeated_stop");
            Set(voice,"m_LastEncouragement","Nice work.");
            Call(voice,"HandleGameStarted");
            Check(!(bool)voice.GetType().GetField("m_EncouragementSessionStopped",flags).GetValue(voice) &&
                voice.GetType().GetField("m_LastEncouragement",flags).GetValue(voice)==null,"Restart resets stop and wording history");
            var disabling=new System.Threading.CancellationTokenSource();var disablingToken=disabling.Token;
            Set(voice,"m_SuccessCancellation",disabling);Set(voice,"<SuccessFeedbackBusy>k__BackingField",true);
            Call(voice,"OnDisable");
            Check(disablingToken.IsCancellationRequested && !voice.SuccessFeedbackBusy,"Component disable cancels pending advice");
            foreach(var phrase in SpatialVoice.RoundEncouragement.AllTexts())
                Check(Array.IndexOf(VoiceAssistantController.StarterPhrases(),phrase)>=0 &&
                    Array.IndexOf(VoiceAssistantController.PhraseLibrary(),phrase)>=0,"Encouragement is prepared for quick and full voice setup");
            Debug.Log("[StudyFeedbackChecks] PASS: round encouragement facts, immutable snapshots, practice identity, telemetry, missing client, repeated stop, restart, disable and prepared phrases.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    static void CheckVerticalWallDistance()
    {
        var host = new GameObject("VerticalWallDistanceFixture"); host.SetActive(false);
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FindObjectGameManager).GetField("m_SpawnCenter", flags).SetValue(game, new Vector3(3, 1, 2));
            typeof(FindObjectGameManager).GetField("m_SeatedRotation", flags).SetValue(game, Quaternion.Euler(0, 90, 0));
            typeof(FindObjectGameManager).GetField("m_RotationalRadius", flags).SetValue(game, 1.5f);
            typeof(FindObjectGameManager).GetField("m_MaxObjectHeight", flags).SetValue(game, 4f);
            Check(Mathf.Abs(game.SearchPlaneHeight - 4.1f) < .0001f, "Guidance denominator matches the full drawn wall height.");
            Vector3 eye = new Vector3(3, 1, 2), gaze = new Vector3(1, .2f, 0).normalized;
            Check(game.TryVerticalGuidanceError(eye, gaze, 0, 2.3f, out float error) && Mathf.Abs(error - 1) < .0001f,
                "Rotated translated wall uses ray-plane intersection rather than eye-to-target height.");
            Check(DirectionalHintPolicy.VerticalBand(error / game.SearchPlaneHeight) == 1,
                "One metre on a 4.1m wall is a medium correction.");
            Check(game.TryVerticalGuidanceError(eye, gaze * 2, 0, 2.3f, out float scaled) && Mathf.Abs(error - scaled) < .0001f,
                "Ray normalization does not change vertical distance.");
            Check(!game.TryVerticalGuidanceError(eye, Vector3.left, 0, 2.3f, out _), "Backward ray has no target-wall height estimate.");
            Check(!game.TryVerticalGuidanceError(eye, Vector3.up, 0, 2.3f, out _), "Parallel ray has no height estimate.");
            Check(game.TryVerticalGuidanceError(eye, new Vector3(1, -3, 0).normalized, 0, 2.3f, out error) && error > game.SearchPlaneHeight,
                "Gaze below the wall is not clamped into a smaller correction.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
