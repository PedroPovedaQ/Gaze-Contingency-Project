using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class TrialReviewCatalogChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception("Catalog: " + message); }
    public static void Run(TrialReplayTimeline timeline)
    {
        string root = Path.Combine(Path.GetTempPath(), "trial-catalog-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "catalog.json"), source = Path.Combine(root, "recording.jsonl");
            File.WriteAllText(source, "original research data");
            var catalog = new TrialReviewCatalog(path);
            var dataset = catalog.Register(source, "recording-1", "DEV_NEUTRAL", "Development");
            catalog.SaveParticipant("p020", "Pilot 20");
            catalog.Edit(dataset, "P020", "Morning run", "Correct pilot assignment");
            string attachment = Path.Combine(root, "voice's sample.wav"); File.WriteAllText(attachment, "test");
            catalog.Attach(dataset, attachment); catalog.Attach(dataset, attachment); catalog.Save();
            var restored = new TrialReviewCatalog(path);
            var saved = restored.Data.datasets.Single();
            Check(saved.participantId == "P020" && saved.sourceParticipantId == "DEV_NEUTRAL", "Assignment is separate from source ID");
            Check(saved.name == "Morning run" && restored.FindParticipant("P020").name == "Pilot 20", "Names survive reload");
            Check(saved.files.Count == 2 && saved.assignments.Single().reason == "Correct pilot assignment", "One link per file and reassignment audit");
            Check(File.ReadAllText(source) == "original research data", "Source stays byte-identical");
            string exports = Path.Combine(root, "generated"); Directory.CreateDirectory(exports);
            string condensed = Path.Combine(exports, "trials_condensed.csv");
            File.WriteAllText(condensed, "participant_id,trial_id\nP001,run_r00\n");
            restored.AttachFolderFiles(saved, exports); restored.AttachFolderFiles(saved, exports); restored.Save();
            Check(new TrialReviewCatalog(path).Data.datasets.Single().files.Count(f => f.path == condensed) == 1,
                "Generated condensed CSV is independently catalogued once and survives reload");
            bool rejected = false;
            try { restored.Edit(saved, "DEV_NEUTRAL", "", ""); } catch (ArgumentException) { rejected = true; }
            Check(rejected && saved.participantId == "P020", "No reassignment without reason");
            var other = new TrialReviewCatalog(path); other.SaveParticipant("P021", "Other"); other.Save();
            restored.SaveParticipant("P022", "Conflict"); rejected = false;
            try { restored.Save(); } catch (IOException) { rejected = true; }
            Check(rejected && new TrialReviewCatalog(path).FindParticipant("P021") != null, "Second viewer cannot overwrite a newer catalog");
            string copy = Path.Combine(root, "copy.jsonl"); File.Copy(source, copy);
            var existing = other.Register(copy, "recording-1", "DEV_NEUTRAL", "");
            Check(existing.id == saved.id && other.Data.datasets.Count == 1, "Copied recording keeps dataset identity");
            var separate = other.Register(source, "recording-2", "DEV_NEUTRAL", "");
            Check(separate.id != existing.id && separate.participantId == "DEV_NEUTRAL",
                "Another developer recording does not inherit this run's reassignment");
            Check(other.Register(source, "recording-3", "DEV_NEUTRAL", "").id != separate.id,
                "A new recording at a reused path gets its own dataset");
            foreach (double at in new[] { 1.0, 21.0, 1.0 })
            {
                timeline.Seek(at);
                Check(timeline.State.Trial.trialId == (at == 21 ? "demo-1" : "demo-0"), "Arbitrary forward/backward study seek restores the correct trial");
            }
            Check(TrialReviewTracks.AtFraction(10, 40, -.3f) == 10 && TrialReviewTracks.AtFraction(10, 40, 2) == 40,
                "Dragging outside either endpoint clamps to recording range");
            CheckPlaybackKeyboard(timeline);
            Debug.Log("TRIAL_REVIEW_CATALOG_PASS names, assignment audit, file links, reload, copies, write conflict, unchanged source, cross-trial seeks");
        }
        finally { Directory.Delete(root, true); }
    }
    static void CheckPlaybackKeyboard(TrialReplayTimeline timeline)
    {
        var root = new GameObject("Viewer keyboard check");
        try
        {
            var app = root.AddComponent<TrialReviewApp>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(TrialReviewApp).GetField("m_Timeline", flags).SetValue(app, timeline);
            var playing = typeof(TrialReviewApp).GetField("m_Playing", flags);
            var keyboard = typeof(TrialReviewApp).GetMethod("HandlePlaybackKeyboard", flags);
            void Key(EventType type, bool enabled = true, string field = "") => keyboard.Invoke(app,
                new object[] { new Event { type = type, keyCode = KeyCode.Space }, enabled, field });
            Key(EventType.KeyDown, true, "text:trial-search");
            Check(!(bool)playing.GetValue(app), "Space stays text while searching");
            Key(EventType.KeyDown, false);
            Check(!(bool)playing.GetValue(app), "Modal dialog blocks playback shortcut");
            Key(EventType.KeyDown);
            Check((bool)playing.GetValue(app), "Space starts playback");
            Key(EventType.KeyDown);
            Check((bool)playing.GetValue(app), "Held Space does not toggle repeatedly");
            Key(EventType.KeyUp); Key(EventType.KeyDown);
            Check(!(bool)playing.GetValue(app), "Next Space pauses playback");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
