using UnityEngine;
using UnityEngine.Rendering;

/// <summary>100 independently moving faceted fragments in one draw, with no physics or search colliders.</summary>
public sealed class TargetCrumble : MonoBehaviour
{
    public const int PieceCount = 100;
    public const float Duration = 1.2f;
    readonly Vector3[] m_Centers = new Vector3[PieceCount], m_Velocities = new Vector3[PieceCount], m_Axes = new Vector3[PieceCount];
    readonly Vector3[] m_Corners = new Vector3[PieceCount * 4], m_Vertices = new Vector3[PieceCount * 12];
    Mesh m_Mesh;
    Material m_Material;
    FindObjectGameManager m_Game;
    float m_Time;
    static readonly int[] k_Faces = { 0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3 };

    public static TargetCrumble Play(GameObject target, FindObjectGameManager game)
    {
        if (target == null || !target.TryGetComponent<Renderer>(out var source)) return null;
        var root = new GameObject("Correct target crumble (100 fragments)");
        root.transform.position = source.bounds.center;
        var effect = root.AddComponent<TargetCrumble>();
        effect.m_Game = game;
        effect.Create(target, source);
        // Keep the actual object/metadata alive for capture observers and normal
        // round cleanup. Only its visible surface is replaced after timing stops.
        source.enabled = false;
        return effect;
    }

    void Create(GameObject target, Renderer source)
    {
        var bounds = source.bounds;
        var collider = target.GetComponent<Collider>();
        var random = new System.Random(100); // Never disturb the experiment's RNG.
        float Next() => (float)random.NextDouble();
        float size = Mathf.Max(.002f, bounds.size.magnitude * .085f);
        for (int i = 0; i < PieceCount; i++)
        {
            var point = bounds.min + Vector3.Scale(bounds.size, new Vector3(Next(), Next(), Next()));
            // Pull outside samples onto the surface, preserving the selected shape's silhouette.
            if (collider != null && collider.enabled) point = collider.ClosestPoint(point);
            m_Centers[i] = point - bounds.center;
            var outward = m_Centers[i].sqrMagnitude > .000001f ? m_Centers[i].normalized : Vector3.up;
            m_Velocities[i] = outward * (.16f + Next() * .2f) + Vector3.up * (.2f + Next() * .2f);
            m_Axes[i] = new Vector3(Next() + .1f, Next() + .1f, Next() + .1f).normalized;
            Quaternion rotation = Quaternion.Euler(Next() * 360, Next() * 360, Next() * 360);
            float radius = size * (.65f + Next() * .7f);
            m_Corners[i * 4] = rotation * new Vector3(1, 1, 1) * radius;
            m_Corners[i * 4 + 1] = rotation * new Vector3(-1, -1, 1) * radius;
            m_Corners[i * 4 + 2] = rotation * new Vector3(-1, 1, -1) * radius;
            m_Corners[i * 4 + 3] = rotation * new Vector3(1, -1, -1) * radius;
        }
        m_Mesh = new Mesh { name = "100 crumble fragments" }; m_Mesh.MarkDynamic();
        var indices = new int[m_Vertices.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        UpdateGeometry(0);
        m_Mesh.triangles = indices; m_Mesh.RecalculateNormals();
        gameObject.AddComponent<MeshFilter>().sharedMesh = m_Mesh;
        m_Material = new Material(source.sharedMaterial);
        var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = m_Material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    // Brief outward lift, tumbling fall, then shrink away. The independent root
    // survives removal of the selected object, including the final trial.
    void UpdateGeometry(float seconds)
    {
        for (int i = 0; i < PieceCount; i++)
        {
            float t = Mathf.Max(0, seconds - (i % 10) * .008f);
            float shrink = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, 1.1f, t));
            Vector3 center = m_Centers[i] + m_Velocities[i] * t + Vector3.down * (1.1f * t * t);
            Quaternion tumble = Quaternion.AngleAxis(t * (140 + i * 3), m_Axes[i]);
            for (int j = 0; j < 12; j++)
                m_Vertices[i * 12 + j] = center + tumble * m_Corners[i * 4 + k_Faces[j]] * shrink;
        }
        m_Mesh.vertices = m_Vertices;
        m_Mesh.RecalculateBounds(); m_Mesh.RecalculateNormals();
    }
    void Update()
    {
        if (m_Game != null && m_Game.ResearcherPaused) return;
        m_Time += Time.deltaTime;
        if (m_Time >= Duration) { Destroy(gameObject); return; }
        UpdateGeometry(m_Time);
    }
    void OnDestroy()
    {
        if (Application.isPlaying) { Destroy(m_Mesh); Destroy(m_Material); }
        else { DestroyImmediate(m_Mesh); DestroyImmediate(m_Material); }
    }
}
