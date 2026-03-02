#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility to generate bare shape prefabs from FBX meshes.
/// Run via menu: Tools > Generate Shape Prefabs
/// </summary>
public static class ShapePrefabGenerator
{
    static readonly string[] k_MeshPaths =
    {
        "Assets/MRTemplateAssets/Models/Primitives/Sphere.fbx",
        "Assets/MRTemplateAssets/Models/Primitives/Cube.fbx",
        "Assets/MRTemplateAssets/Models/Primitives/Pyramid.fbx",
    };

    static readonly string[] k_Names = { "Sphere", "Cube", "Pyramid" };

    const string k_OutputFolder = "Assets/Prefabs/Shapes";
    const string k_MaterialPath = "Assets/MRTemplateAssets/Materials/Primitive/Interactables.mat";

    [MenuItem("Tools/Generate Shape Prefabs")]
    static void Generate()
    {
        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(k_OutputFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Shapes");

        var baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);

        for (int i = 0; i < k_MeshPaths.Length; i++)
        {
            CreateShapePrefab(k_Names[i], k_MeshPaths[i], baseMaterial);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ShapePrefabGenerator] Done! Created 3 prefabs in " + k_OutputFolder);
    }

    static void CreateShapePrefab(string shapeName, string meshPath, Material material)
    {
        // Load mesh from FBX
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(meshPath);
        Mesh mesh = null;
        foreach (var asset in allAssets)
        {
            if (asset is Mesh m)
            {
                mesh = m;
                break;
            }
        }

        if (mesh == null)
        {
            Debug.LogError($"[ShapePrefabGenerator] No mesh found in {meshPath}");
            return;
        }

        // Create GameObject
        var go = new GameObject(shapeName);

        // MeshFilter
        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        // MeshRenderer
        var meshRenderer = go.AddComponent<MeshRenderer>();
        if (material != null)
            meshRenderer.sharedMaterial = material;

        // MeshCollider (convex for physics interaction)
        var meshCollider = go.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;

        // Rigidbody
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 1f;

        // Scale — FBX primitives are in centimeters, scale to ~10cm
        go.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Save as prefab
        string prefabPath = $"{k_OutputFolder}/{shapeName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        Debug.Log($"[ShapePrefabGenerator] Created {prefabPath}");
    }
}
#endif
