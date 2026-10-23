using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Regenerate the shared study card from the authored MR coaching style.</summary>
public static class StudyPromptAssets
{
    [MenuItem("Tools/Codex/Refresh Study Prompt Style")]
    public static void Generate()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MRTemplateAssets/Prefabs/UI/CoachingUI.prefab");
        var background = source.GetComponentsInChildren<Image>(true).Single(i => i.name == "CoachingCardRoot");
        var message = source.GetComponentsInChildren<TextMeshProUGUI>(true)
            .First(t => t.text.Contains("Tap a surface"));
        var root = new GameObject("StudyPromptCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(StudyPromptCard));
        try
        {
            var image = root.GetComponent<Image>();
            // The original root references a missing sprite in this recovered project.
            // Reuse the surviving rounded UI sprite from the same template package.
            image.sprite = background.sprite != null ? background.sprite :
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MRTemplateAssets/Sprites/UI/Round_Radius_10.png");
            if (image.sprite == null || message.font == null)
                throw new System.InvalidOperationException("The coaching card font and rounded sprite are required.");
            image.type = Image.Type.Sliced;
            image.color = background.color; image.raycastTarget = true;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(700,480);
            var serialized = new SerializedObject(root.GetComponent<StudyPromptCard>());
            serialized.FindProperty("m_Font").objectReferenceValue = message.font;
            serialized.FindProperty("m_TextColor").colorValue = message.color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/Resources/UI");
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/UI/StudyPromptCard.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[StudyPromptAssets] Shared card saved with template rounded sprite and original coaching font.");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
