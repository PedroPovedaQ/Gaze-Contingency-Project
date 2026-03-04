# Gaze Contingency Project — Functionality Guide

This guide explains how every piece of the VR "Find the Object" game works, from plane detection to eye gaze tracking to the AI voice assistant. It is written for someone new to Unity and assumes no prior knowledge of XR development. Every section includes full code walkthroughs with line-by-line explanations.

---

## Table of Contents

1. [How Unity Works (Quick Primer)](#1-how-unity-works-quick-primer)
2. [Project Architecture Overview](#2-project-architecture-overview)
3. [Auto-Attach Pattern](#3-auto-attach-pattern)
4. [Plane Detection — How the Table Is Found](#4-plane-detection--how-the-table-is-found)
5. [Object Spawning — What Happens When You Tap](#5-object-spawning--what-happens-when-you-tap)
6. [Shape and Color Assignment (Full Code Walkthrough)](#6-shape-and-color-assignment-full-code-walkthrough)
7. [The Find the Object Game Mode (Full Code Walkthrough)](#7-the-find-the-object-game-mode-full-code-walkthrough)
8. [Heads-Up Display — HUD (Full Code Walkthrough)](#8-heads-up-display--hud-full-code-walkthrough)
9. [Eye Gaze Tracking](#9-eye-gaze-tracking)
10. [Gaze Highlighting (Full Code Walkthrough)](#10-gaze-highlighting-full-code-walkthrough)
11. [Controller Highlighting (Full Code Walkthrough)](#11-controller-highlighting-full-code-walkthrough)
12. [Eye Gaze Ray Visual (Full Code Walkthrough)](#12-eye-gaze-ray-visual-full-code-walkthrough)
13. [Gaze Data Logging (Full Code Walkthrough)](#13-gaze-data-logging-full-code-walkthrough)
14. [Object Metadata — SpawnableObjectInfo](#14-object-metadata--spawnableobjectinfo)
15. [The AI Voice Assistant](#15-the-ai-voice-assistant)
16. [Voice Assistant Controller (Full Code Walkthrough)](#16-voice-assistant-controller-full-code-walkthrough)
17. [Agent Context — Scene Knowledge Builder (Full Code Walkthrough)](#17-agent-context--scene-knowledge-builder-full-code-walkthrough)
18. [Hint Generator — OpenAI Integration (Full Code Walkthrough)](#18-hint-generator--openai-integration-full-code-walkthrough)
19. [Voice Synthesizer — ElevenLabs TTS (Full Code Walkthrough)](#19-voice-synthesizer--elevenlabs-tts-full-code-walkthrough)
20. [Feature Toggling (Hand Menu)](#20-feature-toggling-hand-menu)
21. [File Reference](#21-file-reference)
22. [Data Flow Diagrams](#22-data-flow-diagrams)

---

## 1. How Unity Works (Quick Primer)

Unity is a game engine where everything in your scene is a **GameObject**. Each GameObject has **Components** attached to it that define its behavior. For example:

- A `MeshRenderer` component makes an object visible.
- A `Rigidbody` component gives it physics (gravity, collisions).
- A `MonoBehaviour` script (written in C#) adds custom logic.

### Key Lifecycle Methods

Every C# script that inherits from `MonoBehaviour` can use these methods, which Unity calls automatically:

| Method | When It Runs |
|--------|-------------|
| `Awake()` | Once, when the component is first created |
| `OnEnable()` | Each time the component is enabled |
| `Start()` | Once, on the first frame after `Awake()` |
| `Update()` | Every frame (~72 fps in VR) |
| `LateUpdate()` | Every frame, after all `Update()` calls finish |
| `OnDisable()` | Each time the component is disabled |
| `OnDestroy()` | When the GameObject is destroyed |

### Coroutines

Coroutines are Unity's way of doing asynchronous work (like waiting for an API response) without freezing the game. They use `yield return` to pause and resume across frames:

```csharp
IEnumerator DoSomethingAsync()
{
    Debug.Log("Starting...");
    yield return new WaitForSeconds(2f);  // pause for 2 seconds — game keeps running
    Debug.Log("Done!");                    // resumes here after 2 seconds
}
```

You start a coroutine with `StartCoroutine(DoSomethingAsync())`. Unlike a normal method call, the coroutine doesn't block — it runs a little bit each frame, yielding control back to Unity in between.

### Events and Delegates

Scripts communicate through **events** — one script fires an event, and other scripts that subscribed to it get notified:

```csharp
// ---- In the PUBLISHER script (the one that knows when something happened) ----
public event System.Action<int> OnScoreChanged;  // declares "I can notify about score changes"

// Somewhere inside a method:
OnScoreChanged?.Invoke(newScore);  // fire the event — all subscribers get called
// The ?. means "only fire if someone is listening" (avoids a crash if nobody subscribed)

// ---- In the SUBSCRIBER script (the one that wants to react) ----
scoreManager.OnScoreChanged += HandleScoreChanged;  // subscribe: "call my method when it fires"

void HandleScoreChanged(int score)
{
    Debug.Log($"Score is now {score}");  // this runs whenever the publisher fires the event
}

// To stop listening (important for cleanup):
scoreManager.OnScoreChanged -= HandleScoreChanged;  // unsubscribe
```

### Important C# Concepts Used in This Project

**`GetComponent<T>()`** — finds a component of type T on the same GameObject:
```csharp
var rb = gameObject.GetComponent<Rigidbody>();  // find the Rigidbody on this object
rb.useGravity = true;                           // modify it
```

**`FindObjectOfType<T>()`** — searches the ENTIRE scene for the first component of type T:
```csharp
var spawner = FindObjectOfType<ObjectSpawner>();  // find the one ObjectSpawner in the scene
```

**`AddComponent<T>()`** — adds a new component to a GameObject at runtime:
```csharp
var audioSource = gameObject.AddComponent<AudioSource>();  // add audio capability
audioSource.volume = 0.7f;
```

**`[SerializeField]`** — makes a private field visible in the Unity Inspector (the UI panel):
```csharp
[SerializeField] Material m_BaseMaterial;  // shows up in Inspector, but can't be accessed from other scripts
```

**`Debug.Log()`** — prints messages to the Unity console (like `console.log` in JavaScript):
```csharp
Debug.Log("Hello from Unity!");           // normal message
Debug.LogWarning("Something is wrong");   // yellow warning
Debug.LogError("Critical failure!");      // red error
```

---

## 2. Project Architecture Overview

The entire system is built around a single pre-existing component called **ObjectSpawner** (from Unity's XR Interaction Toolkit starter assets). All custom scripts auto-attach themselves to the ObjectSpawner's GameObject at scene load, forming a component stack:

```
ObjectSpawner (pre-existing, from XRI Starter Assets)
│
├── ShapeObjectFactory        — assigns shapes, colors, and physics
├── FindObjectGameManager     — runs the game logic
├── FindObjectUI              — shows the HUD
└── VoiceAssistantController  — AI voice assistant
    ├── AgentContext           — builds scene descriptions for the LLM
    ├── HintGenerator          — calls OpenAI for hints
    └── VoiceSynthesizer       — calls ElevenLabs for speech

Gaze Interactor (on the XR Gaze Interactor GameObject)
├── GazeHighlightManager      — orange glow on gazed objects
├── GazeDataLogger            — CSV telemetry logging
└── EyeGazeRayVisual          — orange ray visualization
```

### Why This Architecture?

- **Zero manual setup**: No dragging components in the Inspector. Everything auto-attaches at runtime using `[RuntimeInitializeOnLoadMethod]`.
- **Event-driven**: Components communicate through C# events, not direct references. This means you can remove the voice assistant and the game still works perfectly.
- **Graceful degradation**: If the OpenAI or ElevenLabs API fails, the game continues silently — no crashes, just log warnings.

---

## 3. Auto-Attach Pattern

Every major script uses this pattern to attach itself to the right GameObject at scene load:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void AutoAttach()
{
    // Step 1: Search the entire scene for an ObjectSpawner component
    var spawner = FindObjectOfType<ObjectSpawner>();

    // Step 2: Only attach if the spawner exists AND we're not already attached
    if (spawner != null && spawner.GetComponent<MyScript>() == null)
    {
        // Step 3: Add ourselves as a component on the spawner's GameObject
        spawner.gameObject.AddComponent<MyScript>();
        Debug.Log("[MyScript] Auto-attached");
    }
}
```

**How it works:**
1. The `[RuntimeInitializeOnLoadMethod]` attribute tells Unity: "Call this static method automatically after the scene finishes loading."
2. `AfterSceneLoad` means all GameObjects and their components are already in the scene, so `FindObjectOfType` will find them.
3. The `static` keyword means this method belongs to the class itself, not to an instance — it runs before any instance of this script exists.
4. The null check (`spawner.GetComponent<MyScript>() == null`) prevents adding duplicates if the scene is reloaded.

This means you never need to manually add components in the Unity Inspector — just write the script and it wires itself up.

---

## 4. Plane Detection — How the Table Is Found

Before any game objects can be spawned, the system needs to detect real-world surfaces (tables, floors, walls). This project uses **VIVE's native plane detection** instead of Unity's AR Foundation, because it is faster and more reliable on the VIVE Focus Vision headset.

### VivePlaneData.cs — Full Code Walkthrough

This is a lightweight data component. It's attached to each detected plane GameObject and stores metadata about that plane.

```csharp
using UnityEngine;
using static VIVE.OpenXR.PlaneDetection.VivePlaneDetection;
// "using static" lets us use VIVE's enum types directly without the long prefix

public class VivePlaneData : MonoBehaviour
{
    // These properties are set once by VivePlaneProvider when the plane is created.
    // They use C# auto-properties (get/set) — like public fields but with controlled access.
    public ulong PlaneId { get; set; }                              // unique ID from VIVE
    public Vector2 Size { get; set; }                                // width x height in meters
    public XrPlaneDetectorOrientationEXT Orientation { get; set; }   // horizontal/vertical
    public XrPlaneDetectorSemanticTypeEXT SemanticType { get; set; } // table/floor/wall/etc.

    // These are DYNAMIC — they read from the transform every time you access them.
    // This means if the plane moves (unlikely but possible), these stay accurate.
    public Vector3 Center => transform.position;   // world-space center
    public Vector3 Normal => transform.forward;    // direction the plane faces

    // Quick check: is this a table or floor (horizontal, facing up)?
    public bool IsHorizontalUp =>
        Orientation == XrPlaneDetectorOrientationEXT.HORIZONTAL_UPWARD_EXT;
}
```

**Key Unity concept: `transform`**. Every GameObject has a `transform` component that stores its position, rotation, and scale in the 3D world. When we write `transform.position`, we're asking "where is this plane in the world?"

### VivePlaneProvider.cs — Full Code Walkthrough

This script bridges VIVE's proprietary plane detection API with our game system.

```csharp
public class VivePlaneProvider : MonoBehaviour
{
    [SerializeField] Material m_PlaneMaterial;  // visual material for plane surfaces
    [SerializeField] int m_PlaneLayer = 7;      // physics layer (for raycasting)

    readonly List<GameObject> m_DetectedPlanes = new List<GameObject>();
    bool m_DetectionComplete;

    public bool HasPlanes => m_DetectedPlanes.Count > 0;
    public IReadOnlyList<GameObject> DetectedPlanes => m_DetectedPlanes;
```

**`[SerializeField]`** makes these fields configurable in Unity's Inspector panel. `m_PlaneMaterial` is the semi-transparent material applied to detected surfaces so you can see them. `m_PlaneLayer` is set to 7 ("Placeable Surface") so the spawn system knows which surfaces accept object placement.

The detection runs as a coroutine:

```csharp
    IEnumerator DetectPlanes()
    {
        yield return new WaitForSeconds(0.5f);  // brief pause for OpenXR to be ready

        // Step 1: Check if the headset supports plane detection
        if (!PlaneDetectionManager.IsSupported())
        {
            Debug.LogError("VivePlaneProvider: PlaneDetection feature not supported");
            yield break;  // "yield break" exits a coroutine (like "return" for normal methods)
        }

        // Step 2: Create a detector and start scanning
        var pd = PlaneDetectionManager.CreatePlaneDetector();
        pd.BeginPlaneDetection();
        yield return null;  // wait one frame for detection to start

        // Step 3: Poll until detection is complete (or timeout after 5 seconds)
        var state = pd.GetPlaneDetectionState();
        float timeout = Time.unscaledTime + 5f;
        while (state == XrPlaneDetectionStateEXT.PENDING_EXT)
        {
            if (Time.unscaledTime > timeout) { /* handle timeout */ yield break; }
            yield return null;  // wait one frame, then check again
            state = pd.GetPlaneDetectionState();
        }

        // Step 4: Get the detected planes
        pd.GetPlaneDetections(out var locations);

        // Step 5: Create a Unity GameObject for each detected plane
        foreach (var location in locations)
        {
            var planeGO = CreatePlaneGameObject(location);
            m_DetectedPlanes.Add(planeGO);
        }

        PlaneDetectionManager.DestroyPlaneDetector(pd);  // cleanup
        m_DetectionComplete = true;
    }
```

For each detected plane, a GameObject is created with visible mesh and physics collider:

```csharp
    GameObject CreatePlaneGameObject(PlaneDetectorLocation location)
    {
        // Create a new empty GameObject named like "VivePlane_TABLE_12345"
        var go = new GameObject($"VivePlane_{location.semanticType}_{location.planeId}");
        go.layer = m_PlaneLayer;  // assign to "Placeable Surface" physics layer

        // Position and rotate it to match the real-world surface
        go.transform.position = location.pose.position;
        go.transform.rotation = location.pose.rotation;

        // Create a flat rectangular mesh matching the plane's real size
        var mesh = CreateRectMesh(location.size.x, location.size.y);

        // MeshFilter holds the geometry, MeshRenderer makes it visible
        go.AddComponent<MeshFilter>().mesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.material = m_PlaneMaterial;  // semi-transparent visualization

        // MeshCollider makes the plane "solid" so raycasts can hit it
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        // Attach our metadata component
        var planeData = go.AddComponent<VivePlaneData>();
        planeData.PlaneId = location.planeId;
        planeData.Size = new Vector2(location.size.x, location.size.y);
        planeData.Orientation = location.orientation;
        planeData.SemanticType = location.semanticType;

        return go;
    }
```

The rectangular mesh is a simple quad (4 vertices, 2 triangles):

```csharp
    static Mesh CreateRectMesh(float width, float height)
    {
        float hx = width / 2f;   // half-width
        float hy = height / 2f;  // half-height

        var mesh = new Mesh { name = "VivePlaneMesh" };

        // 4 corners of a rectangle, centered at origin, lying flat in the XY plane
        mesh.vertices = new[]
        {
            new Vector3(-hx, -hy, 0),  // bottom-left
            new Vector3( hx, -hy, 0),  // bottom-right
            new Vector3(-hx,  hy, 0),  // top-left
            new Vector3( hx,  hy, 0),  // top-right
        };

        // 2 triangles forming the rectangle (indices into the vertex array)
        // Triangle 1: bottom-left → top-right → top-left
        // Triangle 2: bottom-left → bottom-right → top-right
        mesh.triangles = new[] { 0, 3, 2, 0, 1, 3 };

        // UV coordinates for texture mapping (0,0 = bottom-left, 1,1 = top-right)
        mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };

        mesh.RecalculateNormals();   // compute which direction each face points
        mesh.RecalculateBounds();    // compute bounding box for culling
        return mesh;
    }
```

---

## 5. Object Spawning — What Happens When You Tap

The spawn flow starts when you point your controller at a detected plane and tap:

```
User taps plane
    → ARContactSpawnTrigger fires UnityEvent
        → ObjectSpawner.TrySpawnObject(position, normal)
            → Instantiates a prefab at that position
            → Fires objectSpawned event
                → ShapeObjectFactory.OnObjectSpawned() — assigns shape/color
                → FindObjectGameManager.OnObjectSpawned() — game logic
```

### ObjectSpawner (Pre-existing)

This is part of Unity's XR Interaction Toolkit sample. It:
- Instantiates a random prefab from its list at the given position
- Fires `objectSpawned` (an `Action<GameObject>` event) so other scripts can post-process the spawned object

### ARContactSpawnTrigger (Modified)

Detects when the controller touches a plane surface. Originally worked only with AR Foundation planes; modified to also work with our `VivePlaneData` planes.

---

## 6. Shape and Color Assignment (Full Code Walkthrough)

### ShapeObjectFactory.cs

Every spawned object starts as a generic prefab (a cylinder from the starter assets). ShapeObjectFactory transforms it into one of 12 possible shape-color combinations.

**Available shapes:** Sphere, Cube, Pyramid
**Available colors:** Red, Blue, Yellow, Purple

#### Color Definitions

```csharp
// These colors are shared across the entire project.
// "static readonly" means there's one copy for all instances and it never changes.
static readonly (Color color, string name)[] k_Colors =
{
    (new Color(0.9f, 0.15f, 0.15f, 1f), "Red"),     // RGBA, 1f = fully opaque
    (new Color(0.15f, 0.35f, 0.9f, 1f), "Blue"),
    (new Color(0.95f, 0.85f, 0.1f, 1f), "Yellow"),
    (new Color(0.6f, 0.15f, 0.85f, 1f), "Purple"),
};
```

#### The Combo Queue System

The game manager can pre-specify what shape and color the next spawned object should be:

```csharp
// Queue of (shapeIndex, colorIndex) pairs. When non-empty, spawns use these instead of random.
readonly Queue<(int shapeIdx, int colorIdx)> m_ComboQueue = new();

public void EnqueueCombo(string shapeName, string colorName)
{
    // Look up the index of the shape name in our array (e.g., "Sphere" → 0)
    int si = System.Array.IndexOf(m_ShapeNames, shapeName);
    // Look up the color name using a predicate function
    int ci = System.Array.FindIndex(k_Colors, c => c.name == colorName);

    if (si >= 0 && ci >= 0)
        m_ComboQueue.Enqueue((si, ci));  // add to the back of the queue
    else
        Debug.LogWarning($"Unknown combo: {shapeName}/{colorName}");
}
```

#### Auto-Initialization (Creating Meshes from Primitives)

If no meshes are assigned in the Inspector, the factory creates them from Unity's built-in primitives:

```csharp
void EnsureInitialized()
{
    if (m_ShapeMeshes == null || m_ShapeMeshes.Length == 0)
    {
        m_ShapeMeshes = new Mesh[3];
        m_ShapeNames = new[] { "Sphere", "Cube", "Pyramid" };

        // Create a temporary sphere, steal its mesh, then destroy the temp object
        var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m_ShapeMeshes[0] = tempSphere.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempSphere);  // we only needed the mesh data

        // Same for cube
        var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m_ShapeMeshes[1] = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempCube);

        // Pyramid has no built-in primitive, so we create one procedurally
        m_ShapeMeshes[2] = CreatePyramidMesh();
    }

    // Auto-find or create the material for coloring objects
    if (m_BaseMaterial == null)
    {
        // Search the scene for any renderer using the InteractablePrimitive shader
        var renderers = FindObjectsOfType<MeshRenderer>();
        foreach (var r in renderers)
        {
            if (r.sharedMaterial?.shader?.name.Contains("InteractablePrimitive") == true)
            {
                m_BaseMaterial = r.sharedMaterial;
                break;
            }
        }
        // Fallback: create a new material from the shader
        if (m_BaseMaterial == null)
        {
            var shader = Shader.Find("Shader Graphs/InteractablePrimitive");
            m_BaseMaterial = shader != null
                ? new Material(shader)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
    }
}
```

#### The Main Spawn Handler — OnObjectSpawned()

This is the most important method. It transforms a generic prefab into a colored shape:

```csharp
void OnObjectSpawned(GameObject obj)
{
    // 1. Remove the spinning animation that comes with the starter prefab
    var anim = obj.GetComponent<Animation>();
    if (anim != null) Destroy(anim);

    // 2. Hide child renderers (rings, crosses, etc. from the XR starter kit)
    //    We DON'T destroy them because the affordance system needs the child GameObjects
    //    alive — it tracks them for visual feedback. We just make them invisible.
    HideChildRenderers(obj);

    // 3. Pick shape and color
    int shapeIdx, colorIdx;
    if (m_ComboQueue.Count > 0)
    {
        // Game mode: use the pre-specified combo
        var combo = m_ComboQueue.Dequeue();  // take from front of queue
        shapeIdx = combo.shapeIdx;
        colorIdx = combo.colorIdx;
    }
    else
    {
        // Free play: pick randomly
        shapeIdx = Random.Range(0, m_ShapeMeshes.Length);  // 0, 1, or 2
        colorIdx = Random.Range(0, k_Colors.Length);       // 0, 1, 2, or 3
    }

    // 4. Swap the mesh on the object
    var meshFilter = obj.GetComponent<MeshFilter>();
    if (meshFilter == null)
        meshFilter = obj.AddComponent<MeshFilter>();
    meshFilter.sharedMesh = m_ShapeMeshes[shapeIdx];  // replace geometry

    // 5. Replace the collider with one that matches the new shape
    ReplaceCollider(obj, m_ShapeMeshes[shapeIdx], shapeIdx);

    // 6. Create a PER-INSTANCE material (each object gets its own copy)
    //    This is important because materials are shared by default — if we
    //    changed the shared material, ALL objects would change color.
    var (color, colorName) = k_Colors[colorIdx];
    var renderer = obj.GetComponent<MeshRenderer>();
    var mat = new Material(m_BaseMaterial);     // create a copy
    mat.SetColor("_BaseColor", color);          // set the URP base color
    mat.SetColor("_Color", color);              // set the legacy color (compatibility)
    mat.SetFloat("_EdgeHighlightFalloff", 1.5f); // configure highlight shader
    renderer.material = mat;                    // assign the copy to this object only

    // 7. Add physics (Rigidbody) so the object falls with gravity
    var rb = obj.GetComponent<Rigidbody>();
    if (rb == null)
        rb = obj.AddComponent<Rigidbody>();
    rb.useGravity = true;     // affected by gravity
    rb.isKinematic = false;   // not kinematic = physics engine moves it

    // 8. Enable gaze interaction on the XR grab system
    var grab = obj.GetComponent<XRGrabInteractable>();
    if (grab != null)
    {
        grab.allowGazeInteraction = true;   // eye gaze can hover over it
        grab.allowGazeSelect = false;       // but can't select (grab) with gaze

        // Update the collider list — the grab system uses this to detect hits
        grab.colliders.Clear();
        grab.colliders.AddRange(obj.GetComponents<Collider>());

        // CRITICAL: Re-register with the XR Interaction Manager.
        // The manager keeps a map of collider → interactable. When we replaced
        // the collider above, the old mapping became stale. Toggling enabled
        // forces the manager to rebuild its lookup table.
        grab.enabled = false;
        grab.enabled = true;

        HideChildRenderers(obj);  // re-hide because toggling may recreate visuals
    }

    // 9. Add per-object controller highlight (blue glow)
    obj.AddComponent<InteractableHighlight>();

    // 10. Scale to 10cm and raise slightly above the table
    obj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
    var pos = obj.transform.position;
    pos.y += 0.05f;  // 5cm above surface, then gravity drops it
    obj.transform.position = pos;

    // 11. Add metadata component for logging and game logic
    var info = obj.AddComponent<SpawnableObjectInfo>();
    info.shapeName = shapeName;
    info.colorName = colorName;
    obj.name = info.DisplayName;  // rename to "Red_Sphere", etc.

    // 12. Fire event so the game manager can wire grab detection
    objectFullyConfigured?.Invoke(obj);
}
```

#### Collider Replacement

Each shape gets a collider that's 30% oversized for easier gaze targeting:

```csharp
void ReplaceCollider(GameObject obj, Mesh mesh, int shapeIdx)
{
    // Remove all existing colliders on the root object
    foreach (var col in obj.GetComponents<Collider>())
        DestroyImmediate(col);  // DestroyImmediate removes it THIS frame (not next frame)

    switch (shapeIdx)
    {
        case 0: // Sphere — mesh radius is 0.5, collider is 0.65 (30% bigger)
            var sc = obj.AddComponent<SphereCollider>();
            sc.radius = 0.65f;
            break;

        case 1: // Cube — mesh is 1x1x1, collider is 1.3x1.3x1.3
            var bc = obj.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.3f, 1.3f, 1.3f);
            break;

        default: // Pyramid — sphere collider centered slightly above base
            var pc = obj.AddComponent<SphereCollider>();
            pc.center = new Vector3(0f, 0.4f, 0f);  // offset up from base
            pc.radius = 0.65f;
            break;
    }
}
```

#### Procedural Pyramid Mesh

Unity has no built-in pyramid, so we create one manually. A mesh is defined by three arrays:
- **vertices**: the 3D points
- **triangles**: groups of 3 vertex indices defining each face
- **normals**: the direction each vertex faces (for lighting)

```csharp
static Mesh CreatePyramidMesh()
{
    var mesh = new Mesh { name = "Pyramid" };

    float s = 0.5f;  // half-size of the base
    float h = 1f;    // height of the apex

    // We need SEPARATE vertices per face for "flat shading" — where each
    // triangle has a uniform normal. Shared vertices would create smooth shading.
    var vertices = new Vector3[]
    {
        // Base top (visible from above) — 4 corners
        new(-s, 0, -s), new(s, 0, -s), new(s, 0, s), new(-s, 0, s),
        // Base bottom (visible from below — so you can see it when picked up)
        new(-s, 0, -s), new(s, 0, -s), new(s, 0, s), new(-s, 0, s),
        // Front face — 3 vertices: bottom-right, bottom-left, apex
        new(s, 0, -s), new(-s, 0, -s), new(0, h, 0),
        // Right face
        new(s, 0, s), new(s, 0, -s), new(0, h, 0),
        // Back face
        new(-s, 0, s), new(s, 0, s), new(0, h, 0),
        // Left face
        new(-s, 0, -s), new(-s, 0, s), new(0, h, 0),
    };

    // Triangles: groups of 3 indices. The ORDER matters — it determines
    // which side of the triangle faces outward (counter-clockwise = front).
    var triangles = new int[]
    {
        0, 2, 1,  0, 3, 2,     // base top (facing up)
        4, 5, 6,  4, 6, 7,     // base bottom (facing down — reversed winding)
        8, 9, 10,               // front
        11, 12, 13,             // right
        14, 15, 16,             // back
        17, 18, 19,             // left
    };

    // Normals: computed via cross product of two edges of each face.
    // Vector3.Cross(A, B) gives a vector perpendicular to both A and B.
    var frontN = Vector3.Cross(
        vertices[9] - vertices[8],    // edge 1 of the front face
        vertices[10] - vertices[8]    // edge 2 of the front face
    ).normalized;                      // normalize to unit length

    // ... (same for right, back, left faces)

    mesh.vertices = vertices;
    mesh.triangles = triangles;
    mesh.normals = normals;
    mesh.uv = uvs;             // texture coordinates for the shader
    mesh.RecalculateBounds();   // compute bounding box
    return mesh;
}
```

---

## 7. The Find the Object Game Mode (Full Code Walkthrough)

### FindObjectGameManager.cs

This is the main game orchestrator. It manages the state machine, spawns 9 objects, tracks objectives, and handles grab detection.

#### Constants and State

```csharp
public class FindObjectGameManager : MonoBehaviour
{
    const int k_ObjectCount = 9;           // always 9 objects per round
    const float k_Margin = 0.05f;          // 5cm inset from table edges
    const float k_Jitter = 0.02f;          // ±2cm random offset per object
    const float k_SpawnHeightOffset = 0.05f; // 5cm above table, then drops
    const float k_ResetDelay = 5f;         // seconds before resetting after completion

    // State machine: the game can only be in one of these states
    public enum GameState { Idle, Playing, Completed }

    // ---- Events ----
    // Other scripts subscribe to these to react to game events.
    // The voice assistant uses ALL of these.
    public event System.Action OnGameStarted;
    public event System.Action<int> OnObjectFound;            // arg: objective index
    public event System.Action<string, string> OnWrongGrab;   // args: grabbed name, wanted name
    public event System.Action<float> OnGameCompleted;        // arg: elapsed seconds

    // ---- Read-only accessors ----
    // The "=>" syntax is a C# expression-bodied property. It's a read-only shortcut.
    // Writing "public GameState CurrentState => m_State;" is the same as:
    //   public GameState CurrentState { get { return m_State; } }
    public GameState CurrentState => m_State;
    public IReadOnlyList<(string shape, string color, Color colorValue)> Objectives => m_Objectives;
    public IReadOnlyList<GameObject> SpawnedObjects => m_SpawnedObjects;
    public int CurrentObjectiveIndex => m_CurrentObjectiveIndex;
    public int FoundCount => m_FoundCount;

    // ---- Private fields ----
    GameState m_State = GameState.Idle;
    readonly List<(string shape, string color, Color colorValue)> m_Objectives = new();
    readonly List<GameObject> m_SpawnedObjects = new();
    int m_CurrentObjectiveIndex;
    int m_FoundCount;
    int m_ExpectedSpawnCount;  // how many batch spawns we're still waiting for
```

#### Initialization

```csharp
    void OnEnable()
    {
        m_Spawner = GetComponent<ObjectSpawner>();

        // Create the HUD (heads-up display)
        if (m_UI == null)
        {
            m_UI = gameObject.AddComponent<FindObjectUI>();
            m_UI.Initialize();
        }

        // Subscribe to the spawner's event — we'll be notified every time
        // an object is spawned, whether by the player tapping or by our batch spawn.
        m_Spawner.objectSpawned += OnObjectSpawned;
    }
```

#### Spawn Interception — The State Machine Gate

Every spawn goes through this method, which decides what to do based on the current game state:

```csharp
    void OnObjectSpawned(GameObject obj)
    {
        switch (m_State)
        {
            case GameState.Idle:
                // First spawn triggers a new game
                StartGame(obj);
                break;

            case GameState.Playing:
                if (m_ExpectedSpawnCount > 0)
                {
                    // This is one of our batch spawns — let it through
                    m_ExpectedSpawnCount--;
                }
                else
                {
                    // Player tapped during gameplay — destroy the stray spawn
                    Destroy(obj);
                }
                break;

            case GameState.Completed:
                // Block all spawns during the completion screen
                Destroy(obj);
                break;
        }
    }
```

#### Starting a Game

This is the longest method. Here's what happens step by step:

```csharp
    void StartGame(GameObject triggerObj)
    {
        // --- Step 1: Find the nearest table ---
        Vector3 spawnCenter = triggerObj.transform.position;
        var planes = FindObjectsOfType<VivePlaneData>();
        VivePlaneData bestPlane = null;
        float bestDist = float.MaxValue;

        foreach (var p in planes)
        {
            if (!p.IsHorizontalUp) continue;  // skip walls, ceilings
            float dist = Vector3.Distance(spawnCenter, p.Center);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestPlane = p;
            }
        }

        // Use the table's center, size, and orientation for grid layout
        if (bestPlane != null)
        {
            spawnCenter = bestPlane.Center;
            planeSize = bestPlane.Size;
            planeRight = bestPlane.transform.right;      // table's "left-right" axis
            planeForward = bestPlane.transform.up;        // table's "forward-back" axis
        }

        Destroy(triggerObj);  // the trigger object served its purpose

        // --- Step 2: Generate 9 random shape-color combos ---
        // There are 3 shapes × 4 colors = 12 possible combos.
        // We pick 9 of them randomly using a Fisher-Yates shuffle.
        var allCombos = new List<(string shape, string color, Color colorValue)>();
        foreach (var s in m_Factory.ShapeNames)
            foreach (var c in ShapeObjectFactory.Colors)
                allCombos.Add((s, c.name, c.color));

        // Fisher-Yates shuffle: walk backwards through the list, swapping each
        // element with a random earlier element. This gives a uniform random permutation.
        for (int i = allCombos.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (allCombos[i], allCombos[j]) = (allCombos[j], allCombos[i]);  // C# tuple swap
        }

        // Take the first 9
        m_Objectives.Clear();
        for (int i = 0; i < k_ObjectCount; i++)
            m_Objectives.Add(allCombos[i]);

        // Shuffle AGAIN for the find order (so players don't always find
        // objects in the same order they were spawned)
        // ... (same Fisher-Yates shuffle on a copy)

        // --- Step 3: Calculate 3×3 grid positions ---
        float usableW = planeSize.x - k_Margin * 2;  // subtract margins
        float usableH = planeSize.y - k_Margin * 2;
        float cellW = usableW / 3f;  // each cell is 1/3 of the usable area
        float cellH = usableH / 3f;

        var positions = new List<Vector3>();
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                // Center of each cell, with random jitter
                float localX = -usableW / 2f + cellW * (col + 0.5f)
                              + Random.Range(-k_Jitter, k_Jitter);
                float localY = -usableH / 2f + cellH * (row + 0.5f)
                              + Random.Range(-k_Jitter, k_Jitter);

                // Convert from table-local to world coordinates
                Vector3 worldPos = spawnCenter
                    + planeRight * localX      // move along table's left-right
                    + planeForward * localY;   // move along table's forward-back
                worldPos.y = spawnCenter.y + k_SpawnHeightOffset;  // above table

                positions.Add(worldPos);
            }
        }

        // Shuffle positions so grid layout doesn't correlate with objective order
        // ... (Fisher-Yates shuffle)

        // --- Step 4: Enter playing state and spawn everything ---
        m_State = GameState.Playing;
        m_SpawnedObjects.Clear();
        m_CurrentObjectiveIndex = 0;
        m_FoundCount = 0;
        m_ExpectedSpawnCount = m_Objectives.Count;

        // Subscribe to factory's "fully configured" event
        m_Factory.objectFullyConfigured += OnObjectFullyConfigured;

        // Enqueue each combo then spawn it at the corresponding position
        for (int i = 0; i < m_Objectives.Count; i++)
        {
            var combo = m_Objectives[i];
            m_Factory.EnqueueCombo(combo.shape, combo.color);  // tell factory what to make
            m_Spawner.TrySpawnObject(positions[i], Vector3.up); // spawn at position
        }

        ShowCurrentObjective();
        m_UI.StartTimer();

        OnGameStarted?.Invoke();  // notify voice assistant
    }
```

#### Wiring Grab Detection

After each object is fully configured by ShapeObjectFactory, we wire up grab detection:

```csharp
    void OnObjectFullyConfigured(GameObject obj)
    {
        if (m_State != GameState.Playing) return;

        m_SpawnedObjects.Add(obj);

        // Get the XRGrabInteractable — this is the XR Interaction Toolkit component
        // that makes objects grabbable with controllers.
        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            // "selectEntered" fires when the player picks up (grabs) the object.
            // AddListener registers our callback method.
            grab.selectEntered.AddListener(OnObjectGrabbed);
        }

        // Once all 9 are configured, stop listening (prevent memory leaks)
        if (m_SpawnedObjects.Count >= m_Objectives.Count)
            m_Factory.objectFullyConfigured -= OnObjectFullyConfigured;
    }
```

#### Handling Grabs — Correct vs Wrong

```csharp
    void OnObjectGrabbed(SelectEnterEventArgs args)
    {
        if (m_State != GameState.Playing) return;

        // Get the grabbed object and its metadata
        var obj = args.interactableObject.transform.gameObject;
        var info = obj.GetComponent<SpawnableObjectInfo>();
        if (info == null) return;

        // What are we looking for right now?
        var current = m_Objectives[m_CurrentObjectiveIndex];

        // Check if shape AND color both match
        if (info.shapeName == current.shape && info.colorName == current.color)
        {
            // ===== CORRECT! =====
            m_FoundCount++;
            OnObjectFound?.Invoke(m_CurrentObjectiveIndex);  // tell voice assistant

            // Force-drop and deactivate the object (make it disappear)
            var grab = obj.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected)
                grab.enabled = false;           // this force-drops the object
            grab?.selectEntered.RemoveListener(OnObjectGrabbed);  // cleanup
            obj.SetActive(false);               // hide it

            // Advance to next objective
            m_CurrentObjectiveIndex++;
            if (m_CurrentObjectiveIndex >= m_Objectives.Count)
            {
                // ===== ALL FOUND! =====
                m_State = GameState.Completed;
                float elapsed = m_UI.StopTimer();
                m_UI.ShowCompletion(m_Objectives.Count, elapsed);
                OnGameCompleted?.Invoke(elapsed);       // tell voice assistant
                StartCoroutine(ResetAfterDelay());       // auto-reset after 5 seconds
            }
            else
            {
                ShowCurrentObjective();  // show next target
            }
        }
        else
        {
            // ===== WRONG! =====
            m_UI.ShowWrongFeedback();
            OnWrongGrab?.Invoke(info.DisplayName, $"{current.color}_{current.shape}");
        }
    }
```

#### Game Reset

```csharp
    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(k_ResetDelay);  // wait 5 seconds

        // Destroy all spawned objects
        foreach (var obj in m_SpawnedObjects)
            if (obj != null) Destroy(obj);

        m_SpawnedObjects.Clear();
        m_Objectives.Clear();
        m_UI.Hide();
        m_State = GameState.Idle;  // ready for the next round
    }
```

---

## 8. Heads-Up Display — HUD (Full Code Walkthrough)

### FindObjectUI.cs

The HUD is a world-space canvas that floats in front of the player's view. It is created entirely in code — no prefabs or Inspector setup needed.

#### Canvas Hierarchy

```
World-Space Canvas (scale: 0.001, so 1 unit = 1mm)
├── Background Panel (400x240, black 70% opacity)
│   ├── ObjectiveText (top, 36pt, "Find: Red Sphere")
│   ├── ProgressText (middle, 28pt, "3 / 9 found")
│   └── TimerText (bottom, 26pt, yellow, "12.5s")
└── CompletionPanel (green, hidden until game ends)
    └── CompletionText (40pt, "All 9 found! Time: 42.3s")
```

#### Creating the HUD at Runtime

```csharp
public void Initialize()
{
    // Create a new empty GameObject named "FindObjectCanvas"
    var canvasGO = new GameObject("FindObjectCanvas");
    canvasGO.transform.SetParent(transform, false);  // make it a child of the spawner

    // Add Canvas component in WORLD SPACE mode.
    // Screen Space = flat on screen. World Space = floating 3D panel in the world.
    m_Canvas = canvasGO.AddComponent<Canvas>();
    m_Canvas.renderMode = RenderMode.WorldSpace;

    // Set canvas size and scale.
    // Unity UI uses pixel-like units, but we're in world space where 1 unit = 1 meter.
    // Scale 0.001 means 1 UI unit = 1 millimeter. So 400 units = 40cm wide.
    m_CanvasRect = canvasGO.GetComponent<RectTransform>();
    m_CanvasRect.sizeDelta = new Vector2(400, 240);     // 400mm × 240mm
    canvasGO.transform.localScale = Vector3.one * 0.001f;

    // LazyFollow makes the canvas smoothly follow the player's head.
    // It doesn't snap instantly — it lerps (smoothly interpolates) to the target position.
    m_LazyFollow = canvasGO.AddComponent<LazyFollow>();
    m_LazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
    m_LazyFollow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
    m_LazyFollow.targetOffset = new Vector3(0f, 0.25f, 1.2f);
    //                                       ↑     ↑       ↑
    //                            no left/right  25cm up  1.2m forward
    m_LazyFollow.applyTargetInLocalSpace = true;
    // "local space" means these offsets are relative to the camera, not the world.
    // So "1.2m forward" means 1.2m in front of wherever you're looking.

    // Create background panel (black, 70% opacity)
    var bgGO = CreatePanel(canvasGO.transform, "Background",
        new Vector2(400, 240), new Color(0f, 0f, 0f, 0.7f));

    // Create text elements at different vertical positions
    m_ObjectiveText = CreateText(bgGO.transform, "ObjectiveText",
        new Vector2(380, 80), new Vector2(0, 50), 36);  // top
    m_ProgressText = CreateText(bgGO.transform, "ProgressText",
        new Vector2(380, 40), new Vector2(0, -20), 28); // middle
    m_TimerText = CreateText(bgGO.transform, "TimerText",
        new Vector2(380, 40), new Vector2(0, -70), 26); // bottom

    m_TimerText.color = new Color(1f, 0.9f, 0.5f, 1f);  // warm yellow

    // Hidden completion panel (green background)
    m_CompletionPanel = CreatePanel(canvasGO.transform, "CompletionPanel",
        new Vector2(400, 240), new Color(0.05f, 0.3f, 0.05f, 0.85f));
    m_CompletionPanel.SetActive(false);  // hidden until game ends

    canvasGO.SetActive(false);  // entire HUD starts hidden
}
```

#### Showing the Current Objective

```csharp
public void ShowObjective(Color color, string shapeName, int found, int total)
{
    m_Canvas.gameObject.SetActive(true);   // make HUD visible
    m_CompletionPanel.SetActive(false);     // hide completion overlay

    // Convert Color to hex string for rich text: Color(0.9, 0.15, 0.15) → "E52626"
    string hex = ColorUtility.ToHtmlStringRGB(color);
    // TextMeshPro supports HTML-like rich text tags
    m_CurrentObjectiveString = $"Find: <color=#{hex}>{shapeName}</color>";
    // This renders as: "Find: " in white, then "Red Sphere" in red

    m_ObjectiveText.text = m_CurrentObjectiveString;
    m_ProgressText.text = $"{found} / {total} found";
}
```

#### Wrong Feedback and Live Timer (Update Loop)

```csharp
void Update()
{
    // ---- Wrong feedback auto-clear ----
    // When ShowWrongFeedback() is called, it sets m_WrongFeedbackEndTime to
    // Time.time + 0.8. This block waits until that time passes, then restores
    // the original objective text.
    if (m_WrongFeedbackEndTime > 0f && Time.time > m_WrongFeedbackEndTime)
    {
        m_WrongFeedbackEndTime = 0f;  // reset so this doesn't run again
        m_ObjectiveText.text = m_CurrentObjectiveString;  // restore
    }

    // ---- Live timer ----
    if (m_TimerRunning && m_TimerText != null)
    {
        float elapsed = Time.time - m_TimerStartTime;
        int minutes = (int)(elapsed / 60f);
        float seconds = elapsed % 60f;       // remainder after dividing by 60
        m_TimerText.text = minutes > 0
            ? $"{minutes}:{seconds:00.0}s"   // "1:05.3s"
            : $"{seconds:F1}s";              // "12.5s"
        // F1 = 1 decimal place, 00.0 = zero-padded to 2 digits
    }
}
```

#### Helper: Creating UI Elements in Code

```csharp
static TextMeshProUGUI CreateText(Transform parent, string name,
    Vector2 size, Vector2 position, float fontSize)
{
    var go = new GameObject(name);                     // create empty GameObject
    go.transform.SetParent(parent, false);             // make it a child

    var rect = go.AddComponent<RectTransform>();       // UI layout component
    rect.sizeDelta = size;                             // width × height
    rect.anchoredPosition = position;                  // position relative to parent center

    var tmp = go.AddComponent<TextMeshProUGUI>();      // TextMeshPro text component
    tmp.fontSize = fontSize;
    tmp.color = Color.white;
    tmp.enableWordWrapping = true;
    tmp.overflowMode = TextOverflowModes.Ellipsis;     // "..." if text is too long

    return tmp;
}
```

---

## 9. Eye Gaze Tracking

The VIVE Focus Vision headset has built-in eye tracking hardware. Unity's XR Interaction Toolkit provides the `XRGazeInteractor` to process eye gaze data.

### How Eye Gaze Works in This Project

1. **Hardware**: The headset's eye cameras track pupil position at high frequency.
2. **XR Subsystem**: VIVE's OpenXR runtime converts pupil data into a world-space ray (origin + direction).
3. **XRGazeInteractor**: Unity's component casts this ray into the scene and determines which `XRGrabInteractable` objects it hits.
4. **interactablesHovered**: The list of objects currently under the gaze ray. This is what our scripts poll.

### The Gaze Interactor

The gaze interactor is a GameObject in the scene with these components:
- `XRGazeInteractor` — processes eye tracking data
- `GazeHighlightManager` — applies orange highlights
- `GazeDataLogger` — records gaze data to CSV
- `EyeGazeRayVisual` — shows the orange ray

---

## 10. Gaze Highlighting (Full Code Walkthrough)

### GazeHighlightManager.cs

This script makes objects glow orange when you look at them with your eyes.

```csharp
[DefaultExecutionOrder(200)]  // Run AFTER other LateUpdate scripts (see explanation below)
public class GazeHighlightManager : MonoBehaviour
{
    // Pre-compute shader property IDs for performance.
    // Shader.PropertyToID converts a string name to an integer ID.
    // Integers are faster to look up than strings every frame.
    static readonly int k_EdgeHighlightColor = Shader.PropertyToID("_EdgeHighlightColor");
    static readonly int k_EdgeHighlightFalloff = Shader.PropertyToID("_EdgeHighlightFalloff");

    static readonly Color k_GazeHighlightColor = new Color(1f, 0.5f, 0f, 1f);  // orange
    const float k_GazeHighlightFalloff = 1.5f;

    XRBaseInputInteractor m_GazeInteractor;
    MaterialPropertyBlock m_PropertyBlock;

    // Two sets track which renderers are highlighted THIS frame vs LAST frame.
    // By comparing them, we know what to highlight and what to un-highlight.
    readonly HashSet<Renderer> m_PrevHighlighted = new HashSet<Renderer>();
    readonly HashSet<Renderer> m_CurrentHighlighted = new HashSet<Renderer>();

    void OnEnable()
    {
        m_PropertyBlock = new MaterialPropertyBlock();
        // Find the XR interactor on the same GameObject as us
        m_GazeInteractor = GetComponent<XRBaseInputInteractor>();
    }

    void LateUpdate()
    {
        if (m_GazeInteractor == null) return;

        m_CurrentHighlighted.Clear();

        // Step 1: Get all objects currently under the gaze ray
        var hovered = m_GazeInteractor.interactablesHovered;

        // Step 2: For each hovered object, apply the orange highlight
        for (int i = 0; i < hovered.Count; i++)
        {
            var interactable = hovered[i];
            if (interactable == null) continue;

            // Get ALL renderers on the object (root + children)
            var renderers = interactable.transform.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                m_CurrentHighlighted.Add(r);  // mark as highlighted this frame

                // MaterialPropertyBlock lets us change shader properties per-object
                // WITHOUT creating a new material. This is much more efficient.
                r.GetPropertyBlock(m_PropertyBlock);  // read current values
                m_PropertyBlock.SetColor(k_EdgeHighlightColor, k_GazeHighlightColor);
                m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, k_GazeHighlightFalloff);
                r.SetPropertyBlock(m_PropertyBlock);  // write back
            }
        }

        // Step 3: Clear highlight on renderers from LAST frame that aren't hovered anymore
        foreach (var r in m_PrevHighlighted)
        {
            if (r == null) continue;
            if (m_CurrentHighlighted.Contains(r)) continue;  // still hovered, skip

            // Set color to clear (transparent) = no highlight
            r.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
            r.SetPropertyBlock(m_PropertyBlock);
        }

        // Step 4: Swap — current becomes previous for next frame
        m_PrevHighlighted.Clear();
        foreach (var r in m_CurrentHighlighted)
            m_PrevHighlighted.Add(r);
    }
}
```

**Why `[DefaultExecutionOrder(200)]` and `LateUpdate()`?**

Unity calls `LateUpdate()` on all scripts every frame, but the ORDER depends on their execution order number. By default, everything runs at 0. Here's the priority chain:

1. `Update()` — XR Interaction Toolkit updates hover states
2. `LateUpdate()` at order 100 — `InteractableHighlight` applies blue controller highlight
3. `LateUpdate()` at order 200 — `GazeHighlightManager` applies orange gaze highlight

Because gaze runs LAST, it overwrites the controller blue with gaze orange. This ensures eye gaze always takes visual priority over controller hover.

---

## 11. Controller Highlighting (Full Code Walkthrough)

### InteractableHighlight.cs

Per-object highlight for controller hover (light blue glow). This is different from gaze — it's attached to each spawned object individually and uses XR events instead of polling.

```csharp
[DefaultExecutionOrder(100)]  // Run BEFORE gaze highlight (200)
[RequireComponent(typeof(XRGrabInteractable))]  // Unity enforces: this component needs XRGrabInteractable
public class InteractableHighlight : MonoBehaviour
{
    static readonly Color k_ControllerColor = new Color(0.7f, 0.87f, 1f, 1f);  // light blue

    XRGrabInteractable m_Grab;
    MeshRenderer m_Renderer;
    MaterialPropertyBlock m_PropertyBlock;
    bool m_IsControllerHovering;

    void OnEnable()
    {
        m_Grab = GetComponent<XRGrabInteractable>();
        m_Renderer = GetComponent<MeshRenderer>();
        m_PropertyBlock = new MaterialPropertyBlock();

        // Subscribe to XR hover events on THIS object
        m_Grab.hoverEntered.AddListener(OnHoverEntered);
        m_Grab.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        // Always unsubscribe in OnDisable to prevent memory leaks
        m_Grab.hoverEntered.RemoveListener(OnHoverEntered);
        m_Grab.hoverExited.RemoveListener(OnHoverExited);

        // Clear highlight when disabled
        m_Renderer?.GetPropertyBlock(m_PropertyBlock);
        m_PropertyBlock?.SetColor(k_EdgeHighlightColor, Color.clear);
        m_Renderer?.SetPropertyBlock(m_PropertyBlock);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        // IGNORE gaze hovers — GazeHighlightManager handles those separately
        if (args.interactorObject is XRGazeInteractor)
            return;

        m_IsControllerHovering = true;
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject is XRGazeInteractor)
            return;

        m_IsControllerHovering = false;
    }

    void LateUpdate()
    {
        if (m_Renderer == null) return;

        m_Renderer.GetPropertyBlock(m_PropertyBlock);

        if (m_IsControllerHovering)
        {
            // Apply light blue highlight
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, k_ControllerColor);
            m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, 1.5f);
        }
        else
        {
            // Clear highlight
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
        }

        m_Renderer.SetPropertyBlock(m_PropertyBlock);
        // NOTE: If gaze is also active, GazeHighlightManager (order 200) will
        // overwrite this with orange in its own LateUpdate, which runs later.
    }
}
```

---

## 12. Eye Gaze Ray Visual (Full Code Walkthrough)

### EyeGazeRayVisual.cs

Configures the visible orange ray that shows where your eyes are looking.

```csharp
public class EyeGazeRayVisual : MonoBehaviour
{
    static readonly Color k_OrangeColor = new Color(1f, 0.5f, 0f, 1f);

    [SerializeField] bool m_FlipX = true;  // VIVE bug fix (explained below)

    void Awake()
    {
        // --- LineRenderer: the actual visible line in 3D space ---
        var lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.widthMultiplier = 0.02f;  // 2cm wide
        lineRenderer.useWorldSpace = true;      // positions are in world coordinates

        // --- XRInteractorLineVisual: XR toolkit wrapper that drives the LineRenderer ---
        var lineVisual = GetComponent<XRInteractorLineVisual>();
        if (lineVisual == null)
            lineVisual = gameObject.AddComponent<XRInteractorLineVisual>();

        lineVisual.lineWidth = 0.005f;
        lineVisual.overrideInteractorLineLength = true;
        lineVisual.lineLength = 10f;                        // max 10 meters long
        lineVisual.autoAdjustLineLength = true;             // shorten if hitting something
        lineVisual.stopLineAtFirstRaycastHit = true;        // stop at the first object hit
        lineVisual.useDistanceToHitAsMaxLineLength = true;  // don't extend past the hit point

        // Set ALL gradient states to orange (valid, invalid, blocked)
        // By default, XRI uses green=valid, red=invalid. We want always-orange.
        var alwaysOnGradient = new Gradient();
        alwaysOnGradient.SetKeys(
            new[] { new GradientColorKey(k_OrangeColor, 0f), new GradientColorKey(k_OrangeColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        lineVisual.validColorGradient = alwaysOnGradient;    // hitting a valid target
        lineVisual.invalidColorGradient = alwaysOnGradient;  // hitting nothing / invalid
        lineVisual.blockedColorGradient = alwaysOnGradient;  // blocked by UI
    }

    void LateUpdate()
    {
        // VIVE Focus Vision bug fix: the eye tracking data has the X axis
        // inverted (left eye reports right position and vice versa).
        // We correct this every frame by flipping the local X position.
        if (m_FlipX)
        {
            var pos = transform.localPosition;
            pos.x = -pos.x;
            transform.localPosition = pos;
        }
    }
}
```

---

## 13. Gaze Data Logging (Full Code Walkthrough)

### GazeDataLogger.cs

Records eye gaze data to a CSV file every frame for research analysis.

```csharp
public class GazeDataLogger : MonoBehaviour
{
    const int k_FlushInterval = 300;  // flush to disk every 300 frames

    XRBaseInputInteractor m_Interactor;
    StreamWriter m_Writer;

    // These track what the player is currently looking at.
    // Updated via events (not polling) for accuracy.
    string m_CurrentHoveredObject = "";
    string m_CurrentHoveredShape = "";
    string m_CurrentHoveredColor = "";
    int m_FrameCount;

    void OnEnable()
    {
        m_Interactor = GetComponent<XRBaseInputInteractor>();

        // Subscribe to HOVER events — more accurate than polling every frame
        // because we get the exact moment the gaze enters/exits an object.
        m_Interactor.hoverEntered.AddListener(OnHoverEntered);
        m_Interactor.hoverExited.AddListener(OnHoverExited);

        // Create CSV file with timestamp in the name
        var dir = Application.persistentDataPath;
        // On VIVE: /sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/
        var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(dir, $"gaze_log_{timestamp}.csv");

        m_Writer = new StreamWriter(path, false, Encoding.UTF8);
        // Write CSV header
        m_Writer.WriteLine("timestamp,frame,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z," +
                           "hovered_object,hovered_shape,hovered_color,ray_visible");
        m_Writer.Flush();
    }

    void OnDisable()
    {
        // CRITICAL: Always close file handles to prevent data loss
        m_Interactor?.hoverEntered.RemoveListener(OnHoverEntered);
        m_Interactor?.hoverExited.RemoveListener(OnHoverExited);
        m_Writer?.Flush();
        m_Writer?.Close();
    }

    // --- Event handlers for gaze entering/exiting objects ---

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        m_CurrentHoveredObject = args.interactableObject.transform.name;  // e.g., "Red_Sphere"

        // Read shape/color metadata from SpawnableObjectInfo (if present)
        var info = args.interactableObject.transform.GetComponent<SpawnableObjectInfo>();
        if (info != null)
        {
            m_CurrentHoveredShape = info.shapeName;  // "Sphere"
            m_CurrentHoveredColor = info.colorName;  // "Red"
        }
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        // Only clear if it's the SAME object that exited
        // (edge case: gaze could jump directly from one object to another)
        if (m_CurrentHoveredObject == args.interactableObject.transform.name)
        {
            m_CurrentHoveredObject = "";
            m_CurrentHoveredShape = "";
            m_CurrentHoveredColor = "";
        }
    }

    // --- Per-frame logging ---

    void Update()
    {
        if (m_Writer == null) return;

        var pos = transform.position;             // world position of gaze ray origin
        var rot = transform.rotation.eulerAngles; // gaze direction as Euler angles

        // Check if the ray visual is currently visible
        var lineVisual = GetComponent<XRInteractorLineVisual>();
        bool rayVisible = lineVisual != null && lineVisual.enabled;

        // Write one CSV row per frame
        m_Writer.WriteLine(string.Format(
            "{0:F3},{1},{2:F4},{3:F4},{4:F4},{5:F2},{6:F2},{7:F2},{8},{9},{10},{11}",
            Time.time,           // 0: seconds since app start
            Time.frameCount,     // 1: frame number
            pos.x, pos.y, pos.z, // 2-4: position
            rot.x, rot.y, rot.z, // 5-7: rotation
            m_CurrentHoveredObject,  // 8: what object is being looked at
            m_CurrentHoveredShape,   // 9: its shape
            m_CurrentHoveredColor,   // 10: its color
            rayVisible ? 1 : 0       // 11: is the ray showing?
        ));

        // Flush to disk periodically (not every frame — that would stutter)
        m_FrameCount++;
        if (m_FrameCount >= k_FlushInterval)  // every 300 frames (~4 seconds at 72fps)
        {
            m_Writer.Flush();
            m_FrameCount = 0;
        }
    }
}
```

**Example CSV output:**
```
timestamp,frame,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,hovered_object,hovered_shape,hovered_color,ray_visible
12.345,892,0.1234,1.5000,0.2345,45.00,30.00,0.00,Red_Sphere,Sphere,Red,1
12.359,893,0.1238,1.5001,0.2347,44.98,30.02,0.00,Red_Sphere,Sphere,Red,1
12.373,894,0.1250,1.4999,0.2355,44.50,31.00,0.00,,,,1
```

---

## 14. Object Metadata — SpawnableObjectInfo

### SpawnableObjectInfo.cs

A tiny but important component. Added to every spawned object by ShapeObjectFactory.

```csharp
public class SpawnableObjectInfo : MonoBehaviour
{
    public string shapeName;   // "Sphere", "Cube", or "Pyramid"
    public string colorName;   // "Red", "Blue", "Yellow", or "Purple"

    // Convenience property: returns "Red_Sphere" format
    public string DisplayName => $"{colorName}_{shapeName}";
}
```

This is used by:
- **GazeDataLogger** — to log which shape/color is being looked at
- **FindObjectGameManager** — to check if the grabbed object matches the current objective
- **AgentContext** — to describe objects to the LLM
- **ShapeObjectFactory** — to name the GameObject (e.g., `obj.name = "Red_Sphere"`)

---

## 15. The AI Voice Assistant

The voice assistant is a multi-component system that gives the player spoken hints during the game. It uses two external APIs:

- **OpenAI GPT-4o-mini** — generates contextual text hints
- **ElevenLabs** — converts text to natural-sounding speech

### Component Overview

```
VoiceAssistantController (orchestrator)
├── AgentContext         → "What's happening in the game right now?"
├── HintGenerator        → "Generate a helpful hint" (calls OpenAI)
└── VoiceSynthesizer     → "Say this out loud" (calls ElevenLabs)
```

### Design Decisions

1. **Direct TTS for predictable messages**: Welcome, congratulations, next objective — these have no LLM latency because the text is hardcoded.
2. **LLM only for search hints**: Where contextual, varied responses actually matter.
3. **Non-spatialized audio**: The assistant is an omnipresent guide, not a spatial entity. The voice comes from "inside your head" (2D audio, no directional positioning).
4. **Graceful degradation**: All API failures log warnings silently. The game works identically without the assistant.

---

## 16. Voice Assistant Controller (Full Code Walkthrough)

### VoiceAssistantController.cs

This is the orchestrator — it wires everything together and decides what to say in response to game events.

```csharp
public class VoiceAssistantController : MonoBehaviour
{
    const string k_KeysFile = "api_keys.json";  // file in StreamingAssets/

    FindObjectGameManager m_GameManager;
    AgentContext m_AgentContext;
    HintGenerator m_HintGenerator;
    VoiceSynthesizer m_VoiceSynthesizer;

    // Auto-attach: runs automatically after the scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var spawner = FindObjectOfType<ObjectSpawner>();
        if (spawner != null && spawner.GetComponent<VoiceAssistantController>() == null)
            spawner.gameObject.AddComponent<VoiceAssistantController>();
    }

    void OnEnable()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
        if (m_GameManager == null) { enabled = false; return; }

        // --- Load API keys from a JSON file ---
        // StreamingAssets is a special Unity folder: files here are included in
        // the build and accessible at runtime via Application.streamingAssetsPath.
        string openAiKey = "";
        string elevenLabsKey = "";
        string keysPath = System.IO.Path.Combine(Application.streamingAssetsPath, k_KeysFile);
        try
        {
            string json = System.IO.File.ReadAllText(keysPath);
            var keys = JsonUtility.FromJson<ApiKeys>(json);
            openAiKey = keys.openai_key;
            elevenLabsKey = keys.elevenlabs_key;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not load API keys: {e.Message}. Voice assistant disabled.");
        }

        // --- Create sub-components ---
        // These are added to the SAME GameObject (the ObjectSpawner).
        // AddComponent returns the newly added component so we can configure it.
        m_VoiceSynthesizer = gameObject.AddComponent<VoiceSynthesizer>();
        m_VoiceSynthesizer.Initialize(elevenLabsKey);

        m_AgentContext = gameObject.AddComponent<AgentContext>();
        var ui = GetComponent<FindObjectUI>();
        m_AgentContext.Initialize(m_GameManager, ui);

        m_HintGenerator = gameObject.AddComponent<HintGenerator>();
        m_HintGenerator.Initialize(openAiKey, m_AgentContext, m_VoiceSynthesizer);

        // --- Subscribe to game events ---
        // When these events fire, our handler methods get called automatically.
        m_GameManager.OnGameStarted += HandleGameStarted;
        m_GameManager.OnObjectFound += HandleObjectFound;
        m_GameManager.OnWrongGrab += HandleWrongGrab;
        m_GameManager.OnGameCompleted += HandleGameCompleted;
    }

    void OnDisable()
    {
        // ALWAYS unsubscribe in OnDisable to prevent memory leaks and crashes
        if (m_GameManager != null)
        {
            m_GameManager.OnGameStarted -= HandleGameStarted;
            m_GameManager.OnObjectFound -= HandleObjectFound;
            m_GameManager.OnWrongGrab -= HandleWrongGrab;
            m_GameManager.OnGameCompleted -= HandleGameCompleted;
        }
    }
```

#### Event Handlers

```csharp
    void HandleGameStarted()
    {
        // Build a welcome message with the first objective
        var objectives = m_GameManager.Objectives;
        if (objectives.Count == 0) return;

        var first = objectives[0];
        string welcome = $"Let's play! Find the {first.color} {first.shape}. Look around the table!";

        // Direct TTS — no LLM call, instant speech
        m_VoiceSynthesizer.Speak(welcome, "welcome");
        m_HintGenerator.OnNewObjective();  // start hint timers
    }

    void HandleObjectFound(int objectiveIndex)
    {
        // Step 1: Stop any in-progress hint speech
        m_VoiceSynthesizer.InterruptIfAbout("hint");
        m_HintGenerator.CancelPending();

        // Step 2: Announce the next objective
        var objectives = m_GameManager.Objectives;
        int nextIndex = objectiveIndex + 1;

        if (nextIndex < objectives.Count)
        {
            var next = objectives[nextIndex];
            string congrats = $"Great job! Now find the {next.color} {next.shape}.";
            m_VoiceSynthesizer.Speak(congrats, "congrats");
            m_HintGenerator.OnNewObjective();  // reset hint timers
        }
        // If it's the last object, HandleGameCompleted will fire separately
    }

    void HandleWrongGrab(string grabbedName, string wantedName)
    {
        // Don't speak directly — let the HintGenerator decide when
        // (it waits 3 seconds, then asks OpenAI for a contextual redirect)
        m_HintGenerator.OnWrongGrab();
    }

    void HandleGameCompleted(float elapsedSeconds)
    {
        // Stop all hint activity
        m_HintGenerator.CancelPending();
        m_VoiceSynthesizer.Stop();

        // Format time as natural speech
        int minutes = (int)(elapsedSeconds / 60f);
        float seconds = elapsedSeconds % 60f;
        string timeStr = minutes > 0
            ? $"{minutes} minutes and {seconds:F0} seconds"
            : $"{seconds:F0} seconds";

        string completion = $"Amazing! You found all the objects in {timeStr}! Great work!";
        m_VoiceSynthesizer.Speak(completion, "completion");
    }

    // Struct for deserializing the api_keys.json file
    [System.Serializable]
    struct ApiKeys
    {
        public string openai_key;
        public string elevenlabs_key;
    }
}
```

---

## 17. Agent Context — Scene Knowledge Builder (Full Code Walkthrough)

### AgentContext.cs

This script builds a structured text description of the current game state that gets sent to the LLM as context. It answers: "What is happening in the game right now?"

```csharp
public class AgentContext : MonoBehaviour
{
    FindObjectGameManager m_GameManager;
    FindObjectUI m_UI;
    XRBaseInputInteractor m_GazeInteractor;
    readonly StringBuilder m_Builder = new();  // reusable string builder (avoids garbage collection)

    public void Initialize(FindObjectGameManager gameManager, FindObjectUI ui)
    {
        m_GameManager = gameManager;
        m_UI = ui;
    }

    void Start()
    {
        // Find the gaze interactor by looking for GazeHighlightManager first.
        // Both GazeHighlightManager and AgentContext need the same interactor,
        // so we find it via the highlight manager rather than searching by type.
        var highlighter = FindObjectOfType<GazeHighlightManager>();
        if (highlighter != null)
            m_GazeInteractor = highlighter.GetComponent<XRBaseInputInteractor>();
    }
```

#### Building the Context Prompt

This is the text that gets sent to GPT-4o-mini as the "user" message:

```csharp
    public string BuildContextPrompt()
    {
        if (m_GameManager == null ||
            m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing)
            return "";  // only generate context during active gameplay

        var cam = Camera.main;  // the player's head camera
        if (cam == null) return "";

        m_Builder.Clear();  // reuse the same StringBuilder to avoid memory allocation

        // ---- Current objective ----
        var objectives = m_GameManager.Objectives;
        int idx = m_GameManager.CurrentObjectiveIndex;
        if (idx < objectives.Count)
            m_Builder.AppendLine($"CURRENT TARGET: {objectives[idx].color} {objectives[idx].shape}");

        // ---- Progress ----
        m_Builder.AppendLine(
            $"PROGRESS: {m_GameManager.FoundCount}/{objectives.Count} found " +
            $"(searching for #{idx + 1})");

        // ---- Elapsed time ----
        if (m_UI != null && m_UI.IsTimerRunning)
        {
            float elapsed = Time.time - m_UI.TimerStartTime;
            m_Builder.AppendLine($"TIME SEARCHING: {elapsed:F0} seconds");
        }

        // ---- What the player is looking at ----
        string gazedObjectDesc = GetGazedObjectDescription();
        m_Builder.AppendLine(gazedObjectDesc != null
            ? $"PLAYER IS LOOKING AT: {gazedObjectDesc}"
            : "PLAYER IS LOOKING AT: nothing / empty space");

        // ---- Gaze direction ----
        string gazeDescription = DescribeGazeDirection(cam.transform.forward);
        m_Builder.AppendLine($"GAZE DIRECTION: {gazeDescription}");

        // ---- All remaining objects with spatial descriptions ----
        m_Builder.AppendLine("\nOBJECTS ON TABLE:");
        var spawnedObjects = m_GameManager.SpawnedObjects;
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            var obj = spawnedObjects[i];
            if (obj == null || !obj.activeSelf) continue;  // skip found (deactivated) objects

            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info == null) continue;

            // Convert 3D position to natural language
            string spatial = DescribeObjectRelativeToPlayer(obj.transform.position, cam.transform);

            // Mark the target object so the LLM knows which one to guide toward
            bool isTarget = idx < objectives.Count &&
                            info.shapeName == objectives[idx].shape &&
                            info.colorName == objectives[idx].color;

            string marker = isTarget ? " [TARGET]" : "";
            m_Builder.AppendLine($"  - {info.colorName} {info.shapeName}: {spatial}{marker}");
        }

        return m_Builder.ToString();
    }
```

**Example output sent to GPT-4o-mini:**
```
CURRENT TARGET: Blue Cube
PROGRESS: 3/9 found (searching for #4)
TIME SEARCHING: 23 seconds
PLAYER IS LOOKING AT: Red Sphere
GAZE DIRECTION: looking left, looking down at the table

OBJECTS ON TABLE:
  - Red Sphere: 0.3m to your left and 0.2m ahead
  - Blue Cube: 0.5m to your right and 0.4m ahead [TARGET]
  - Yellow Pyramid: 0.1m to your left and 0.3m behind you
  - Purple Sphere: 0.6m to your right and 0.1m ahead
  ...
```

#### Converting 3D Positions to Natural Language

```csharp
    string DescribeObjectRelativeToPlayer(Vector3 worldPos, Transform playerTransform)
    {
        // InverseTransformPoint converts a world position to LOCAL space
        // relative to the player. In local space:
        //   +X = player's right
        //   -X = player's left
        //   +Z = forward (where the player faces)
        //   -Z = behind the player
        Vector3 local = playerTransform.InverseTransformPoint(worldPos);

        var parts = new List<string>();

        // Left/right (only mention if > 10cm, to avoid "0.0m to your right")
        if (Mathf.Abs(local.x) > 0.1f)
        {
            parts.Add(local.x > 0
                ? $"{Mathf.Abs(local.x):F1}m to your right"   // positive X = right
                : $"{Mathf.Abs(local.x):F1}m to your left");  // negative X = left
        }

        // Forward/behind
        if (Mathf.Abs(local.z) > 0.1f)
        {
            parts.Add(local.z > 0
                ? $"{Mathf.Abs(local.z):F1}m ahead"
                : $"{Mathf.Abs(local.z):F1}m behind you");
        }

        if (parts.Count == 0)
            return "directly in front of you";

        return string.Join(" and ", parts);
        // Result: "0.5m to your right and 0.4m ahead"
    }
```

#### Gaze Target Detection

```csharp
    public bool IsGazingAtTarget()
    {
        if (m_GazeInteractor == null || m_GameManager == null) return false;

        var target = m_GameManager.Objectives[m_GameManager.CurrentObjectiveIndex];

        // Check all currently hovered objects
        var hovered = m_GazeInteractor.interactablesHovered;
        for (int i = 0; i < hovered.Count; i++)
        {
            var info = hovered[i]?.transform.GetComponent<SpawnableObjectInfo>();
            // Match BOTH shape AND color
            if (info != null && info.shapeName == target.shape && info.colorName == target.color)
                return true;
        }
        return false;
    }
```

---

## 18. Hint Generator — OpenAI Integration (Full Code Walkthrough)

### HintGenerator.cs

This script has two jobs: (1) decide WHEN to generate a hint, and (2) call the OpenAI API.

#### Timing Constants

```csharp
const float k_FirstHintDelay = 15f;    // first hint after 15s of searching
const float k_SubsequentInterval = 20f; // then every 20s if still stuck
const float k_WrongGrabDelay = 3f;      // hint 3s after grabbing wrong object
const float k_GazeNudgeTime = 10f;      // if staring at target for 10s without grabbing
const float k_MinGap = 8f;              // minimum 8s between ANY two hints
```

#### The LLM System Prompt

This defines the assistant's personality and behavior:

```csharp
const string k_SystemPrompt =
    "You are a friendly VR game assistant helping a player find colored shapes on a table. " +
    "Give SHORT hints (1-2 sentences, under 30 words). " +
    "Use spatial directions (left, right, ahead, behind). " +
    "Be warm and encouraging. " +
    "Don't give exact answers immediately — get more specific if the player " +
    "has been struggling longer. Vary your phrasing. " +
    "You have access to the player's eye gaze data — use it to guide them. " +
    "If they're looking in the wrong direction, gently redirect. " +
    "If they're close to the target, encourage them.";
```

#### Hint Timing Logic (Update Loop)

Every frame, this checks if it's time to generate a hint:

```csharp
    void Update()
    {
        // Don't do anything if not properly initialized
        if (m_Context == null || m_Voice == null) return;
        if (string.IsNullOrEmpty(m_ApiKey)) return;

        // Don't queue if already generating a hint OR voice is playing
        if (m_GenerateCoroutine != null || m_Voice.IsSpeaking) return;

        float now = Time.time;
        float sinceObjective = now - m_ObjectiveStartTime;  // how long on current target
        float sinceLastHint = now - m_LastHintTime;         // how long since any hint

        // Enforce minimum gap between hints (prevents spamming)
        if (sinceLastHint < k_MinGap) return;

        // ---- Check 1: Gaze nudge ----
        // Is the player staring at the target without picking it up?
        if (m_Context.IsGazingAtTarget())
        {
            if (m_GazeOnTargetStart <= 0f)
                m_GazeOnTargetStart = now;  // start tracking gaze duration

            if (!m_GazeNudgeGiven && (now - m_GazeOnTargetStart) >= k_GazeNudgeTime)
            {
                m_GazeNudgeGiven = true;
                RequestHint("The player has been looking at the target object for a while " +
                           "but hasn't grabbed it. Give an encouraging nudge to pick it up.");
                return;
            }
        }
        else
        {
            m_GazeOnTargetStart = 0f;  // reset timer when not looking at target
        }

        // ---- Check 2: After wrong grab ----
        if (m_WrongGrabTime > 0f && (now - m_WrongGrabTime) >= k_WrongGrabDelay)
        {
            m_WrongGrabTime = 0f;
            RequestHint("The player just grabbed the wrong object. " +
                       "Help redirect them to the correct one.");
            return;
        }

        // ---- Check 3: First hint ----
        if (m_LastHintTime < m_ObjectiveStartTime && sinceObjective >= k_FirstHintDelay)
        {
            RequestHint("The player has been searching for a while. " +
                       "Give a helpful spatial hint.");
            return;
        }

        // ---- Check 4: Subsequent hints ----
        if (sinceLastHint >= k_SubsequentInterval && sinceObjective >= k_FirstHintDelay)
        {
            // Get MORE specific if the player has been struggling
            string urgency = sinceObjective > 60f
                ? "The player has been struggling for over a minute. " +
                  "Give a more specific, direct hint."
                : "Give another helpful hint. Be a bit more specific than before.";
            RequestHint(urgency);
        }
    }
```

#### Making the OpenAI API Call

```csharp
    IEnumerator GenerateHint(string situationDescription)
    {
        // Step 1: Get the current scene state from AgentContext
        string contextPrompt = m_Context.BuildContextPrompt();
        if (string.IsNullOrEmpty(contextPrompt)) yield break;

        // Step 2: Build the user message (situation + scene state)
        string userMessage = $"{situationDescription}\n\nCurrent scene state:\n{contextPrompt}";

        // Step 3: Build JSON body manually
        // We don't use JsonUtility because it can't handle nested arrays properly.
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"model\":\"{k_Model}\",");           // "gpt-4o-mini"
        sb.Append($"\"max_tokens\":{k_MaxTokens},");      // 80
        sb.Append($"\"temperature\":{k_Temperature},");   // 0.8
        sb.Append("\"messages\":[");
        sb.Append($"{{\"role\":\"system\",\"content\":{EscapeJson(k_SystemPrompt)}}},");
        sb.Append($"{{\"role\":\"user\",\"content\":{EscapeJson(userMessage)}}}");
        sb.Append("]}");

        // Step 4: Send HTTP POST request
        var request = new UnityWebRequest(k_ApiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(sb.ToString());
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {m_ApiKey}");
        request.timeout = 10;

        yield return request.SendWebRequest();
        // Execution pauses here until the HTTP response arrives.
        // During the wait, the game continues running normally.

        // Step 5: Check if we were cancelled while waiting
        if (m_CancelRequested) yield break;

        // Step 6: Check for errors
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"OpenAI request failed: {request.error}");
            yield break;
        }

        // Step 7: Parse the JSON response using Newtonsoft.Json
        string hintText = null;
        try
        {
            var json = JObject.Parse(request.downloadHandler.text);
            // Navigate: response.choices[0].message.content
            hintText = json["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to parse response: {e.Message}");
        }

        if (string.IsNullOrEmpty(hintText)) yield break;

        // Step 8: Speak the hint!
        m_LastHintTime = Time.time;
        m_Voice.Speak(hintText, "hint");  // "hint" context tag for interruption matching
    }
```

#### JSON Escaping Helper

```csharp
    // Properly escapes a string for embedding in JSON.
    // Without this, characters like quotes and newlines would break the JSON.
    static string EscapeJson(string s)
    {
        return "\"" + s
            .Replace("\\", "\\\\")   // \ → \\
            .Replace("\"", "\\\"")   // " → \"
            .Replace("\n", "\\n")    // newline → \n
            .Replace("\r", "\\r")    // carriage return → \r
            .Replace("\t", "\\t")    // tab → \t
            + "\"";
    }
```

---

## 19. Voice Synthesizer — ElevenLabs TTS (Full Code Walkthrough)

### VoiceSynthesizer.cs

Converts text to spoken audio using the ElevenLabs API.

```csharp
public class VoiceSynthesizer : MonoBehaviour
{
    const string k_BaseUrl = "https://api.elevenlabs.io/v1/text-to-speech/";
    const string k_VoiceId = "21m00Tcm4TlvDq8ikWAM";  // Rachel — warm, clear voice
    const string k_Model = "eleven_turbo_v2_5";         // lowest latency model

    string m_ApiKey;
    AudioSource m_AudioSource;
    Coroutine m_SpeakCoroutine;
    string m_CurrentContext;  // what the current speech is about ("hint", "welcome", etc.)

    // Other scripts can check this to avoid queueing hints while speech is playing
    public bool IsSpeaking => m_AudioSource != null && m_AudioSource.isPlaying;

    public void Initialize(string apiKey)
    {
        m_ApiKey = apiKey;

        // Create an AudioSource — Unity's component for playing sounds
        m_AudioSource = gameObject.AddComponent<AudioSource>();
        m_AudioSource.spatialBlend = 0f;    // 0 = 2D (non-spatialized, "in your head")
                                             // 1 = 3D (positional, comes from a location)
        m_AudioSource.volume = 0.7f;
        m_AudioSource.playOnAwake = false;   // don't play on startup
    }
```

#### Speaking Text

```csharp
    public void Speak(string text, string context = null)
    {
        if (string.IsNullOrEmpty(m_ApiKey)) return;

        Stop();                  // cancel any in-progress speech first
        m_CurrentContext = context;
        m_SpeakCoroutine = StartCoroutine(SpeakCoroutine(text));
    }
```

#### The TTS Pipeline Coroutine

```csharp
    IEnumerator SpeakCoroutine(string text)
    {
        // ---- Step 1: Call ElevenLabs API ----
        string url = $"{k_BaseUrl}{k_VoiceId}";  // POST to /v1/text-to-speech/{voiceId}

        // Build the request body
        string jsonBody = JsonUtility.ToJson(new TtsRequest
        {
            text = text,
            model_id = k_Model
        });

        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("xi-api-key", m_ApiKey);  // ElevenLabs uses xi-api-key header
        request.timeout = 15;

        yield return request.SendWebRequest();
        // Wait for the API response... game keeps running

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"TTS failed: {request.error}");
            yield break;
        }

        // ---- Step 2: Get the MP3 audio bytes ----
        byte[] mp3Data = request.downloadHandler.data;
        if (mp3Data == null || mp3Data.Length < 100)
        {
            Debug.LogWarning("TTS returned empty audio");
            yield break;
        }

        // ---- Step 3: Save to temp file and decode ----
        // We can't decode MP3 bytes directly into an AudioClip in Unity.
        // Workaround: save to a temp file, then use Unity's built-in MP3 decoder.
        string tempPath = System.IO.Path.Combine(
            Application.temporaryCachePath, "tts_temp.mp3");
        System.IO.File.WriteAllBytes(tempPath, mp3Data);

        // Use UnityWebRequestMultimedia to decode the MP3 file
        string fileUrl = "file://" + tempPath;  // local file URL
        using (var audioRequest = UnityWebRequestMultimedia.GetAudioClip(
            fileUrl, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Failed to decode MP3: {audioRequest.error}");
                yield break;
            }

            // ---- Step 4: Play the audio ----
            var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
            m_AudioSource.clip = clip;
            m_AudioSource.Play();

            // ---- Step 5: Wait for playback to finish ----
            while (m_AudioSource.isPlaying)
                yield return null;  // check every frame
        }

        // ---- Step 6: Cleanup ----
        try { System.IO.File.Delete(tempPath); } catch { }
        m_SpeakCoroutine = null;
        m_CurrentContext = null;
    }
```

#### Interruption System

```csharp
    public void Stop()
    {
        // Cancel the coroutine (stops the API call or playback)
        if (m_SpeakCoroutine != null)
        {
            StopCoroutine(m_SpeakCoroutine);
            m_SpeakCoroutine = null;
        }

        // Stop the AudioSource immediately
        if (m_AudioSource != null && m_AudioSource.isPlaying)
            m_AudioSource.Stop();

        m_CurrentContext = null;
    }

    // Selective interruption: only stop if the current speech matches the given context.
    // Example: InterruptIfAbout("hint") stops hint speech but NOT congratulations.
    public void InterruptIfAbout(string context)
    {
        if (!string.IsNullOrEmpty(m_CurrentContext) &&
            m_CurrentContext.Equals(context, StringComparison.OrdinalIgnoreCase))
        {
            Stop();
        }
    }
```

#### The TTS Request Struct

```csharp
    [Serializable]
    struct TtsRequest
    {
        public string text;       // the text to speak
        public string model_id;   // "eleven_turbo_v2_5"
    }
    // JsonUtility.ToJson(new TtsRequest { text = "Hello", model_id = "eleven_turbo_v2_5" })
    // produces: {"text":"Hello","model_id":"eleven_turbo_v2_5"}
```

---

## 20. Feature Toggling (Hand Menu)

### ARFeatureController.cs

Manages toggleable AR features from the hand menu. This script is part of the original Unity template and was modified to support VIVE plane detection.

| Feature | What It Does |
|---------|-------------|
| Planes | Show/hide detected plane meshes |
| Plane Visualization | Toggle mesh visibility (colliders stay) |
| Gaze Ray | Show/hide the orange eye gaze ray |
| Passthrough | Toggle camera passthrough |
| Bounding Boxes | Toggle scene understanding |

**The VIVE fallback pattern** is used in `TogglePlanes()` and `TogglePlaneVisualization()`:

```csharp
public void TogglePlanes(bool enabled)
{
    // Try AR Foundation first (works on most XR headsets)
    if (m_PlaneManager.subsystem != null)
    {
        // AR Foundation path: enable/disable the plane manager
        m_PlaneManager.enabled = enabled;
        m_PlaneManager.SetTrackablesActive(enabled);
        return;
    }

    // Fallback: use VIVE native plane detection
    if (m_VivePlaneProvider != null)
    {
        if (enabled)
            m_VivePlaneProvider.EnablePlanes();
        else
            m_VivePlaneProvider.DisablePlanes();
    }
}
```

**Gaze ray toggle** — only hides the visual, keeps the interactor active:

```csharp
public void ToggleGazeRay(bool enabled)
{
    if (m_GazeInteractor == null) return;

    // Toggle the visual components only
    var lineRenderer = m_GazeInteractor.GetComponent<LineRenderer>();
    if (lineRenderer != null) lineRenderer.enabled = enabled;

    var lineVisual = m_GazeInteractor.GetComponent<XRInteractorLineVisual>();
    if (lineVisual != null) lineVisual.enabled = enabled;

    // NOTE: The XRGazeInteractor itself stays active, so:
    //   - Gaze highlights still work
    //   - Gaze data still gets logged
    //   - The voice assistant still knows what you're looking at
}
```

### GazeToggleConnector.cs

A simple bridge that connects a UI Toggle to the gaze ray toggle:

```csharp
public class GazeToggleConnector : MonoBehaviour
{
    [SerializeField] ARFeatureController m_FeatureController;  // set in Inspector

    void Start()
    {
        var toggle = GetComponent<Toggle>();  // UI Toggle on the same GameObject
        if (toggle != null && m_FeatureController != null)
        {
            // When the toggle changes, call ToggleGazeRay with the new value
            toggle.onValueChanged.AddListener(m_FeatureController.ToggleGazeRay);
            // onValueChanged passes a bool (true/false) which matches
            // ToggleGazeRay's bool parameter — so it connects directly.
        }
    }
}
```

---

## 21. File Reference

### Core Game Logic
| File | Lines | Purpose |
|------|-------|---------|
| `FindObjectGameManager.cs` | ~376 | Game orchestration, state machine, objectives |
| `FindObjectUI.cs` | ~198 | World-space HUD with LazyFollow |
| `ShapeObjectFactory.cs` | ~407 | Shape/color assignment, mesh generation |
| `SpawnableObjectInfo.cs` | ~17 | Per-object metadata (shape, color) |

### Eye Gaze System
| File | Lines | Purpose |
|------|-------|---------|
| `GazeHighlightManager.cs` | ~99 | Orange highlight on gazed objects |
| `GazeDataLogger.cs` | ~114 | CSV logging of per-frame gaze data |
| `EyeGazeRayVisual.cs` | ~96 | Orange ray configuration + VIVE bug fix |
| `InteractableHighlight.cs` | ~91 | Blue highlight for controller hover |

### AI Voice Assistant
| File | Lines | Purpose |
|------|-------|---------|
| `VoiceAssistantController.cs` | ~151 | Orchestrator, event handling, API key loading |
| `AgentContext.cs` | ~216 | Scene state for LLM prompts |
| `HintGenerator.cs` | ~240 | OpenAI API + timing/debounce logic |
| `VoiceSynthesizer.cs` | ~173 | ElevenLabs TTS + audio playback + interruption |

### Plane Detection
| File | Lines | Purpose |
|------|-------|---------|
| `VivePlaneProvider.cs` | ~219 | VIVE native plane detection bridge |
| `VivePlaneData.cs` | ~32 | Plane metadata component |
| `ARFeatureController.cs` | ~443 | Feature toggling with VIVE fallback |

### Utilities
| File | Lines | Purpose |
|------|-------|---------|
| `GazeToggleConnector.cs` | ~22 | UI toggle wiring |
| `VivePassthrough.cs` | ~43 | Passthrough initialization |
| `PlaneDebug.cs` | ~43 | Diagnostic logging |

---

## 22. Data Flow Diagrams

### Spawn and Game Flow

```
Player taps table
    │
    ▼
ARContactSpawnTrigger
    │ UnityEvent
    ▼
ObjectSpawner.TrySpawnObject(pos, normal)
    │ objectSpawned event
    ├──────────────────────────────────┐
    ▼                                  ▼
ShapeObjectFactory                FindObjectGameManager
    │                                  │
    ├─ Pick shape (Sphere/Cube/Pyramid)│
    ├─ Pick color (Red/Blue/Yellow/Purple)
    ├─ Swap mesh, collider, material   │
    ├─ Enable gaze interaction         │
    ├─ Add SpawnableObjectInfo         │
    │                                  │
    │ objectFullyConfigured event      │
    └──────────────────────────────────┘
                    │
                    ▼
         Wire grab detection
         (XRGrabInteractable.selectEntered)
                    │
                    ▼
            Player grabs object
                    │
            ┌───────┴───────┐
            ▼               ▼
        Correct          Wrong
            │               │
    Deactivate obj    Flash "Wrong!"
    Advance objective  OnWrongGrab event
    OnObjectFound event     │
            │               ▼
            │      HintGenerator
            │      (3s delay → OpenAI → TTS)
            ▼
    All 9 found?
    ├─ No → Show next objective
    └─ Yes → OnGameCompleted
              Show completion screen
              5s → Reset to Idle
```

### Voice Assistant Flow

```
Game Event
    │
    ├─ OnGameStarted ────→ Direct TTS: "Let's play! Find the..."
    │
    ├─ OnObjectFound ────→ Interrupt hints
    │                      Direct TTS: "Great job! Now find..."
    │                      Reset hint timers
    │
    ├─ OnWrongGrab ──────→ HintGenerator.OnWrongGrab()
    │                      (3s delay)
    │                          │
    │                          ▼
    │                      AgentContext.BuildContextPrompt()
    │                          │ (scene state text)
    │                          ▼
    │                      OpenAI GPT-4o-mini
    │                          │ (hint text)
    │                          ▼
    │                      VoiceSynthesizer.Speak(hint)
    │                          │
    │                          ▼
    │                      ElevenLabs TTS API
    │                          │ (MP3 audio)
    │                          ▼
    │                      AudioSource.Play()
    │
    └─ OnGameCompleted ──→ Cancel all hints
                           Direct TTS: "Amazing! You found all..."
```

### Eye Gaze Data Flow

```
VIVE Eye Tracking Hardware
    │
    ▼
OpenXR Runtime (pupil → world ray)
    │
    ▼
XRGazeInteractor
    │ interactablesHovered (list of hit objects)
    │
    ├─────────────────────┐─────────────────────┐
    ▼                     ▼                     ▼
GazeHighlightManager  GazeDataLogger       AgentContext
    │                     │                     │
    │ Orange glow via     │ CSV per-frame       │ "Player is looking at
    │ MaterialPropertyBlock│ logging             │  Red Sphere, 0.3m left"
    │                     │                     │
    ▼                     ▼                     ▼
Visual feedback      Research data        LLM context for hints
```

### Highlight Priority Chain

```
Controller hovers object
    │
    ▼
InteractableHighlight (execution order 100)
    │ Applies BLUE highlight via MaterialPropertyBlock
    │
    ▼
Eye gaze also on same object?
    │
    ▼
GazeHighlightManager (execution order 200)
    │ OVERWRITES with ORANGE highlight via MaterialPropertyBlock
    │ (runs later because 200 > 100)
    │
    ▼
Player sees: ORANGE glow (gaze wins)
```
