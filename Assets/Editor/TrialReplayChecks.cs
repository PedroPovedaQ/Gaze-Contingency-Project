using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TrialReplayChecks
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    [MenuItem("Tools/Codex/Trial Replay/Run Verification")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running replay checks.");
        string root = Path.Combine(Application.temporaryCachePath, "ReplayChecks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = CreateDemo(Path.Combine(root, "demo"));
            var timeline = TrialReplayFile.Read(file);
            Assert(timeline.Warning == null && timeline.Trials.Count == 1, "Round trip complete recording.");
            Assert(timeline.Trials[0].objects.Length == 168, "All 168 objects recorded.");
            timeline.Seek(1.6); Assert(timeline.State.Selection.objectId == "o01" && !timeline.State.Selection.correct, "Wrong choice identity retained.");
            timeline.Seek(2.5); Assert(!timeline.State.Visible, "Pause hides stimuli.");
            timeline.Seek(5); var position = timeline.State.Transforms["o02"].position;
            timeline.Seek(1); Assert(timeline.State.Selection == null, "Backward seek clears future selection.");
            timeline.Seek(5); Assert(timeline.State.Transforms["o02"].position == position, "Seek retains moved transform.");
            timeline.Seek(6); Assert(timeline.State.Selection.correct && timeline.State.Selection.objectId == "o00", "Correct choice preserved.");
            string incomplete = Path.Combine(root, "partial.jsonl");
            var lines = File.ReadAllLines(file); File.WriteAllLines(incomplete, new ArraySegment<string>(lines, 0, lines.Length - 1));
            File.AppendAllText(incomplete, "{\"kind\":");
            Assert(TrialReplayFile.Read(incomplete).Warning != null, "Interrupted tail is marked incomplete.");
            string corrupt = Path.Combine(root, "corrupt.jsonl");
            var bad = (string[])lines.Clone(); bad[3] = "not-json"; File.WriteAllLines(corrupt, bad);
            Reject(() => TrialReplayFile.Read(corrupt), "Corrupt middle must fail.");
            Reject(() => TrialReplayFile.AssetPath(root, "../outside.wav"), "Asset traversal must fail.");
            Reject(() => new TrialReplayWriter(file), "Existing file must not be overwritten.");
            var future = JsonUtility.FromJson<TrialReplayRecord>(lines[0]); future.schema = 99;
            bad = (string[])lines.Clone(); bad[0] = JsonUtility.ToJson(future); File.WriteAllLines(corrupt, bad);
            Reject(() => TrialReplayFile.Read(corrupt), "Unsupported schema must fail.");
            var samples = TrialReplayAudio.Decode(Path.Combine(timeline.DirectoryPath, "audio/tone.wav"), out int channels, out int rate);
            Assert(samples.Length == 24000 && channels == 1 && rate == 24000, "Recorded audio roundtrip.");
            timeline.Seek(1);
            Assert(timeline.State.Sample.dwell == 0 && timeline.State.Sample.leftController.triggerHeld == 1 &&
                timeline.State.Sample.leftController.hoveredId == "o01", "Controller hit/trigger preserved independently of gaze.");
            timeline.Seek(4.1);
            Assert(timeline.State.Sample.leftController.tracked == 0 && timeline.State.Sample.leftController.triggerHeld == -1 &&
                !timeline.State.Sample.leftController.rayAvailable, "Lost controller tracking remains explicit.");
            // Invalid controller evidence must be rejected, including unknown object identities.
            var invalid = JsonUtility.FromJson<TrialReplayRecord>(lines[3]);
            invalid.leftController = new TrialReplayController { available = true, tracked = 1, rayAvailable = true, hoveredId = "missing" };
            bad = (string[])lines.Clone(); bad[3] = JsonUtility.ToJson(invalid); File.WriteAllLines(corrupt, bad);
            Reject(() => TrialReplayFile.Read(corrupt), "Unknown controller target must fail.");
            invalid.leftController.hoveredId = "o00"; invalid.leftController.triggerHeld = 2;
            bad[3] = JsonUtility.ToJson(invalid); File.WriteAllLines(corrupt, bad);
            Reject(() => TrialReplayFile.Read(corrupt), "Invalid trigger must fail.");
            invalid.leftController.triggerHeld = 0; invalid.dwell = 1;
            bad[3] = JsonUtility.ToJson(invalid); File.WriteAllLines(corrupt, bad);
            Reject(() => TrialReplayFile.Read(corrupt), "New recordings cannot imply dwell selection.");
            // Original schema remains readable, but has no controller evidence.
            var legacy = new List<string>();
            foreach (var line in lines)
            {
                var r = JsonUtility.FromJson<TrialReplayRecord>(line); r.schema = 1;
                r.leftController = r.rightController = null; r.dwell = r.kind == "sample" ? 0.5f : 0;
                legacy.Add(JsonUtility.ToJson(r));
            }
            File.WriteAllLines(corrupt, legacy);
            Assert(TrialReplayFile.Read(corrupt).Header.schema == 1, "Legacy schema is readable.");
            CheckLiveSelectionHook(root);
            CheckWriterBackpressure(root);
            CheckReadinessReplay();
            // Graphics verification is separate so these checks also run with -nographics.
            Debug.Log("[TrialReplayChecks] PASS: roundtrip, 168 objects, wrong/correct choice, pause, rewind, motion, recovery, corruption, traversal, version, overwrite, controller tracking/trigger/hit, no dwell, legacy schema, audio.");
        }
        finally { Directory.Delete(root, true); }
    }
    sealed class CaptureObserved : Exception { }
    static void CheckReadinessReplay()
    {
        var state = new TrialReplayState();
        var gate = new TrialReplayRecord { kind = "readiness_started", readinessActive = true, readinessWall = 3,
            readinessProgress = 0, readinessPosition = new Vector3(1, 1.5f, 1) };
        state.Apply(gate);
        CheckReadiness(state.Readiness == gate && !state.Searching, "Readiness events are distinct from search.");
        gate = new TrialReplayRecord { kind = "readiness_charge_started", readinessActive = true, readinessProgress = 0.2f, readinessWall = 3 };
        var copy = JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(gate));
        state.Apply(copy);
        CheckReadiness(state.Readiness.readinessProgress == 0.2f && state.Readiness.dwell == 0,
            "Readiness charge survives serialization without object dwell.");
        state.Apply(new TrialReplayRecord { kind = "readiness_completed", readinessProgress = 1, readinessActive = false });
        CheckReadiness(!state.Readiness.readinessActive && !state.Searching, "Completing gaze alone is not search onset.");
        state.Apply(new TrialReplayRecord { kind = "search_start" });
        CheckReadiness(state.Searching, "Only authoritative search event opens playback stimuli.");
        state.Reset(); CheckReadiness(state.Readiness == null, "Rewind clears future readiness state.");
    }
    static void CheckReadiness(bool condition, string message) => Assert(condition, message);
    static void CheckWriterBackpressure(string root)
    {
        using (var entered = new System.Threading.ManualResetEvent(false))
        using (var release = new System.Threading.ManualResetEvent(false))
        using (var writer = new TrialReplayWriter(Path.Combine(root, "blocked-writer.jsonl")))
        {
            // Fault-inject a stalled worker rather than requiring a failing physical disk.
            var enqueue = typeof(TrialReplayWriter).GetMethod("Enqueue", BindingFlags.Instance | BindingFlags.NonPublic);
            enqueue.Invoke(writer, new object[] { (Action)(() => { entered.Set(); release.WaitOne(); }) });
            try
            {
                Assert(entered.WaitOne(2000), "Writer fault fixture must start.");
                bool accepted = true;
                for (int i = 0; i < 4096 && accepted; i++) accepted = writer.Append(new TrialReplayRecord { kind = "sample", sequence = i });
                Assert(!accepted && writer.Error != null, "Bounded queue must report overflow.");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                writer.StopWithoutWaiting();
                Assert(watch.ElapsedMilliseconds < 1000, "Replay failure cannot wait for blocked storage.");
            }
            finally { release.Set(); }
        }
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static void CheckLiveSelectionHook(string root)
    {
        var host = new GameObject("ReplayIntegrationFixture"); host.SetActive(false);
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material material = null;
        TrialReplayRecorder recorder = null;
        string directory = null;
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            Set(game, "m_UseRotationalLayout", false); // Do not assign a participant ID during this fixture.
            var ui = host.AddComponent<FindObjectUI>();
            var selector = host.AddComponent<ControllerRaySelector>();
            recorder = host.AddComponent<TrialReplayRecorder>();
            // Inactive fixture suppresses runtime bootstraps; drive the same subscribed recorder callbacks.
            Call(recorder, "OnEnable");
            Set(game, "m_UI", ui); Set(game, "m_State", FindObjectGameManager.GameState.Playing);
            Set(game, "<SearchActive>k__BackingField", true);
            Set(game, "m_CurrentRound", 0); Set(game, "m_CurrentTarget", ("Cube", "Blue", Color.blue));
            var info = obj.AddComponent<SpawnableObjectInfo>(); info.objectId = "fixture_target"; info.shapeName = "Cube"; info.colorName = "Blue";
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = Color.blue;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            ((List<GameObject>)game.SpawnedObjects).Add(obj);
            // Use a temporary writer and the same record/snapshot path without opening participant directories.
            directory = Path.Combine(root, "live-hook"); Directory.CreateDirectory(directory);
            Set(recorder, "m_Directory", directory);
            Set(recorder, "m_OriginRotation", Quaternion.identity);
            Set(recorder, "m_Started", Time.realtimeSinceStartupAsDouble);
            var writer = new TrialReplayWriter(Path.Combine(directory, "recording.jsonl"));
            Set(recorder, "m_Writer", writer);
            writer.Append(new TrialReplayRecord { kind = "header", recordingId = "synthetic-hook", sourceType = "synthetic_demo", sequence = 0 });
            Set(recorder, "m_Sequence", 1L);
            Call(recorder, "ObjectsReady", 0, "Blue", "Cube");
            Set(selector, "<TelemetryFrame>k__BackingField", Time.frameCount);
            Set(selector, "<SelectingHand>k__BackingField", "left");
            Set(selector, "<LeftTelemetry>k__BackingField", new ControllerRaySelector.TelemetrySample {
                available = true, rayAvailable = true, tracked = 1, triggerHeld = 1, triggerValue = 0.8f,
                rotation = Quaternion.identity, position = new Vector3(1, 2, 3),
                rayOrigin = new Vector3(1, 2, 3), rayDirection = Vector3.forward, rayEnd = obj.transform.position, target = obj });
            Set(selector, "<RightTelemetry>k__BackingField", new ControllerRaySelector.TelemetrySample {
                tracked = -1, triggerHeld = -1, triggerValue = -1, rotation = Quaternion.identity });
            // The sentinel ends the fixture after the real game increments its round; no study transitions execute.
            game.OnObjectFound += _ => throw new CaptureObserved();
            bool observed = false;
            try { Call(game, "OnObjectCaptured", obj); }
            catch (TargetInvocationException e) when (e.InnerException is CaptureObserved) { observed = true; }
            Assert(observed && game.CurrentObjectiveIndex == 1, "Real capture advances the manager round.");
            Set(game, "<IsPractice>k__BackingField", true);
            Call(recorder, "Transition", 1, "Blue", "Cube");
            Call(recorder, "ObjectsReady", 1, "Blue", "Cube");
            Set(game, "<IsPractice>k__BackingField", false);
            Set(game, "m_CurrentRound", 10);
            Call(recorder, "Transition", 10, "Blue", "Cube");
            Call(recorder, "ObjectsReady", 10, "Blue", "Cube");
            Set(game, "m_CurrentRound", 11);
            Call(recorder, "Finish", true, "synthetic verification");
            var recording = TrialReplayFile.Read(Path.Combine(directory, "recording.jsonl"));
            var selection = recording.Records.Find(r => r.kind == "selection");
            Assert(selection != null && selection.round == 0 && selection.objectId == "fixture_target" && selection.correct && selection.dwell == 0 && selection.selectionMethod == "controller_ray_trigger_press_v1",
                "Recorder must retain original trial identity before the real game advances.");
            Assert(selection.selectingHand == "left" && selection.leftController.triggerHeld == 1 &&
                selection.leftController.hoveredId == "fixture_target" && selection.rightController.triggerHeld == -1 &&
                selection.leftController.position == new Vector3(1, 2, 3), "Capture uses actual selector snapshot before reset.");
            Assert(recording.Records[recording.Records.Count - 1].round == 10,
                "Completion linkage must not adopt the already advanced objective index.");
            Assert(recording.Trials.Count == 3 && recording.Trials[1].practice &&
                recording.Trials[1].runNumber == 0 && string.IsNullOrEmpty(recording.Trials[1].sourceTrialId) &&
                recording.Trials[2].block == 1 && recording.Trials[0].trialId != recording.Trials[2].trialId,
                "Repeated object IDs across practice and blocks require unique replay trials and correct linkage.");
            // Force the replay sink closed, then pass a correct choice through the live manager again.
            // Only the optional recorder may fail; the trial must still advance.
            var closed = new TrialReplayWriter(Path.Combine(directory, "closed.jsonl")); closed.Dispose();
            Set(recorder, "m_Writer", closed);
            Set(game, "m_CurrentRound", 0); Set(game, "<SearchActive>k__BackingField", true);
            observed = false;
            try { Call(game, "OnObjectCaptured", obj); }
            catch (TargetInvocationException e) when (e.InnerException is CaptureObserved) { observed = true; }
            Assert(observed && game.CurrentObjectiveIndex == 1 &&
                typeof(TrialReplayRecorder).GetField("m_Failure", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(recorder) != null,
                "Closed replay sink must not block the live choice or objective advancement.");
        }
        finally
        {
            if (recorder != null) Call(recorder, "OnDisable");
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(obj);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }
    static void Reject(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException) { failed = true; }
        Assert(failed, message);
    }
    [MenuItem("Tools/Codex/Trial Replay/Verify Rendering")]
    public static void RenderSmoke()
    {
        string root = Environment.GetEnvironmentVariable("REPLAY_SMOKE_OUTPUT");
        if (string.IsNullOrEmpty(root)) root = Path.Combine(Application.temporaryCachePath, "ReplayRender_" + Guid.NewGuid().ToString("N"));
        string file = CreateDemo(Path.Combine(root, "synthetic"));
        Run();
        var timeline = TrialReplayFile.Read(file);
        using (var renderer = new TrialReplayRenderer(timeline) { RecordedHead = true })
        {
            renderer.Prepare(timeline, 1);
            var image = renderer.Capture(1280, 720);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "preview.png"), image.EncodeToPNG());
                int colored = 0;
                foreach (var pixel in image.GetPixels32())
                    if (pixel.r > 100 || pixel.g > 100 || pixel.b > 100) colored++;
                Assert(colored > 500, "Rendered preview must contain visible geometry, not a black/empty image.");
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        Debug.Log("[TrialReplayChecks] Render PASS: " + Path.Combine(root, "preview.png"));

    }

    public static string CreateDemo(string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Demo output must be new.");
        Directory.CreateDirectory(Path.Combine(directory, "audio"));
        string file = Path.Combine(directory, "recording.jsonl");
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
        var savedMesh = new TrialReplayMesh { id = "cube", vertices = mesh.vertices, normals = mesh.normals, triangles = mesh.triangles };
        UnityEngine.Object.DestroyImmediate(cube);
        var objects = new List<TrialReplayObject>();
        var slots = RotationalSearchLayout.Build(RotationalSearchLayout.DefaultRadius);
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            objects.Add(new TrialReplayObject { id = "o" + i.ToString("D2"), mesh = "cube", shape = "Cube", color = i == 0 ? "Yellow" : "Blue",
                rgba = i == 0 ? Color.yellow : new Color(0.15f, 0.4f, 0.85f), target = i == 0,
                position = new Vector3(slot.x, slot.y, slot.z), rotation = Quaternion.Euler(0, slot.azimuth, 0), scale = Vector3.one * 0.2f });
        }
        var samples = new float[24000]; for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 440 * 2 * Mathf.PI / 24000) * 0.08f;
        File.WriteAllBytes(Path.Combine(directory, "audio/tone.wav"), TrialReplayAudio.Encode(samples, 1, 24000));
        using (var writer = new TrialReplayWriter(file))
        {
            long sequence = 0;
            Action<TrialReplayRecord> write = r => { r.sequence = sequence++; if (!writer.Append(r)) throw new IOException(writer.Error); };
            write(new TrialReplayRecord { kind = "header", recordingId = "synthetic-" + Guid.NewGuid().ToString("N"), sourceType = "synthetic_demo", detail = "SYNTHETIC DEMO — no participant data; tone is illustrative." });
            write(new TrialReplayRecord { kind = "trial", time = 0, trialId = "demo_trial", targetId = "o00", layout = RotationalSearchLayout.LayoutTag, objects = objects.ToArray(), meshes = new[] { savedMesh } });
            write(new TrialReplayRecord { kind = "search_start", time = 0, trialId = "demo_trial" });
            for (int i = 0; i <= 480; i++)
            {
                double t = i / 60.0;
                if (i == 30) write(new TrialReplayRecord { kind = "audio_playback_start", time = t, trialId = "demo_trial", clip = "audio/tone.wav", text = "Synthetic tone" });
                if (i == 90)
                {
                    write(new TrialReplayRecord { kind = "audio_playback_end", time = t, trialId = "demo_trial" });
                    write(new TrialReplayRecord { kind = "selection", time = t, trialId = "demo_trial", objectId = "o01", correct = false, searchSeconds = t });
                }
                if (i == 120) write(new TrialReplayRecord { kind = "search_paused", time = t, trialId = "demo_trial", searchSeconds = 2 });
                if (i == 180) write(new TrialReplayRecord { kind = "search_resumed", time = t, trialId = "demo_trial", searchSeconds = 2 });
                bool active = (t < 2 || t >= 3) && t < 6;
                string hover = t < 2 ? "o01" : "o00";
                write(new TrialReplayRecord { kind = "sample", time = t, frame = i, trialId = "demo_trial", headAvailable = true,
                    headPosition = Vector3.zero, headRotation = Quaternion.LookRotation(objects[0].position), headTracked = 1,
                    gazeAvailable = true, gazeTracked = t >= 4 && t < 4.25 ? 0 : 1,
                    gazeOrigin = Vector3.zero, gazeDirection = objects[hover == "o01" ? 1 : 0].position.normalized,
                    searchActive = active, visible = active, searchSeconds = Math.Min(5, t <= 2 ? t : t < 3 ? 2 : t - 1),
                    hoveredId = active ? "o00" : null,
                    leftController = new TrialReplayController {
                        available = true, tracked = t >= 4 && t < 4.25 ? 0 : 1,
                        triggerHeld = t >= 4 && t < 4.25 ? -1 : i % 60 == 0 ? 1 : 0,
                        triggerValue = t >= 4 && t < 4.25 ? -1 : i % 60 == 0 ? 1 : 0,
                        rayAvailable = !(t >= 4 && t < 4.25), position = new Vector3(-0.2f, 0, 0),
                        rayOrigin = new Vector3(-0.2f, 0, 0), rayEnd = objects[hover == "o01" ? 1 : 0].position,
                        rayDirection = objects[hover == "o01" ? 1 : 0].position.normalized,
                        hoveredId = active && !(t >= 4 && t < 4.25) ? hover : null },
                    rightController = new TrialReplayController(),
                    changes = i == 240 ? new[] { new TrialReplayTransform { id = "o02", position = objects[2].position + Vector3.up * 0.05f, rotation = objects[2].rotation, scale = objects[2].scale } } : null });
                if (i == 360) write(new TrialReplayRecord { kind = "selection", time = t, trialId = "demo_trial", objectId = "o00", correct = true, searchSeconds = 5 });
            }
            write(new TrialReplayRecord { kind = "end", time = 8, complete = true, detail = "synthetic demonstration complete" });
        }
        return file;
    }
}
