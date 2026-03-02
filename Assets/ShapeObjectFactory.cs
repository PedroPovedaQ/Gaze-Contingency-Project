using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Post-processes spawned objects to assign a random shape and color.
/// Attaches to the same GameObject as ObjectSpawner. Subscribes to
/// ObjectSpawner.objectSpawned to transform each object after instantiation.
///
/// Also enables gaze interaction on the XRGrabInteractable so that
/// GazeHighlightManager can apply the orange glow on eye gaze hover.
///
/// If mesh and material references are not assigned in the Inspector,
/// the factory auto-initializes using Unity primitives and the
/// InteractablePrimitive shader.
/// </summary>
public class ShapeObjectFactory : MonoBehaviour
{
    /// <summary>
    /// Auto-attaches ShapeObjectFactory to the ObjectSpawner at scene load.
    /// No manual Inspector setup required.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var spawner = FindObjectOfType<ObjectSpawner>();
        if (spawner != null && spawner.GetComponent<ShapeObjectFactory>() == null)
        {
            spawner.gameObject.AddComponent<ShapeObjectFactory>();
            Debug.Log("[ShapeFactory] Auto-attached to " + spawner.gameObject.name);
        }
    }

    [Tooltip("Meshes to randomly pick from (Sphere, Cube, Pyramid). Auto-initialized if empty.")]
    [SerializeField] Mesh[] m_ShapeMeshes;

    [Tooltip("Names corresponding to each mesh in m_ShapeMeshes.")]
    [SerializeField] string[] m_ShapeNames;

    [Tooltip("Base material using InteractablePrimitive shader. Auto-initialized if null.")]
    [SerializeField] Material m_BaseMaterial;

    static readonly (Color color, string name)[] k_Colors =
    {
        (new Color(0.9f, 0.15f, 0.15f, 1f), "Red"),
        (new Color(0.15f, 0.35f, 0.9f, 1f), "Blue"),
        (new Color(0.95f, 0.85f, 0.1f, 1f), "Yellow"),
        (new Color(0.6f, 0.15f, 0.85f, 1f), "Purple"),
    };

    const string k_Tag = "[ShapeFactory]";
    ObjectSpawner m_Spawner;

    void OnEnable()
    {
        m_Spawner = GetComponent<ObjectSpawner>();
        if (m_Spawner == null)
        {
            Debug.LogWarning($"{k_Tag} No ObjectSpawner found on {gameObject.name}");
            return;
        }

        EnsureInitialized();

        m_Spawner.objectSpawned += OnObjectSpawned;
        Debug.Log($"{k_Tag} Attached to ObjectSpawner, meshes={m_ShapeMeshes?.Length}, material={m_BaseMaterial?.name}");
    }

    void OnDisable()
    {
        if (m_Spawner != null)
            m_Spawner.objectSpawned -= OnObjectSpawned;
    }

    void EnsureInitialized()
    {
        // Auto-create meshes from Unity primitives if not assigned
        if (m_ShapeMeshes == null || m_ShapeMeshes.Length == 0)
        {
            Debug.Log($"{k_Tag} Auto-initializing shape meshes");
            m_ShapeMeshes = new Mesh[3];
            m_ShapeNames = new[] { "Sphere", "Cube", "Pyramid" };

            // Sphere
            var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_ShapeMeshes[0] = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempSphere);

            // Cube
            var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_ShapeMeshes[1] = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);

            // Pyramid (procedural)
            m_ShapeMeshes[2] = CreatePyramidMesh();
        }

        // Auto-find material if not assigned
        if (m_BaseMaterial == null)
        {
            // Try to find an existing InteractablePrimitive material in the scene
            var renderers = FindObjectsOfType<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null &&
                    r.sharedMaterial.shader != null &&
                    r.sharedMaterial.shader.name.Contains("InteractablePrimitive"))
                {
                    m_BaseMaterial = r.sharedMaterial;
                    Debug.Log($"{k_Tag} Auto-found material: {m_BaseMaterial.name}");
                    break;
                }
            }

            // Fallback: create material from shader
            if (m_BaseMaterial == null)
            {
                var shader = Shader.Find("Shader Graphs/InteractablePrimitive");
                if (shader != null)
                {
                    m_BaseMaterial = new Material(shader);
                    Debug.Log($"{k_Tag} Created material from InteractablePrimitive shader");
                }
                else
                {
                    // Last resort: use standard shader
                    m_BaseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    Debug.LogWarning($"{k_Tag} InteractablePrimitive shader not found, using URP/Lit fallback");
                }
            }
        }
    }

    void OnObjectSpawned(GameObject obj)
    {
        // Remove SittingCylinder animation if present
        var anim = obj.GetComponent<Animation>();
        if (anim != null) Destroy(anim);

        // Destroy all child GameObjects (Interaction Affordance, Audio Affordance, etc.)
        // These carry visual artifacts ("weird circles") from the original prefab.
        for (int i = obj.transform.childCount - 1; i >= 0; i--)
            Destroy(obj.transform.GetChild(i).gameObject);

        // Pick random shape
        string shapeName = "Unknown";
        if (m_ShapeMeshes != null && m_ShapeMeshes.Length > 0)
        {
            int shapeIdx = Random.Range(0, m_ShapeMeshes.Length);
            var mesh = m_ShapeMeshes[shapeIdx];

            if (mesh != null)
            {
                // Swap MeshFilter on root (children are destroyed)
                var meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = obj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                // Ensure MeshRenderer exists on root
                var renderer = obj.GetComponent<MeshRenderer>();
                if (renderer == null)
                    renderer = obj.AddComponent<MeshRenderer>();

                // Swap or replace collider for the shape
                ReplaceCollider(obj, mesh, shapeIdx);
            }

            shapeName = (m_ShapeNames != null && shapeIdx < m_ShapeNames.Length)
                ? m_ShapeNames[shapeIdx]
                : mesh != null ? mesh.name : "Unknown";
        }

        // Pick random color
        int colorIdx = Random.Range(0, k_Colors.Length);
        var (color, colorName) = k_Colors[colorIdx];

        // Assign per-instance material with random base color
        var renderer2 = obj.GetComponent<MeshRenderer>();
        if (renderer2 != null && m_BaseMaterial != null)
        {
            var mat = new Material(m_BaseMaterial);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_EdgeHighlightFalloff", 1.5f);
            renderer2.material = mat;
        }

        // Ensure Rigidbody exists with gravity for physics drop
        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
            rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;

        // Enable gaze interaction and fix grab for dragging
        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.allowGazeInteraction = true;
            grab.allowGazeSelect = false;

            // Re-register colliders — old ones removed with DestroyImmediate,
            // so only the new collider is found here.
            grab.colliders.Clear();
            grab.colliders.AddRange(obj.GetComponentsInChildren<Collider>());
        }

        // Add per-object highlight (replaces destroyed affordance children)
        obj.AddComponent<InteractableHighlight>();

        // Adjust scale for simple shapes
        obj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        // Raise object 5cm above surface and let gravity drop it
        var pos = obj.transform.position;
        pos.y += 0.05f;
        obj.transform.position = pos;

        // Add metadata for gaze data logging
        var info = obj.AddComponent<SpawnableObjectInfo>();
        info.shapeName = shapeName;
        info.colorName = colorName;
        obj.name = info.DisplayName;

        Debug.Log($"{k_Tag} Spawned {info.DisplayName}");
    }

    void ReplaceCollider(GameObject obj, Mesh mesh, int shapeIdx)
    {
        // Remove existing colliders immediately so they don't appear
        // in GetComponentsInChildren when re-registering with XRGrabInteractable.
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            DestroyImmediate(col);

        // Add appropriate collider for the shape
        switch (shapeIdx)
        {
            case 0: // Sphere
                var sc = obj.AddComponent<SphereCollider>();
                sc.radius = 0.5f;
                break;
            case 1: // Cube
                obj.AddComponent<BoxCollider>();
                break;
            default: // Pyramid or other
                var mc = obj.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
                break;
        }
    }

    static Mesh CreatePyramidMesh()
    {
        var mesh = new Mesh();
        mesh.name = "Pyramid";

        float s = 0.5f; // half-size
        float h = 1f;   // height

        // Unique vertices per face for flat shading normals.
        // Winding order: clockwise when viewed from outside (Unity front-face convention).
        var vertices = new Vector3[]
        {
            // Base (2 triangles) — facing up (visible from above)
            new(-s, 0, -s), new(s, 0, -s), new(s, 0, s), new(-s, 0, s), // 0-3
            // Front face (outward normal toward -Z)
            new(s, 0, -s), new(-s, 0, -s), new(0, h, 0),                // 4-6
            // Right face (outward normal toward +X)
            new(s, 0, s), new(s, 0, -s), new(0, h, 0),                  // 7-9
            // Back face (outward normal toward +Z)
            new(-s, 0, s), new(s, 0, s), new(0, h, 0),                  // 10-12
            // Left face (outward normal toward -X)
            new(-s, 0, -s), new(-s, 0, s), new(0, h, 0),                // 13-15
        };

        var triangles = new int[]
        {
            // Base (facing up)
            0, 2, 1,
            0, 3, 2,
            // Front
            4, 5, 6,
            // Right
            7, 8, 9,
            // Back
            10, 11, 12,
            // Left
            13, 14, 15,
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
