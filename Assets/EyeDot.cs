using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Optional gaze feedback at the bounded object plane, never a selection target.</summary>
[DefaultExecutionOrder(310)]
public sealed class EyeDot : MonoBehaviour
{
    public bool Visible { get; private set; }
    public bool Drawn => m_Renderer != null && m_Renderer.enabled;
    public Vector3 PlanePoint { get; private set; }
    public int PlaneId { get; private set; } = -1;
    FindObjectGameManager m_Game;
    MeshRenderer m_Renderer;
    Mesh m_Mesh;
    Material m_Material;

    void Awake() => m_Game = GetComponent<FindObjectGameManager>();
    public void SetVisible(bool visible)
    {
        Visible = visible;
        if (!visible) Hide();
    }

    void LateUpdate()
    {
        var stream = SharedGazeStream.Instance;
        if (!Visible || m_Game == null || m_Game.SpawnedObjects.Count == 0 ||
            !m_Game.SearchActive ||
            stream == null || !stream.TryGetValid(out var sample) ||
            !m_Game.TryEyeDotPoint(sample.worldOrigin, sample.worldDirection, out var point, out int plane))
        { Hide(); return; }
        if (m_Renderer == null) CreateMarker();
        if (m_Renderer == null) return;
        PlanePoint = point; PlaneId = plane;
        m_Renderer.transform.SetPositionAndRotation(point, Quaternion.LookRotation(sample.worldDirection));
        m_Renderer.enabled = true;
    }

    void CreateMarker()
    {
        // Resources reference keeps the shader in Android builds even with stripping.
        var shader = Resources.Load<Shader>("EyeDot");
        if (shader == null) { Debug.LogError("[EyeDot] Missing marker shader"); SetVisible(false); return; }
        var marker = new GameObject("Eye dot marker");
        marker.transform.SetParent(transform, false);
        marker.layer = 2; // Ignore Raycast; no collider, interaction or target components.
        m_Mesh = new Mesh { name = "Eye dot disc" };
        const int segments = 32;
        var vertices = new Vector3[segments + 1];
        var indices = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .009f;
            indices[i * 3] = 0; indices[i * 3 + 1] = i + 1; indices[i * 3 + 2] = (i + 1) % segments + 1;
        }
        m_Mesh.vertices = vertices; m_Mesh.triangles = indices; m_Mesh.RecalculateBounds();
        marker.AddComponent<MeshFilter>().sharedMesh = m_Mesh;
        m_Renderer = marker.AddComponent<MeshRenderer>();
        m_Material = new Material(shader);
        m_Renderer.sharedMaterial = m_Material;
        m_Renderer.shadowCastingMode = ShadowCastingMode.Off;
        m_Renderer.receiveShadows = false;
    }
    void Hide() { if (m_Renderer != null) m_Renderer.enabled = false; PlaneId = -1; }
    void OnDisable() => Hide();
    void OnApplicationFocus(bool focused) { if (!focused) Hide(); }
    void OnDestroy()
    {
        if (m_Renderer != null) Destroy(m_Renderer.gameObject);
        if (m_Mesh != null) Destroy(m_Mesh);
        if (m_Material != null) Destroy(m_Material);
    }
}
