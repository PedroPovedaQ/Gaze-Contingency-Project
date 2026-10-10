#if UNITY_EDITOR
using System;
using Newtonsoft.Json.Linq;
using SpatialVoice;
using UnityEditor;
using UnityEngine;

public static class SpatialNavigationChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception("Spatial navigation: " + message); }
    [MenuItem("Tools/Codex/Check Spatial Navigation")]
    public static void Run()
    {
        var db = Resources.Load<NavigationPhraseDatabase>("SpatialVoice/NavigationPhrases");
        Check(db != null && db.phrases.Length == 300, "300-entry ScriptableObject loads"); db.Validate();
        var authored = (JArray)JObject.Parse(System.IO.File.ReadAllText("Assets/Resources/SpatialVoice/navigation-phrases.json"))["navigation_phrases"];
        for (int i = 0; i < db.phrases.Length; i++)
            Check(db.phrases[i].id == (string)authored[i]["id"] &&
                JToken.DeepEquals(JToken.FromObject(db.phrases[i].metadata),
                    JToken.FromObject(authored[i]["metadata"].ToObject<SpeechPhraseMetadata>())), "asset semantics match JSON at " + i);
        var guidance = (JArray)JObject.Parse(System.IO.File.ReadAllText("Assets/Resources/SpatialVoice/guidance-phrase-semantics.json"))["phrases"];
        foreach (var row in guidance)
            Check((string)row["semantics"]["intent"] != "unknown" && JToken.DeepEquals(row["semantics"],
                JToken.FromObject(SpeechPhraseMetadata.Describe((string)row["text"]))), "guidance semantics export matches runtime");
        var output = new NavigationPhrase[4];
        foreach (float distance in new[] { 0f, .5f, 1f, 2f, 4f, 8f, 12f, 9999f })
            for (int angle = -180; angle <= 180; angle++)
            {
                int count = NavigationGeometry.Filter(db.phrases, new NavigationGeometry(angle, distance), output);
                Check(count == 4, "coverage at " + angle + "/" + distance);
                for (int i = 0; i < count; i++) Check(output[i].Matches(angle, distance), "candidate respects boundaries");
            }
        Check(NavigationGeometry.Filter(db.phrases, new NavigationGeometry(float.NaN, 2), output) == 0, "NaN rejected");
        Check(NavigationGeometry.Filter(db.phrases, new NavigationGeometry(181, 2), output) == 0, "outside grid rejected");
        Check(NavigationGeometry.Filter(db.phrases, new NavigationGeometry(0, -1), output) == 0, "negative distance rejected");
        Check(NavigationGeometry.Filter(db.phrases, new NavigationGeometry(0, 2), output, eligible: p => false) == 0, "unprepared clips excluded");
        Check(NavigationGeometry.TryMeasure(new Vector3(0, 2, 0), Vector3.forward, new Vector3(-3, 40, 0), out var left) &&
            Mathf.Abs(left.Angle + 90) < .001 && Mathf.Abs(left.Distance - 3) < .001, "left and horizontal distance ignores height");
        Check(NavigationGeometry.TryMeasure(Vector3.zero, Vector3.forward, Vector3.right, out var right) && right.Angle == 90, "right is positive");
        Check(NavigationGeometry.TryMeasure(Vector3.zero, Vector3.forward, Vector3.back, out var back) && Mathf.Abs(back.Angle) == 180, "rear seam");
        Check(!NavigationGeometry.TryMeasure(Vector3.zero, Vector3.up, Vector3.right, out _), "vertical gaze has no azimuth");
        Check(!NavigationGeometry.TryMeasure(Vector3.zero, Vector3.forward, Vector3.up, out _), "overlapping horizontal position rejected");
        Check(NavigationGeometry.TryMeasure(Vector3.zero, Vector3.right, Vector3.forward, out var rotated) && rotated.Angle == -90, "relative forward rotates with user");
        var preview = new GameObject("Navigation offline checks");
        try
        {
            var manager = preview.AddComponent<SpatialVoiceNavigationManager>();
            manager.Configure(null, null, () => "test-voice", db, allowStreaming: false);
            var first = manager.ChooseAsync(new NavigationGeometry(-45, 3), "Test state", null,
                System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            Check(first.HasValue && first.Value.Matches(-45, 3), "no-key fallback remains inside geometry");
            manager.MarkSpoken(first.Value);
            var second = manager.ChooseAsync(new NavigationGeometry(-45, 3), "Test state", null,
                System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            Check(second.HasValue && second.Value.text != first.Value.text, "avoid immediate local repetition");
            var empty = manager.ChooseAsync(new NavigationGeometry(-45, 3), "Test state", p => false,
                System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            Check(!empty.HasValue, "no fallback outside eligible library");
            manager.TriggerAsync(null).GetAwaiter().GetResult();
            Check(manager.LastStatus == "stream_preview_disabled_in_study", "preview cannot bypass study playback");
        }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
        Debug.Log("[SpatialNavigationChecks] PASS: 300 phrases, complete signed grid, distance seams, validity, preparation filter and relative transforms.");
    }
}
#endif
