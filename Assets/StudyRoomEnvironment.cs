using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Visual-only room shared by the study and desktop reconstruction.</summary>
[ExecuteAlways]
public sealed class StudyRoomEnvironment : MonoBehaviour
{
    public const string GrayFloorVersion = "neutral-room-v1";
    public const string Version = "neutral-room-v2";
    public const float Radius = 5f, Height = 6f;
    public const float CircleRadius = 1.5f, CircleLift = .003f;
    public static readonly Color WallColor = new Color(.38f, .38f, .38f, 1f);
    public static readonly Color FloorColor = new Color(.32f, .32f, .32f, 1f);
    public static readonly Color CircleColor = new Color(.35f, .33f, .23f, 1f);
    Material m_WallMaterial, m_FloorMaterial, m_CircleMaterial;
    Mesh m_Quad, m_Circle;
    Renderer[] m_Backdrop;
    bool[] m_BackdropHidden;

    // The template backdrop is smaller than this room. Keep its existing fade/state
    // intact while preventing it from occluding search; restore when search ends.
    public void HideBackdrop(Renderer[] renderers)
    {
        m_Backdrop = renderers;
        m_BackdropHidden = new bool[renderers.Length];
        for (int i=0;i<renderers.Length;i++)
        {
            m_BackdropHidden[i] = renderers[i].forceRenderingOff;
            renderers[i].forceRenderingOff = true;
        }
    }

    public static GameObject Create(Vector3 center, Quaternion rotation, float floorY, string version = Version)
    {
        var root = new GameObject("Neutral study room");
        root.transform.SetPositionAndRotation(new Vector3(center.x, floorY, center.z), rotation);
        root.AddComponent<StudyRoomEnvironment>().Build(version == Version);
        return root;
    }

    void Build(bool showCircle)
    {
        // Explicit mesh construction: no collider exists, even for one physics frame.
        m_Quad = new Mesh { name = "Room visual quad" };
        m_Quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
            new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        m_Quad.triangles = new[] { 0,2,1,0,3,2 }; // Inward-facing (-Z); outside views are cutaways.
        m_Quad.RecalculateNormals(); m_Quad.RecalculateBounds();
        // Flat neutral diffuse appearance: no specular, texture, emission, or cast shadows.
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        m_WallMaterial = new Material(shader) { color = WallColor };
        m_FloorMaterial = new Material(shader) { color = FloorColor };
        Panel("Floor", Vector3.zero, Quaternion.Euler(90,0,0), new Vector2(Radius*2,Radius*2), m_FloorMaterial);
        if (showCircle)
        {
            // Frozen v2 geometry: tangent to the eight object planes, not their corners.
            const int segments = 256;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * CircleRadius;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            m_Circle = new Mesh { name = "Seated floor circle", vertices = vertices, triangles = triangles };
            m_Circle.RecalculateNormals(); m_Circle.RecalculateBounds();
            m_CircleMaterial = new Material(shader) { color = CircleColor };
            Surface("Seated floor circle", Vector3.up * CircleLift, Quaternion.identity,
                Vector3.one, m_CircleMaterial, m_Circle);
        }
        for (int i=0; i<4; i++)
        {
            var rotation = Quaternion.Euler(0,i*90,0);
            Panel("Distant wall " + i, rotation * new Vector3(0,Height/2,Radius), rotation,
                new Vector2(Radius*2,Height), m_WallMaterial);
        }
    }

    void Panel(string label, Vector3 position, Quaternion rotation, Vector2 size, Material material)
        => Surface(label, position, rotation, new Vector3(size.x,size.y,1), material, m_Quad);

    void Surface(string label, Vector3 position, Quaternion rotation, Vector3 scale, Material material, Mesh mesh)
    {
        var panel = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        panel.layer = 2; // Ignore Raycast, in addition to having no colliders/interactables.
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = position; panel.transform.localRotation = rotation;
        panel.transform.localScale = scale;
        panel.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = panel.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    void OnDestroy()
    {
        if (m_Backdrop != null)
            for (int i=0;i<m_Backdrop.Length;i++)
                if (m_Backdrop[i] != null) m_Backdrop[i].forceRenderingOff = m_BackdropHidden[i];
        Release(m_Quad); Release(m_Circle); Release(m_WallMaterial); Release(m_FloorMaterial); Release(m_CircleMaterial);
    }
    static void Release(Object resource)
    {
        if (resource == null) return;
        if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
    }
}
