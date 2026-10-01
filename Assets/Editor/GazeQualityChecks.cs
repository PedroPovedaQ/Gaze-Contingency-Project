using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GazeQualityChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("Tools/Codex/Check Gaze Measurement")]
    public static void Run()
    {
        var go=new GameObject("CoordinateFixture");
        try
        {
            go.transform.position=new Vector3(1,2,3);go.transform.rotation=Quaternion.Euler(0,90,0);
            var local=new Vector3(.03f,0,0);
            var raw=GazeCoordinates.InputPoseToWorld(local,Quaternion.identity,go.transform,false);
            var flipped=GazeCoordinates.InputPoseToWorld(local,Quaternion.identity,go.transform,true);
            Check(Vector3.Distance(raw.direction,Vector3.right)<.0001f,"Head/tracking-space rotation applies once");
            Check(Vector3.Distance(raw.origin,go.transform.TransformPoint(local))<.0001f,"Origin transforms into world space");
            Check(Mathf.Abs(Vector3.Distance(raw.origin,flipped.origin)-.06f)<.0001f,"Diagnostic X mirror changes origin only");
            Check(local.x==.03f,"Coordinate conversion does not mutate raw input");
            var again=GazeCoordinates.InputPoseToWorld(local,Quaternion.identity,go.transform,true);
            Check(again.origin==flipped.origin,"No cumulative flip between reads");
            Check(VoiceAssistantController.RoundPhrase(1,"Red","Cube")==VoiceAssistantController.RoundPhrase(1,"Blue","Sphere"),"Round speech cannot reveal target identity");
            foreach(var phrase in DirectionalHintPolicy.AllPhrases())
                Check(VoicePromptText.FormatForVoice(phrase,VoiceCondition.Generic)==VoicePromptText.FormatForVoice(phrase,VoiceCondition.SelfSimilar),"Shared voice wording");
            CheckCsvIntegration();
            CheckTrialLinkage();
            CheckDecisionSnapshots();
            CheckValidationPanelClearance();
            Debug.Log("[GazeQualityChecks] PASS: coordinates, idempotent diagnostic flip, hidden target, shared wording, shared-stream CSV validity and append safety. Hardware accuracy/rate NOT validated.");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    static void CheckValidationPanelClearance()
    {
        var type=typeof(GazeDiagnosticView);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var offset=(Vector3)type.GetMethod("PanelOffset",flags).Invoke(null,new object[]{true});
        var size=(Vector2)type.GetField("k_PanelSize",flags).GetValue(null);
        float scale=(float)type.GetField("k_CanvasScale",flags).GetRawConstantValue();
        Vector2 half=size*scale*.5f;
        // Project known local validation directions onto the actual canvas plane.
        // Cover fit/drift +/-15 degrees and held-out +/-9/+3/+15 elevations.
        foreach(float yaw in new[]{-15f,0,15f,22.5f,29.5f})
        foreach(float pitch in new[]{-15f,-12f,-9f,0,3f,12f,15f})
        {
            Vector3 ray=Quaternion.Euler(-pitch,yaw,0)*Vector3.forward;
            Vector3 hit=ray*(offset.z/ray.z)-offset;
            Check(Mathf.Abs(hit.x)>half.x+.02f || Mathf.Abs(hit.y)>half.y+.02f,
                $"Diagnostic panel must not hide the independent target at yaw {yaw}, pitch {pitch}");
        }
    }
    static void CheckDecisionSnapshots()
    {
        var sample = new GazeObservation { sequence=17, recordingId="eye-recording", participant="p1",
            run=2, round=39, block=0, sourceTrialId="p1_run002_r39", inputEventTime=10,
            receiptTime=10.01, valid=true, deviceAvailable=true, source="fixture", phase="search" };
        var request=GuidanceDecision.Capture("request-a","requested","stable_direction","Look up.",
            "A little higher.",sample,10.02,true,true,false,true);
        sample.sequence=18;sample.valid=false;sample.invalidReason="stale";
        var rejected=GuidanceDecision.Capture("request-b","suspended","stale",null,"",sample,
            10.2,false,false,false,false);
        Check(request.observationSequence==17 && request.gazeUsable && request.eyeRecordingId=="eye-recording" &&
            request.sourceTrialId=="p1_run002_r39", "Decision freezes the original gaze and trial identity");
        Check(Math.Abs(request.gazeAgeAtDecision-.02)<.0001 && request.uncertaintyStatus=="not_estimated_pilot_thresholds",
            "Decision records delivery age without inventing measured uncertainty");
        Check(!rejected.gazeUsable && rejected.invalidReason=="stale" && rejected.observationSequence==18,
            "Suspension preserves the invalid observation and reason");
        var saved=JsonUtility.FromJson<GuidanceDecision>(JsonUtility.ToJson(request));
        Check(saved.decisionId=="request-a" && saved.phrase=="A little higher." && saved.observationSequence==17,
            "Persisted decision retains speech and source linkage");
    }
    static void CheckTrialLinkage()
    {
        var host = new GameObject("GazeTrialLinkageFixture"); host.SetActive(false);
        var originalVoice = SessionConfig.Voice;
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            var current = typeof(FindObjectGameManager).GetField("m_CurrentRound", BindingFlags.Instance | BindingFlags.NonPublic);
            var present = typeof(FindObjectGameManager).GetMethod("SetPresentedTrialContext", BindingFlags.Instance | BindingFlags.NonPublic);
            var sample = new GazeObservation { participant="TEST", run=3 };
            SharedGazeStream.FillTrialContext(sample, game);
            Check(sample.round == -1 && sample.block == -1 && sample.sourceTrialId == "", "Setup cannot invent a trial");
            foreach (int round in new[] { 0, 39, 79 })
            {
                current.SetValue(game,round); present.Invoke(game,null);
                string voice = game.PresentedVoiceTag;
                current.SetValue(game,round+1); // Real capture advances before success finishes.
                SessionConfig.Voice = SessionConfig.Voice == VoiceCondition.Generic ? VoiceCondition.SelfSimilar : VoiceCondition.Generic;
                SharedGazeStream.FillTrialContext(sample,game);
                Check(sample.round==round && sample.sourceTrialId==$"TEST_run003_r{round:D2}" &&
                    sample.block==round/40 && sample.voice==voice,"Feedback preserves completed eye trial and voice, including block/final boundary");
            }
            typeof(FindObjectGameManager).GetProperty("IsPractice").SetValue(game,true);
            current.SetValue(game,0);present.Invoke(game,null);current.SetValue(game,40);
            SharedGazeStream.FillTrialContext(sample,game);
            Check(sample.practice && sample.round==0 && sample.block==-1 && sample.sourceTrialId=="",
                "Practice feedback stays practice and cannot join a measured trial");
            present.Invoke(game,null);SharedGazeStream.FillTrialContext(sample,game);
            Check(sample.round==40 && sample.practice,"Only the next presentation advances the recording context");
        }
        finally { SessionConfig.Voice=originalVoice; UnityEngine.Object.DestroyImmediate(host); }
    }
    static void CheckCsvIntegration()
    {
        if (SharedGazeStream.Instance != null) throw new InvalidOperationException("Stop Play mode before gaze checks.");
        string directory = Path.Combine(Application.temporaryCachePath, "GazeCsvChecks_" + Guid.NewGuid().ToString("N"));
        string originalFolder = SessionConfig.CurrentRunFolder, originalParticipant = SessionConfig.ParticipantId;
        var folderProperty = typeof(SessionConfig).GetProperty("CurrentRunFolder");
        var latestProperty = typeof(SharedGazeStream).GetProperty("Latest");
        var host = new GameObject("GazeCsvFixture"); host.SetActive(false);
        var source = new GameObject("SharedGazeFixture");
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            folderProperty.SetValue(null, directory); SessionConfig.ParticipantId = "CSV_FIXTURE";
            var stream = source.AddComponent<SharedGazeStream>();
            // Edit-mode components do not run normal player lifecycle methods.
            typeof(SharedGazeStream).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stream,null);
            var logger = host.AddComponent<GazeDataLogger>();
            void Call(string method) => typeof(GazeDataLogger).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logger, null);
            Call("OpenLog");
            target.layer = 8; target.transform.position = new Vector3(1000, 1000, 1002);
            var info = target.AddComponent<SpawnableObjectInfo>();
            info.objectId = "fixture-object"; info.shapeName = "Cube"; info.colorName = "Blue";
            Physics.SyncTransforms();
            var sample = new GazeObservation { sequence = 1, deviceAvailable = true, valid = true,
                inputEventTime = Time.realtimeSinceStartupAsDouble, receiptTime = Time.realtimeSinceStartupAsDouble,
                worldOrigin = new Vector3(1000,1000,1000), worldDirection = Vector3.forward,
                participant = "SNAPSHOT", run = 3, round = 39, voice = "selfsimilar", practice = true,
                sourceTrialId = "fixture_trial", maxDeliveryAgeSeconds = .1f };
            latestProperty.SetValue(stream, sample);
            Call("Update"); Call("OnDisable");
            Call("OpenLog"); // This used to silently truncate a run's CSV.
            sample.sequence = 2; sample.valid = false; sample.invalidReason = "untracked";
            latestProperty.SetValue(stream, sample);
            Call("Update"); Call("OnDisable");
            var lines = File.ReadAllLines(Path.Combine(directory,"gaze_log.csv"));
            Check(lines.Length == 3, "Re-enable must preserve the first row and a single header.");
            string[] header = lines[0].Split(','), valid = lines[1].Split(','), invalid = lines[2].Split(',');
            Check(header.Length == valid.Length && header.Length == invalid.Length, "Every row matches the CSV schema.");
            string Cell(string[] row, string field) => row[Array.IndexOf(header, field)];
            Check(Cell(valid,"gaze_valid") == "1" && Cell(valid,"hovered_object_id") == "fixture-object", "CSV uses the shared valid ray intersection.");
            Check(Cell(valid,"participant_id") == "SNAPSHOT" && Cell(valid,"run_number") == "3" &&
                Cell(valid,"voice_condition") == "selfsimilar" && Cell(valid,"is_practice") == "1",
                "Compatibility CSV preserves observation identity rather than mutable global state");
            Check(Cell(valid,"objective_index") == "39", "CSV objective index uses the presented trial context");
            Check(Cell(invalid,"gaze_valid") == "0" && Cell(invalid,"gaze_origin_x") == "" && Cell(invalid,"hovered_object_id") == "", "Tracking loss retains a gap without a stale pose or object.");
            Check(Cell(valid,"blink") == "" && Cell(valid,"blink_count") == "" && Cell(valid,"fixation_x") == "", "Unvalidated eye events remain unavailable.");
            Check(logger.BlinkCount == -1 && !logger.HasBlinkSignal, "Legacy callers cannot consume an unvalidated blink count.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(target);
            typeof(SharedGazeStream).GetProperty("Instance").SetValue(null,null);
            folderProperty.SetValue(null, originalFolder); SessionConfig.ParticipantId = originalParticipant;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }

}
