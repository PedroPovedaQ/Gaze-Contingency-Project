using System.Collections.Generic;
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

    // Combo queue: when non-empty, OnObjectSpawned uses queued combos instead of random.
    readonly Queue<(int shapeIdx, int colorIdx)> m_ComboQueue = new();

    /// <summary>Fires after a spawned object is fully configured with metadata.</summary>
    public event System.Action<GameObject> objectFullyConfigured;

    /// <summary>Pre-specify the next shape+color combo. Call before ObjectSpawner.TrySpawnObject.</summary>
    public void EnqueueCombo(string shapeName, string colorName)
    {
        int si = m_ShapeNames != null ? System.Array.IndexOf(m_ShapeNames, shapeName) : -1;
        int ci = System.Array.FindIndex(k_Colors, c => c.name == colorName);
        if (si >= 0 && ci >= 0)
            m_ComboQueue.Enqueue((si, ci));
        else
            Debug.LogWarning($"{k_Tag} Unknown combo: {shapeName}/{colorName}");
    }

    /// <summary>Public access to shape names for game manager.</summary>
    public string[] ShapeNames => m_ShapeNames;
    /// <summary>Public access to color definitions for game manager.</summary>
    public static (Color color, string name)[] Colors => k_Colors;

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
            m_ShapeMeshes = new Mesh[5];
            m_ShapeNames = new[] { "Sphere", "Cube", "Pyramid", "Cylinder", "Star" };

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

            // Cylinder
            var tempCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m_ShapeMeshes[3] = tempCylinder.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCylinder);

            // Star (procedural)
            m_ShapeMeshes[4] = CreateStarMesh();
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

        // Hide all child renderers (affordance visuals: rings, crosses, etc.)
        // We keep the child GameObjects alive — the affordance system's
        // XRInteractableAffordanceStateProvider must remain active for gaze
        // interaction to work. Destroying children corrupts interactable state.
        HideChildRenderers(obj);

        // Pick shape and color — use queued combo if available, otherwise random
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
                // Swap MeshFilter on root
                var meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = obj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                // Ensure MeshRenderer exists on root
                var renderer = obj.GetComponent<MeshRenderer>();
                if (renderer == null)
                    renderer = obj.AddComponent<MeshRenderer>();

                // Swap collider for the shape (root only)
                ReplaceCollider(obj, mesh, shapeIdx);
            }

            shapeName = (m_ShapeNames != null && shapeIdx < m_ShapeNames.Length)
                ? m_ShapeNames[shapeIdx]
                : mesh != null ? mesh.name : "Unknown";
        }

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

        // Enable gaze interaction and update grab colliders
        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.allowGazeInteraction = true;
            grab.allowGazeSelect = false;

            // Update collider list — only root colliders (children hidden, not destroyed)
            grab.colliders.Clear();
            grab.colliders.AddRange(obj.GetComponents<Collider>());

            // Force re-registration with XRInteractionManager so the new
            // colliders appear in the manager's collider-to-interactable map.
            // Without this, the gaze interactor can't resolve hits on new colliders.
            grab.enabled = false;
            grab.enabled = true;

            // The toggle may recreate affordance child renderers — hide them again.
            HideChildRenderers(obj);

            Debug.Log($"{k_Tag} Grab re-registered with {grab.colliders.Count} collider(s), allowGaze={grab.allowGazeInteraction}");
        }

        // Add per-object highlight for controller blue glow
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

        objectFullyConfigured?.Invoke(obj);
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
        // Remove existing colliders on root only (children are kept alive).
        foreach (var col in obj.GetComponents<Collider>())
            DestroyImmediate(col);

        // Add colliders slightly larger than mesh for generous gaze targeting.
        // The visual mesh stays the same size; the collider extends ~30% beyond.
        switch (shapeIdx)
        {
            case 0: // Sphere — mesh radius is 0.5, collider 0.65
                var sc = obj.AddComponent<SphereCollider>();
                sc.radius = 0.65f;
                break;
            case 1: // Cube — mesh is 1x1x1, collider 1.3x1.3x1.3
                var bc = obj.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.3f, 1.3f, 1.3f);
                break;
            case 2: // Pyramid — use sphere collider for generous targeting
                var pc = obj.AddComponent<SphereCollider>();
                pc.center = new Vector3(0f, 0.4f, 0f); // center slightly above base
                pc.radius = 0.65f;
                break;
            case 3: // Cylinder — capsule collider matching cylinder shape
                var cc = obj.AddComponent<CapsuleCollider>();
                cc.radius = 0.65f;
                cc.height = 2.6f; // mesh height is 2.0, generous
                break;
            default: // Star — sphere collider for generous targeting
                var stc = obj.AddComponent<SphereCollider>();
                stc.radius = 0.7f;
                break;
        }
    }

    static Mesh CreatePyramidMesh()
    {
        var mesh = new Mesh();
        mesh.name = "Pyramid";

        float s = 0.5f; // half-size
        float h = 1f;   // height

        // Unique vertices per face for flat shading.
        // Double-sided base so bottom is visible when picked up.
        var vertices = new Vector3[]
        {
            // Base top (visible from above) — 0-3
            new(-s, 0, -s), new(s, 0, -s), new(s, 0, s), new(-s, 0, s),
            // Base bottom (visible from below) — 4-7
            new(-s, 0, -s), new(s, 0, -s), new(s, 0, s), new(-s, 0, s),
            // Front face — 8-10
            new(s, 0, -s), new(-s, 0, -s), new(0, h, 0),
            // Right face — 11-13
            new(s, 0, s), new(s, 0, -s), new(0, h, 0),
            // Back face — 14-16
            new(-s, 0, s), new(s, 0, s), new(0, h, 0),
            // Left face — 17-19
            new(-s, 0, -s), new(-s, 0, s), new(0, h, 0),
        };

        var triangles = new int[]
        {
            // Base top (facing up)
            0, 2, 1,
            0, 3, 2,
            // Base bottom (facing down — reversed winding)
            4, 5, 6,
            4, 6, 7,
            // Front
            8, 9, 10,
            // Right
            11, 12, 13,
            // Back
            14, 15, 16,
            // Left
            17, 18, 19,
        };

        // Explicit normals per vertex.
        var frontN = Vector3.Cross(
            vertices[9] - vertices[8], vertices[10] - vertices[8]).normalized;
        var rightN = Vector3.Cross(
            vertices[12] - vertices[11], vertices[13] - vertices[11]).normalized;
        var backN = Vector3.Cross(
            vertices[15] - vertices[14], vertices[16] - vertices[14]).normalized;
        var leftN = Vector3.Cross(
            vertices[18] - vertices[17], vertices[19] - vertices[17]).normalized;

        var normals = new Vector3[]
        {
            // Base top — facing up
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            // Base bottom — facing down
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
            // Front
            frontN, frontN, frontN,
            // Right
            rightN, rightN, rightN,
            // Back
            backN, backN, backN,
            // Left
            leftN, leftN, leftN,
        };

        // UVs for shader edge highlight.
        var uvs = new Vector2[]
        {
            // Base top
            new(0, 0), new(1, 0), new(1, 1), new(0, 1),
            // Base bottom
            new(0, 0), new(1, 0), new(1, 1), new(0, 1),
            // Front
            new(0, 0), new(1, 0), new(0.5f, 1),
            // Right
            new(0, 0), new(1, 0), new(0.5f, 1),
            // Back
            new(0, 0), new(1, 0), new(0.5f, 1),
            // Left
            new(0, 0), new(1, 0), new(0.5f, 1),
        };

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Creates a 5-pointed star extruded into 3D.
    /// Outer radius 0.5, inner radius 0.2, depth 0.2.
    /// </summary>
    static Mesh CreateStarMesh()
    {
        var mesh = new Mesh();
        mesh.name = "Star";

        const int points = 5;
        const float outerR = 0.5f;
        const float innerR = 0.2f;
        const float halfDepth = 0.1f;

        // Generate 2D star outline (10 vertices: alternating outer/inner)
        var outline = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / points;
            float r = (i % 2 == 0) ? outerR : innerR;
            outline[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        // Build mesh: front face, back face, and side quads
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();

        // Front face — fan from center
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
            tris.Add(frontCenter);
            tris.Add(frontCenter + 1 + next);
            tris.Add(frontCenter + 1 + i);
        }

        // Back face — fan from center (reversed winding)
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
            tris.Add(backCenter);
            tris.Add(backCenter + 1 + i);
            tris.Add(backCenter + 1 + next);
        }

        // Side faces — quads connecting front and back outlines
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
