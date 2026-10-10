#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using SpatialVoice;
using UnityEditor;
using UnityEngine;

public static class NavigationCatalogEditor
{
    const string AssetPath = "Assets/Resources/SpatialVoice/NavigationPhrases.asset";
    [MenuItem("Tools/Codex/Import Navigation Catalog")]
    public static void Import()
    {
        var json = JObject.Parse(File.ReadAllText("Assets/Resources/SpatialVoice/navigation-phrases.json"));
        var entries = (JArray)json["navigation_phrases"];
        var scratch = ScriptableObject.CreateInstance<NavigationPhraseDatabase>();
        try
        {
            scratch.phrases = new NavigationPhrase[entries.Count];
            scratch.version = (string)json["version"];
            for (int i = 0; i < entries.Count; i++)
            {
                var row = entries[i]; var c = row["constraints"];
                scratch.phrases[i] = new NavigationPhrase {
                    id = (string)row["id"], text = (string)row["text"],
                    minAngle = (float)c["min_angle"], maxAngle = (float)c["max_angle"],
                    minDistance = (float)c["min_distance_meters"], maxDistance = (float)c["max_distance_meters"],
                    metadata = row["metadata"].ToObject<SpeechPhraseMetadata>(),
                    tone = (NavigationTone)Enum.Parse(typeof(NavigationTone), (string)row["metadata"]["tone"], true)
                };
            }
            scratch.Validate();
            var asset = AssetDatabase.LoadAssetAtPath<NavigationPhraseDatabase>(AssetPath);
            if (asset == null) { AssetDatabase.CreateAsset(scratch, AssetPath); scratch = null; }
            else { Undo.RecordObject(asset, "Import navigation phrases"); asset.phrases = scratch.phrases; asset.version = scratch.version; EditorUtility.SetDirty(asset); }
            AssetDatabase.SaveAssets();
            var guidance = new JArray();
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var phrase in DirectionalHintPolicy.AllPhrases())
                if (seen.Add(phrase)) guidance.Add(new JObject { ["text"] = phrase,
                    ["semantics"] = JObject.FromObject(SpeechPhraseMetadata.Describe(phrase)) });
            File.WriteAllText("Assets/Resources/SpatialVoice/guidance-phrase-semantics.json", new JObject {
                ["version"] = "guidance-semantics-v1", ["phrases"] = guidance }.ToString());
            AssetDatabase.Refresh();
            Debug.Log("[SpatialVoice] Imported navigation catalog.");
        }
        finally { if (scratch != null) UnityEngine.Object.DestroyImmediate(scratch); }
    }
}
#endif
