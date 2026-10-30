using System;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Editor-only study geometry illustration; never starts a participant session.</summary>
public static class ThesisRoomCapture
{
    const int Width = 2560, Height = 1440, LayoutSeed = 42000;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    static object Call(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlaying ||
            SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Use GPU batch mode outside Play mode; omit -nographics.");
        string output = Environment.GetEnvironmentVariable("THESIS_ROOM_CAPTURE_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set THESIS_ROOM_CAPTURE_OUTPUT.");
        Directory.CreateDirectory(output);
        var receipt = new StringBuilder();
        receipt.AppendLine($"Unity {Application.unityVersion}; GPU {SystemInfo.graphicsDeviceName}; {Width}x{Height}; 4x MSAA");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GazeContingencyStudyScene.unity", OpenSceneMode.Single);
        var sceneCamera = Camera.main;
        if (sceneCamera == null) throw new InvalidOperationException("Study camera missing.");
        // Resolve the same first active InteractablePrimitive material the runtime factory uses.
        Material material = null;
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.InstanceID))
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name.Contains("InteractablePrimitive"))
            { material = renderer.sharedMaterial; break; }
        // Like EnsureInitialized, use the real shader's defaults when no active scene instance exists.
        if (material == null)
        {
            var shader = Shader.Find("Shader Graphs/InteractablePrimitive");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Study object shader missing.");
            material = new Material(shader);
        }
        receipt.AppendLine($"Object material: {AssetDatabase.GetAssetPath(material)}; shader={material.shader.name}");
        var camera = new GameObject("Thesis room camera", typeof(Camera)).GetComponent<Camera>();
        camera.CopyFrom(sceneCamera);
        foreach (var root in scene.GetRootGameObjects())
            if (root != camera.gameObject && root.name != "Environment" && root.name != "Lighting" && root.name != "Directional Light")
                root.SetActive(false);
        camera.tag = "MainCamera";
        camera.enabled = true;
        camera.aspect = (float)Width / Height;
        camera.cullingMask = ~0;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 100;

        var host = new GameObject("Editor-only room fixture");
        host.SetActive(false); // No lifecycle methods, services, loggers or XR input.
        var game = host.AddComponent<FindObjectGameManager>();
        var factory = host.AddComponent<ShapeObjectFactory>();
        try
        {
            // Use factory meshes, avoiding its Play-mode-only deferred primitive destruction.
            var meshes = new[] { PrimitiveMesh(PrimitiveType.Sphere), PrimitiveMesh(PrimitiveType.Cube),
                FactoryMesh("CreatePyramidMesh"), PrimitiveMesh(PrimitiveType.Cylinder),
                FactoryMesh("CreateStarMesh"), PrimitiveMesh(PrimitiveType.Capsule) };
            Set(factory, "m_ShapeMeshes", meshes);
            Set(factory, "m_ShapeNames", new[] { "Sphere", "Cube", "Pyramid", "Cylinder", "Star", "Capsule" });
            Set(factory, "m_BaseMaterial", material);
            Set(game, "m_SpawnCenter", new Vector3(0, 1.2f, 0));
            Set(game, "m_FloorHeight", 0f);
            Call(game, "CreateRotationalFrames");
            // Suppress angle text for the requested clean figure; retain real frame geometry/material.
            foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) label.gameObject.SetActive(false);

            SessionConfig.ParticipantId = "THESIS_ROOM"; // In-memory identity generation only; no StartRun.
            var round = ChallengeSet.Rounds[0];
            float radius = RotationalSearchLayout.DefaultRadius;
            float angle = round.startPlane * Mathf.Deg2Rad * RotationalSearchLayout.PlaneAngleDegrees;
            float targetHeight = Mathf.Lerp(RotationalSearchLayout.DefaultMinHeight,
                RotationalSearchLayout.DefaultMaxHeight, round.targetHeightFraction);
            if (!RotationalSearchLayout.TryTarget(radius, 0, 0, radius * Mathf.Sin(angle), radius * Mathf.Cos(angle),
                round.SignedTheta, round.targetPlane, round.targetSlot, targetHeight, out var target))
                throw new InvalidOperationException("Synthetic centered readiness geometry failed.");
            var slots = RotationalSearchLayout.Build(radius, seed: LayoutSeed, pinnedTarget: target);
            if (slots.Length != round.objects.Length || slots.Length != 168)
                throw new InvalidOperationException("Study count changed; review capture provenance.");
            for (int i = 0; i < slots.Length; i++)
            {
                var definition = round.objects[i];
                factory.EnqueueCombo(definition.shape, definition.color);
                var obj = new GameObject($"Room object {i}");
                factory.ConfigureObject(obj);
                var slot = slots[i];
                obj.transform.SetPositionAndRotation(new Vector3(slot.x, slot.y, slot.z), Quaternion.Euler(0, slot.azimuth + 180, 0));
                if (definition.shape == "Pyramid") obj.transform.position -= Vector3.up * obj.transform.localScale.y * .5f;
                var renderer = obj.GetComponent<MeshRenderer>();
                if (renderer.sharedMaterial == null || !renderer.sharedMaterial.shader.isSupported)
                    throw new InvalidOperationException("Missing or unsupported object material.");
                receipt.AppendLine($"object {i}: {definition.color} {definition.shape}; plane={slot.plane}; slot={slot.slot}; position={obj.transform.position.ToString("F6")}; scale={obj.transform.localScale.ToString("F6")}");
            }
            receipt.AppendLine($"Count={slots.Length}; layout={RotationalSearchLayout.LayoutTag}; layoutSeed={LayoutSeed}; identity=THESIS_ROOM; scheduleSeed={ChallengeSet.ScheduleSeed}; round=0");
            receipt.AppendLine($"Radius={radius:R}; wall height={game.SearchPlaneHeight:R}; floor=0; seated origin=(0,1.2,0); yaw=0; pin={target.plane}/{target.slot}");
            Capture(camera, output, "room-first-person.png", new Vector3(0, 1.2f, 0), new Vector3(0, 1.2f, 1.5f), 80, receipt);
            Capture(camera, output, "room-wide.png", new Vector3(4, 5, -5), new Vector3(0, 1.05f, 0), 34, receipt);
            File.WriteAllText(Path.Combine(output, "render-receipt.txt"), receipt.ToString());
            Debug.Log("[ThesisRoomCapture] PASS: 168 objects, eight runtime frames, two GPU renders; no Play mode or services.");
        }
        finally
        {
            // Runtime OnDestroy uses deferred destruction; release its material in edit mode first.
            var outline = (Material)typeof(FindObjectGameManager).GetField("m_PlaneOutlineMaterial", Private).GetValue(game);
            Set(game, "m_PlaneOutlineMaterial", null);
            if (outline != null) Object.DestroyImmediate(outline);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static Mesh FactoryMesh(string method) => (Mesh)typeof(ShapeObjectFactory)
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var primitive = GameObject.CreatePrimitive(type);
        var mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive);
        return mesh;
    }

    static void Capture(Camera camera, string output, string name, Vector3 position, Vector3 lookAt, float fov, StringBuilder receipt)
    {
        camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        camera.fieldOfView = fov;
        var target = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
        var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            receipt.AppendLine($"{name}: position={position}; lookAt={lookAt}; rotation={camera.transform.eulerAngles}; vertical FOV={fov}");
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }
    }
}
