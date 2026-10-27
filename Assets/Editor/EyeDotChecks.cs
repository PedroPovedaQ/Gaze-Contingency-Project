using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EyeDotChecks
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    static void Call(object target, string method) => target.GetType().GetMethod(method, Flags).Invoke(target, null);

    [MenuItem("Tools/Codex/Check Eye Dot")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var host = new GameObject("Eye dot checks"); host.SetActive(false);
        EyeDot dot = null;
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            var center = new Vector3(4, .2f, -7);
            var facing = Quaternion.Euler(0, 27, 0);
            Set(game, "m_SpawnCenter", center); Set(game, "m_SeatedRotation", facing);
            Set(game, "m_FloorHeight", .2f);
            Vector3 origin = center + Vector3.up * 1.2f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = facing * Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
                Check(game.TryEyeDotPoint(origin, direction, out var point, out int plane) && plane == i,
                    "Translated/rotated layout selects the correct plane " + i);
                Check(Vector3.Distance(point, origin + direction * 1.5f) < .0001f,
                    "Dot uses object depth, not rear outline depth");
                game.SetDegreeGuidesVisible(false);
                Check(game.TryEyeDotPoint(origin, direction, out var hiddenPoint, out _) && hiddenPoint == point,
                    "Hidden wall outlines cannot disable eye dot geometry");
            }
            Check(!game.TryEyeDotPoint(origin, Vector3.up, out _, out _), "No dot above bounded walls");
            Check(!game.TryEyeDotPoint(origin, Vector3.down, out _, out _), "No dot on floor");
            Check(!game.TryEyeDotPoint(origin, Vector3.zero, out _, out _), "Zero ray rejected");
            Check(!game.TryEyeDotPoint(origin, new Vector3(float.NaN, 0, 1), out _, out _), "Invalid ray rejected");
            Vector3 forward = facing * Vector3.forward;
            Check(!game.TryEyeDotPoint(origin + forward * 4, forward, out _, out _), "Behind-eye planes rejected");
            Set(game, "m_UseRotationalLayout", false);
            Check(!game.TryEyeDotPoint(origin, forward, out _, out _), "Unsupported layout never fabricates a plane");
            dot = host.AddComponent<EyeDot>(); Call(dot, "Awake");
            Check(!dot.Visible && !dot.Drawn, "Off by default");
            Call(dot, "CreateMarker");
            var renderer = (MeshRenderer)typeof(EyeDot).GetField("m_Renderer", Flags).GetValue(dot);
            Check(renderer != null && renderer.sharedMaterial.shader.isSupported, "Packaged shader is supported");
            Check(renderer.GetComponent<Collider>() == null && renderer.gameObject.layer == 2,
                "Marker never intercepts selection");
            dot.SetVisible(true); renderer.enabled = true; Call(dot, "LateUpdate");
            Check(!dot.Drawn, "No active search/valid stream hides the marker");
            renderer.enabled = true; dot.SetVisible(false);
            Check(!dot.Visible && !dot.Drawn && dot.PlaneId == -1, "Switch off hides immediately");
            dot.SetVisible(true);
            var sample = new GazeObservation(); SharedGazeStream.FillTrialContext(sample, game);
            Check(sample.eyeDotEnabled, "Enabled feedback is recorded with eye observations");
            dot.SetVisible(false); SharedGazeStream.FillTrialContext(sample, game);
            Check(!sample.eyeDotEnabled, "Disabled feedback is recorded too");
            Debug.Log("[EyeDotChecks] PASS: eight planes, bounds, transformed layout, hidden walls, invalid rays, default/toggle, shader, no collider, unavailable stream and telemetry.");
        }
        finally
        {
            // Edit-mode fixtures own their generated resources; avoid deferred Destroy.
            if (dot != null)
            {
                foreach (string field in new[] { "m_Material", "m_Mesh" })
                {
                    UnityEngine.Object.DestroyImmediate((UnityEngine.Object)typeof(EyeDot).GetField(field, Flags).GetValue(dot));
                    Set(dot, field, null);
                }
                Set(dot, "m_Renderer", null);
            }
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
