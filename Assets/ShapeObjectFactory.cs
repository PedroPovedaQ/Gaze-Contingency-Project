using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Post-processes spawned objects to assign a random shape and color.
/// Also configures gaze interaction and colliders.
///
/// Call <see cref="FinalizeInteractable"/> after positioning to ensure
/// the XRGrabInteractable is properly registered with the interaction manager.
/// </summary>
public class ShapeObjectFactory : MonoBehaviour
{
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

    [SerializeField] Mesh[] m_ShapeMeshes;
    [SerializeField] string[] m_ShapeNames;
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
    readonly Queue<(int shapeIdx, int colorIdx)> m_ComboQueue = new();

    public event System.Action<GameObject> objectFullyConfigured;

    public void EnqueueCombo(string shapeName, string colorName)
    {
        int si = m_ShapeNames != null ? System.Array.IndexOf(m_ShapeNames, shapeName) : -1;
        int ci = System.Array.FindIndex(k_Colors, c => c.name == colorName);
        if (si >= 0 && ci >= 0)
            m_ComboQueue.Enqueue((si, ci));
        else
            Debug.LogWarning($"{k_Tag} Unknown combo: {shapeName}/{colorName}");
    }

    public string[] ShapeNames => m_ShapeNames;
    public static (Color color, string name)[] Colors => k_Colors;

    void OnEnable()
    {
        m_Spawner = GetComponent<ObjectSpawner>();
        if (m_Spawner == null) return;
        EnsureInitialized();
        m_Spawner.objectSpawned += OnObjectSpawned;
    }

    void OnDisable()
    {
        if (m_Spawner != null)
            m_Spawner.objectSpawned -= OnObjectSpawned;
    }

    void EnsureInitialized()
    {
        if (m_ShapeMeshes == null || m_ShapeMeshes.Length == 0)
        {
            m_ShapeMeshes = new Mesh[6];
            m_ShapeNames = new[] { "Sphere", "Cube", "Pyramid", "Cylinder", "Star", "Capsule" };

            var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_ShapeMeshes[0] = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempSphere);

            var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_ShapeMeshes[1] = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);

            m_ShapeMeshes[2] = CreatePyramidMesh();

            var tempCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m_ShapeMeshes[3] = tempCylinder.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCylinder);

            m_ShapeMeshes[4] = CreateStarMesh();

            var tempCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            m_ShapeMeshes[5] = tempCapsule.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCapsule);
        }

        if (m_BaseMaterial == null)
        {
            var renderers = FindObjectsOfType<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null &&
                    r.sharedMaterial.shader.name.Contains("InteractablePrimitive"))
                {
                    m_BaseMaterial = r.sharedMaterial;
                    break;
                }
            }
            if (m_BaseMaterial == null)
            {
                var shader = Shader.Find("Shader Graphs/InteractablePrimitive");
                if (shader != null)
                    m_BaseMaterial = new Material(shader);
                else
                    m_BaseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
        }
    }

    /// <summary>
    /// Configures a spawned object with shape, color, collider, material.
    /// Call this directly when bypassing ObjectSpawner.
    /// </summary>
    public void ConfigureObject(GameObject obj) => OnObjectSpawned(obj);

    void OnObjectSpawned(GameObject obj)
    {
        var anim = obj.GetComponent<Animation>();
        if (anim != null) Destroy(anim);

        HideChildRenderers(obj);

        // Pick shape and color
        int shapeIdx, colorIdx;
        if (m_ComboQueue.Count > 0)
        {
            var combo = m_ComboQueue.Dequeue();
            shapeIdx = combo.shapeIdx;
            colorIdx = combo.colorIdx;
        }
        else
        {
            shapeIdx = Random.Range(0, m_ShapeMeshes?.Length ?? 0);
            colorIdx = Random.Range(0, k_Colors.Length);
        }

        string shapeName = "Unknown";
        if (m_ShapeMeshes != null && shapeIdx < m_ShapeMeshes.Length)
        {
            var mesh = m_ShapeMeshes[shapeIdx];
            if (mesh != null)
            {
                var meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = obj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                var renderer = obj.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = obj.AddComponent<MeshRenderer>();

                ReplaceCollider(obj, mesh, shapeIdx);
            }

            shapeName = (m_ShapeNames != null && shapeIdx < m_ShapeNames.Length)
                ? m_ShapeNames[shapeIdx] : "Unknown";
        }

        var (color, colorName) = k_Colors[colorIdx];

        // Material
        var renderer2 = obj.GetComponent<MeshRenderer>();
        if (renderer2 != null && m_BaseMaterial != null)
        {
            var mat = new Material(m_BaseMaterial);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_EdgeHighlightFalloff", 1.5f);
            renderer2.material = mat;
        }

        // Set layer 8 on root AND all children so nothing blocks gaze on wrong layer
        obj.layer = 8;
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 8;

        // Kinematic rigidbody
        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // Scale (20% smaller — 8cm objects)
        Vector3 scale = shapeIdx switch
        {
            0 => new Vector3(0.08f, 0.08f, 0.08f), // Sphere
            1 => new Vector3(0.08f, 0.08f, 0.08f), // Cube
            2 => new Vector3(0.07f, 0.06f, 0.07f), // Pyramid (shorter to fit in row)
            3 => new Vector3(0.08f, 0.04f, 0.08f), // Cylinder
            4 => new Vector3(0.096f, 0.096f, 0.096f), // Star
            5 => new Vector3(0.035f, 0.035f, 0.035f), // Capsule (pill — rotated on side, uniform scale)
            _ => new Vector3(0.08f, 0.08f, 0.08f),
        };
        obj.transform.localScale = scale;

        // Pyramid: origin at base (y=0), need to lower so base sits on shelf.
        // Other shapes have origin at center so spawn Y puts them on the shelf.
        // Pyramid needs to come down by the full half-height offset (0.04m).
        if (shapeIdx == 2)
        {
            var pos = obj.transform.position;
            pos.y -= 0.06f;
            obj.transform.position = pos;
        }


        // Metadata
        var info = obj.AddComponent<SpawnableObjectInfo>();
        info.shapeName = shapeName;
        info.colorName = colorName;
        obj.name = info.DisplayName;

        objectFullyConfigured?.Invoke(obj);
    }

    /// <summary>
    /// Centralized function to set up gaze interaction on a fully positioned object.
    /// Call AFTER the object is at its final position/rotation/scale.
    /// This ensures colliders, interactable registration, and renderer state are all correct.
    /// </summary>
    /// <summary>
    /// Phase 1: Disable grab and set up colliders. Call on ALL objects first.
    /// </summary>
    public static void PrepareInteractable(GameObject obj)
    {
        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab == null) return;

        // Disable to unregister from interaction manager
        grab.enabled = false;

        // Destroy ALL child colliders — only keep the root shape collider
        foreach (var col in obj.GetComponentsInChildren<Collider>(true))
            if (col.gameObject != obj)
                DestroyImmediate(col);

        // Set layer 8 everywhere
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 8;

        // Set collider list to ONLY the root shape collider
        grab.colliders.Clear();
        foreach (var col in obj.GetComponents<Collider>())
            grab.colliders.Add(col);

        grab.allowGazeInteraction = true;
        grab.allowGazeSelect = false;
        grab.allowGazeAssistance = false;

        // Hide child renderers, keep root visible
        HideChildRenderers(obj);
        var rootRenderer = obj.GetComponent<MeshRenderer>();
        if (rootRenderer != null) rootRenderer.enabled = true;
    }

    /// <summary>
    /// Phase 2: Enable grab to register with the interaction manager.
    /// Call on ALL objects AFTER PrepareInteractable has run on all of them.
    /// </summary>
    public static void ActivateInteractable(GameObject obj)
    {
        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab == null) return;
        grab.enabled = true;

        // OnEnable may recreate child renderers — hide them again
        HideChildRenderers(obj);
        var rootRenderer = obj.GetComponent<MeshRenderer>();
        if (rootRenderer != null) rootRenderer.enabled = true;
    }

    // Keep for backward compat
    public static void FinalizeInteractable(GameObject obj)
    {
        PrepareInteractable(obj);
        ActivateInteractable(obj);
    }

    static void HideChildRenderers(GameObject obj)
    {
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }
    }

    void ReplaceCollider(GameObject obj, Mesh mesh, int shapeIdx)
    {
        // Destroy ALL colliders — root AND children. Stale child colliders from
        // the prefab can block the gaze raycast without being mapped to the
        // interactable, causing some objects to not highlight.
        foreach (var col in obj.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(col);

        switch (shapeIdx)
        {
            case 0:
                var sc = obj.AddComponent<SphereCollider>();
                sc.radius = 0.65f;
                break;
            case 1:
                var bc = obj.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.3f, 1.3f, 1.3f);
                break;
            case 2:
                var pc = obj.AddComponent<SphereCollider>();
                pc.center = new Vector3(0f, 0.4f, 0f);
                pc.radius = 0.65f;
                break;
            case 3:
                var cc = obj.AddComponent<CapsuleCollider>();
                cc.radius = 0.65f;
                cc.height = 2.6f;
                break;
            case 4: // Star
                var stc = obj.AddComponent<SphereCollider>();
                stc.radius = 0.7f;
                break;
            case 5: // Capsule — very generous collider to match other shapes' hit areas
                var capc = obj.AddComponent<SphereCollider>();
                capc.radius = 2.0f;
                break;
            default:
                var defc = obj.AddComponent<SphereCollider>();
                defc.radius = 0.65f;
                break;
        }
    }

    static Mesh CreatePyramidMesh()
    {
        var mesh = new Mesh { name = "Pyramid" };
        float s = 0.5f;
        float h = 1f;
        var vertices = new Vector3[]
        {
            new(-s, 0, -s), new(s, 0, -s), new(s, 0, s), new(-s, 0, s),
            new(-s, 0, -s), new(s, 0, -s), new(s, 0, s), new(-s, 0, s),
            new(s, 0, -s), new(-s, 0, -s), new(0, h, 0),
            new(s, 0, s), new(s, 0, -s), new(0, h, 0),
            new(-s, 0, s), new(s, 0, s), new(0, h, 0),
            new(-s, 0, -s), new(-s, 0, s), new(0, h, 0),
        };
        var triangles = new int[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
        };
        var frontN = Vector3.Cross(vertices[9] - vertices[8], vertices[10] - vertices[8]).normalized;
        var rightN = Vector3.Cross(vertices[12] - vertices[11], vertices[13] - vertices[11]).normalized;
        var backN = Vector3.Cross(vertices[15] - vertices[14], vertices[16] - vertices[14]).normalized;
        var leftN = Vector3.Cross(vertices[18] - vertices[17], vertices[19] - vertices[17]).normalized;
        var normals = new Vector3[]
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
            frontN, frontN, frontN, rightN, rightN, rightN,
            backN, backN, backN, leftN, leftN, leftN,
        };
        var uvs = new Vector2[]
        {
            new(0, 0), new(1, 0), new(1, 1), new(0, 1),
            new(0, 0), new(1, 0), new(1, 1), new(0, 1),
            new(0, 0), new(1, 0), new(0.5f, 1),
            new(0, 0), new(1, 0), new(0.5f, 1),
            new(0, 0), new(1, 0), new(0.5f, 1),
            new(0, 0), new(1, 0), new(0.5f, 1),
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateStarMesh()
    {
        var mesh = new Mesh { name = "Star" };
        const int points = 5;
        const float outerR = 0.5f;
        const float innerR = 0.2f;
        const float halfDepth = 0.25f;
        var outline = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / points;
            float r = (i % 2 == 0) ? outerR : innerR;
            outline[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        int frontCenter = verts.Count;
        verts.Add(new Vector3(0, 0, -halfDepth));
        norms.Add(Vector3.back);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < outline.Length; i++)
        {
            verts.Add(new Vector3(outline[i].x, outline[i].y, -halfDepth));
            norms.Add(Vector3.back);
            uvs.Add(new Vector2(outline[i].x + 0.5f, outline[i].y + 0.5f));
        }
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            tris.Add(frontCenter); tris.Add(frontCenter + 1 + next); tris.Add(frontCenter + 1 + i);
        }
        int backCenter = verts.Count;
        verts.Add(new Vector3(0, 0, halfDepth));
        norms.Add(Vector3.forward);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < outline.Length; i++)
        {
            verts.Add(new Vector3(outline[i].x, outline[i].y, halfDepth));
            norms.Add(Vector3.forward);
            uvs.Add(new Vector2(outline[i].x + 0.5f, outline[i].y + 0.5f));
        }
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            tris.Add(backCenter); tris.Add(backCenter + 1 + i); tris.Add(backCenter + 1 + next);
        }
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            var a = new Vector3(outline[i].x, outline[i].y, -halfDepth);
            var b = new Vector3(outline[next].x, outline[next].y, -halfDepth);
            var c = new Vector3(outline[next].x, outline[next].y, halfDepth);
            var d = new Vector3(outline[i].x, outline[i].y, halfDepth);
            var sideNormal = Vector3.Cross(b - a, d - a).normalized;
            int si = verts.Count;
            verts.AddRange(new[] { a, b, c, d });
            norms.AddRange(new[] { sideNormal, sideNormal, sideNormal, sideNormal });
            uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            tris.AddRange(new[] { si, si + 1, si + 2, si, si + 2, si + 3 });
        }
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
