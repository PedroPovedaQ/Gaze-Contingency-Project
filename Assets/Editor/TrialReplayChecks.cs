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
        string root = Path.Combine(Application.temporaryCachePath, "ReplayChecks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = CreateDemo(Path.Combine(root, "demo"));
            var timeline = TrialReplayFile.Read(file);
            Assert(timeline.Warning == null && timeline.Trials.Count == 1, "Round trip complete recording.");
            Assert(timeline.Trials[0].objects.Length == 56, "All 56 objects recorded.");
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
            byte[] before = File.ReadAllBytes(file);
            using (var rehearsal = new TrialReplayRehearsal(timeline, timeline.Trials[0], root, 100))
            {
                Assert(!rehearsal.Select("o01", 101), "New wrong response.");
                Assert(rehearsal.Select("o00", 103), "New correct response.");
                var fresh = TrialReplayFile.Read(Path.Combine(rehearsal.DirectoryPath, "recording.jsonl"));
                Assert(fresh.Header.sourceType == "desktop_rehearsal" && fresh.Records.FindAll(r => r.kind == "selection").Count == 2, "Rehearsal provenance and fresh choices.");
            }
            Assert(Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(file)), "Source immutable.");
            var warnings = new List<string>(); var wave = TrialReplayExport.MixAudio(timeline, 0, 4, warnings);
            Assert(warnings.Count == 0 && wave.Length == 44 + 4 * 48000 * 2, "Audio export duration and assets.");
            CheckLiveSelectionHook(root);
            // Graphics verification is separate so these checks also run with -nographics.
            Debug.Log("[TrialReplayChecks] PASS: roundtrip, 56 objects, wrong/correct choice, pause, rewind, motion, recovery, corruption, traversal, version, overwrite, rehearsal provenance, audio.");
        }
        finally { Directory.Delete(root, true); }
    }
    sealed class CaptureObserved : Exception { }
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
            var ui = host.AddComponent<FindObjectUI>();
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
            // The sentinel ends the fixture after the real game increments its round; no study transitions execute.
            game.OnObjectFound += _ => throw new CaptureObserved();
            bool observed = false;
            try { Call(game, "OnObjectCaptured", obj); }
            catch (TargetInvocationException e) when (e.InnerException is CaptureObserved) { observed = true; }
            Assert(observed && game.CurrentObjectiveIndex == 1, "Real capture advances the manager round.");
            Call(recorder, "Finish", true, "synthetic verification");
            var recording = TrialReplayFile.Read(Path.Combine(directory, "recording.jsonl"));
            var selection = recording.Records.Find(r => r.kind == "selection");
            Assert(selection != null && selection.round == 0 && selection.objectId == "fixture_target" && selection.correct,
                "Recorder must retain original trial identity before the real game advances.");
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
    public static void RenderSmoke()
    {
        string root = Environment.GetEnvironmentVariable("REPLAY_SMOKE_OUTPUT");
        if (string.IsNullOrEmpty(root)) throw new ArgumentException("Set REPLAY_SMOKE_OUTPUT to a new output folder.");
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
        TrialReplayExport.Export(file, Path.Combine(root, "export"), 0, 8, 30, 1280, 720, true);
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
            write(new TrialReplayRecord { kind = "trial", time = 0, trialId = "demo_trial", targetId = "o00", layout = "rotational_beta_v1", objects = objects.ToArray(), meshes = new[] { savedMesh } });
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
                bool active = (t < 2 || t >= 3) && t <= 6;
                string hover = t < 2 ? "o01" : "o00";
                write(new TrialReplayRecord { kind = "sample", time = t, frame = i, trialId = "demo_trial", headAvailable = true,
                    headPosition = Vector3.zero, headRotation = Quaternion.LookRotation(objects[0].position), headTracked = 1,
                    gazeAvailable = true, gazeTracked = t >= 4 && t < 4.25 ? 0 : 1,
                    gazeOrigin = Vector3.zero, gazeDirection = objects[hover == "o01" ? 1 : 0].position.normalized,
                    searchActive = active, visible = active, searchSeconds = Math.Min(5, t <= 2 ? t : t < 3 ? 2 : t - 1),
                    hoveredId = active ? hover : null, dwell = active ? (float)((t % 1.6) / 1.6) : 0,
                    changes = i == 240 ? new[] { new TrialReplayTransform { id = "o02", position = objects[2].position + Vector3.up * 0.05f, rotation = objects[2].rotation, scale = objects[2].scale } } : null });
                if (i == 360) write(new TrialReplayRecord { kind = "selection", time = t, trialId = "demo_trial", objectId = "o00", correct = true, searchSeconds = 5 });
            }
            write(new TrialReplayRecord { kind = "end", time = 8, complete = true, detail = "synthetic demonstration complete" });
        }
        return file;
    }
}
