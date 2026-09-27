using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Owns only a preview scene. It never loads, edits or runs the study scene.</summary>
public sealed class TrialReplayRenderer : IDisposable
{
    readonly PreviewRenderUtility m_Preview = new PreviewRenderUtility();
    readonly Dictionary<string, Mesh> m_Meshes = new Dictionary<string, Mesh>();
    readonly Dictionary<string, GameObject> m_Objects = new Dictionary<string, GameObject>();
    readonly List<Material> m_Materials = new List<Material>();
    readonly GameObject m_Root;
    readonly LineRenderer m_Ray;
    readonly LineRenderer m_LeftRay, m_RightRay;
    readonly LineRenderer m_CrossHorizontal, m_CrossVertical, m_CrossCharge;
    string m_Trial;
    public float Yaw = 0, Pitch = 25, Distance = 5;
    public bool RecordedHead, Overhead, ShowGaze = true, ShowTarget, ShowControllers = true;
    public Camera Camera => m_Preview.camera;

    public TrialReplayRenderer(TrialReplayTimeline timeline)
    {
        m_Preview.camera.clearFlags = CameraClearFlags.SolidColor;
        m_Preview.camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f);
        m_Preview.camera.nearClipPlane = 0.01f; m_Preview.camera.farClipPlane = 100;
        m_Preview.camera.fieldOfView = 65;
        m_Root = new GameObject("Replay preview (isolated)");
        m_Preview.AddSingleGO(m_Root);
        foreach (var pair in timeline.Meshes)
        {
            var saved = pair.Value;
            var mesh = new Mesh { name = pair.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = saved.vertices; mesh.triangles = saved.triangles;
            if (saved.normals != null && saved.normals.Length == saved.vertices.Length) mesh.normals = saved.normals;
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds(); m_Meshes.Add(pair.Key, mesh);
        }
        m_Ray = MakeRay("Recorded gaze", new Color(1, 0.55f, 0.1f));
        m_LeftRay = MakeRay("Left controller", Color.cyan);
        m_RightRay = MakeRay("Right controller", new Color(0.65f, 0.4f, 1));
        m_CrossHorizontal = MakeRay("Readiness cross horizontal", Color.white);
        m_CrossVertical = MakeRay("Readiness cross vertical", Color.white);
        m_CrossCharge = MakeRay("Readiness left-to-right charge", Color.green);
    }

    LineRenderer MakeRay(string name, Color color)
    {
        var obj = new GameObject(name); obj.transform.SetParent(m_Root.transform);
        var line = obj.AddComponent<LineRenderer>();
        line.sharedMaterial = Material(color); line.positionCount = 2;
        line.startWidth = line.endWidth = 0.009f; line.useWorldSpace = true;
        return line;
    }
    void DrawController(LineRenderer line, TrialReplayController c)
    {
        line.enabled = ShowControllers && c != null && c.rayAvailable && c.tracked == 1;
        if (!line.enabled) return;
        line.SetPosition(0, c.rayOrigin); line.SetPosition(1, c.rayEnd);
        line.startWidth = line.endWidth = c.triggerHeld == 1 ? 0.018f : 0.009f;
    }

    Material Material(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader == null) throw new InvalidOperationException("Replay requires an unlit shader.");
        var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        m_Materials.Add(mat); return mat;
    }

    public void Prepare(TrialReplayTimeline timeline, double time, bool forceVisible = false)
    {
        timeline.Seek(time); var state = timeline.State;
        if (state.Trial != null && m_Trial != state.Trial.trialId)
        {
            foreach (var obj in m_Objects.Values) UnityEngine.Object.DestroyImmediate(obj);
            m_Objects.Clear();
            // Keep the six overlay materials when replacing a trial.
            for (int i = m_Materials.Count - 1; i > 5; i--) { UnityEngine.Object.DestroyImmediate(m_Materials[i]); m_Materials.RemoveAt(i); }
            m_Trial = state.Trial.trialId;
            foreach (var o in state.Trial.objects)
            {
                var go = new GameObject(o.id); go.transform.SetParent(m_Root.transform);
                go.AddComponent<MeshFilter>().sharedMesh = m_Meshes[o.mesh];
                go.AddComponent<MeshRenderer>().sharedMaterial = Material(o.rgba);
                m_Objects.Add(o.id, go);
            }
        }
        foreach (var pair in m_Objects)
        {
            var go = pair.Value; go.SetActive((state.Visible || forceVisible) && state.Trial != null);
            if (!state.Transforms.TryGetValue(pair.Key, out var t)) continue;
            go.transform.SetPositionAndRotation(t.position, t.rotation); go.transform.localScale = t.scale;
        }
        if (state.Trial != null) foreach (var o in state.Trial.objects)
        {
            Color color = o.rgba;
            if (!forceVisible)
            {
                if (ShowTarget && o.target) color = Color.green;
                if (state.Sample != null && state.Sample.hoveredId == o.id) color = Color.Lerp(color, new Color(1, 0.6f, 0.05f), 0.65f);
                if (ShowControllers && state.Sample != null &&
                    (state.Sample.leftController?.hoveredId == o.id || state.Sample.rightController?.hoveredId == o.id))
                    color = Color.Lerp(color, Color.cyan, 0.7f);
                if (state.Selection != null && state.Selection.objectId == o.id && time - state.Selection.time <= 0.3) color = Color.white;
            }
            m_Objects[o.id].GetComponent<Renderer>().sharedMaterial.color = color;
        }
        var sample = state.Sample;
        var readiness = state.Readiness;
        bool showCross = readiness != null && readiness.readinessActive && !readiness.readinessPaused;
        DrawCrossLine(m_CrossHorizontal, readiness, showCross, new Vector3(-0.1f, 0, 0), new Vector3(0.1f, 0, 0));
        DrawCrossLine(m_CrossVertical, readiness, showCross, new Vector3(0, -0.1f, 0), new Vector3(0, 0.1f, 0));
        DrawCrossLine(m_CrossCharge, readiness, showCross && readiness.readinessProgress > 0,
            new Vector3(-0.2f, -0.18f, 0), new Vector3(-0.2f + 0.4f * (readiness?.readinessProgress ?? 0), -0.18f, 0));
        DrawController(m_LeftRay, forceVisible ? null : sample?.leftController);
        DrawController(m_RightRay, forceVisible ? null : sample?.rightController);
        if (RecordedHead && sample != null && sample.headAvailable)
            Camera.transform.SetPositionAndRotation(sample.headPosition, sample.headRotation);
        else
        {
            var rotation = Quaternion.Euler(Overhead ? 89 : Pitch, Yaw, 0);
            Camera.transform.SetPositionAndRotation(rotation * new Vector3(0, 0, -Distance), rotation);
        }
        m_Ray.enabled = !forceVisible && ShowGaze && (state.Visible || showCross) && sample != null && sample.gazeAvailable && sample.gazeTracked != 0;
        if (m_Ray.enabled)
        {
            m_Ray.SetPosition(0, sample.gazeOrigin);
            float length = 3;
            if (!string.IsNullOrEmpty(sample.hoveredId) && state.Transforms.TryGetValue(sample.hoveredId, out var target))
                length = Vector3.Distance(sample.gazeOrigin, target.position);
            m_Ray.SetPosition(1, sample.gazeOrigin + sample.gazeDirection.normalized * length);
        }
    }

    static void DrawCrossLine(LineRenderer line, TrialReplayRecord gate, bool visible, Vector3 a, Vector3 b)
    {
        line.enabled = visible;
        if (!visible) return;
        line.startWidth = line.endWidth = 0.025f;
        line.SetPosition(0, gate.readinessPosition + gate.readinessRotation * a);
        line.SetPosition(1, gate.readinessPosition + gate.readinessRotation * b);
    }

    public Texture Render(Rect rect)
    {
        m_Preview.BeginPreview(rect, GUIStyle.none);
        m_Preview.Render(true);
        return m_Preview.EndPreview();
    }

    public Texture2D Capture(int width, int height)
    {
        float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
        var texture = Render(new Rect(0, 0, width / pixelsPerPoint, height / pixelsPerPoint));
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = (RenderTexture)texture;
            var copy = new Texture2D(width, height, TextureFormat.RGB24, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0); copy.Apply(); return copy;
        }
        finally { RenderTexture.active = previous; }
    }

    public void Dispose()
    {
        m_Preview.Cleanup();
        foreach (var mesh in m_Meshes.Values) UnityEngine.Object.DestroyImmediate(mesh);
        foreach (var mat in m_Materials) UnityEngine.Object.DestroyImmediate(mat);
        m_Meshes.Clear(); m_Materials.Clear(); m_Objects.Clear();
    }
}
