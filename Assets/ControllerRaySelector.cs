using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>Highlights the controller ray hit and confirms once per released/pressed trigger cycle.</summary>
[DefaultExecutionOrder(200)]
public class ControllerRaySelector : MonoBehaviour
{
    static readonly int k_EdgeColor = Shader.PropertyToID("_EdgeHighlightColor");
    static readonly int k_Falloff = Shader.PropertyToID("_EdgeHighlightFalloff");
    static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly XRSelectFilterDelegate k_NoGrab = new XRSelectFilterDelegate((_, __) => false);
    readonly List<NearFarInteractor> m_Rays = new();
    readonly Dictionary<NearFarInteractor, int> m_OriginalMasks = new();
    readonly Dictionary<NearFarInteractor, bool> m_OriginalNearCasting = new();
    readonly HashSet<Renderer> m_Highlighted = new();
    readonly Dictionary<NearFarInteractor, GameObject> m_LastRayTargets = new();
    [SerializeField, Range(0f, 1f)] float m_HoverHapticAmplitude = 0.2f;
    [SerializeField, Min(0f)] float m_HoverHapticDuration = 0.04f;
    MaterialPropertyBlock m_Block;
    Renderer m_GazeShineRenderer;
    Color m_PreShineColor;
    GazeTargetVicinity m_Vicinity;
    FindObjectGameManager m_Game;
    bool m_RightArmed;
    int m_LastCaptureFrame = -1;

    public bool ThroughWallSearch { get; set; }
    public ActivationExercise Exercise { get; set; }
    bool InteractionActive => Application.isFocused &&
        (Exercise != null && Exercise.Active ? Exercise.AcceptingInput : m_Game != null && m_Game.SearchActive);
    IReadOnlyList<GameObject> InteractionObjects => Exercise != null && Exercise.Active ? Exercise.Objects : m_Game?.SpawnedObjects;
    public event Action<GameObject> OnObjectCaptured;

    // A snapshot of the same physical rays used for selection, in world coordinates.
    // Unavailable input is -1 rather than an invented released trigger.
    public struct TelemetrySample
    {
        public bool available, rayAvailable;
        public int tracked, triggerHeld;
        public float triggerValue;
        public Vector3 position, rayOrigin, rayDirection, rayEnd;
        public Quaternion rotation;
        public GameObject target;
    }
    public TelemetrySample LeftTelemetry { get; private set; }
    public TelemetrySample RightTelemetry { get; private set; }
    public int TelemetryFrame { get; private set; } = -1;
    public double TelemetryTime { get; private set; }
    public string SelectingHand { get; private set; }

    TelemetrySample ReadTelemetry(InteractorHandedness hand, bool search)
    {
        var sample = new TelemetrySample { tracked = -1, triggerHeld = -1,
            triggerValue = -1, rotation = Quaternion.identity };
        var device = InputDevices.GetDeviceAtXRNode(hand == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked)) sample.tracked = tracked ? 1 : 0;
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool held)) sample.triggerHeld = held ? 1 : 0;
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float value)) sample.triggerValue = value;
        }
        foreach (var ray in m_Rays)
        {
            if (ray == null || ray.handedness != hand || !ray.isActiveAndEnabled) continue;
            var controller = ray.GetComponentInParent<ControllerInputActionManager>();
            if (controller == null) continue;
            sample.available = true;
            sample.position = controller.transform.position;
            sample.rotation = controller.transform.rotation;
            var origin = ray.farInteractionCaster?.castOrigin;
            sample.rayAvailable = origin != null && ray.enableFarCasting && sample.tracked == 1;
            if (sample.rayAvailable)
            {
                sample.rayOrigin = origin.position;
                sample.rayDirection = origin.forward;
                sample.rayEnd = ((IXRRayProvider)ray).rayEndPoint;
                sample.target = search ? RayTarget(ray) : null;
            }
            break;
        }
        return sample;
    }

    public static void PreventGrab(XRGrabInteractable grab)
    {
        grab.selectFilters.Remove(k_NoGrab);
        grab.selectFilters.Add(k_NoGrab);
    }

    void OnEnable()
    {
        m_Game = GetComponent<FindObjectGameManager>();
        m_Rays.Clear();
        foreach (var ray in FindObjectsOfType<NearFarInteractor>(true))
        {
            // Physical controller rig only: hand rays have no controller action manager.
            if (ray.GetComponentInParent<ControllerInputActionManager>(true) == null ||
                ray.handedness == InteractorHandedness.None) continue;
            m_Rays.Add(ray);
            m_OriginalNearCasting[ray] = ray.enableNearCasting;
            if (ray.farInteractionCaster is CurveInteractionCaster caster)
                m_OriginalMasks[ray] = caster.raycastMask;
        }
        ResetSelection();
    }

    void FixedUpdate()
    {
        // Apply before the physics step, so XRI queries and rendered geometry
        // share the collider pose even with Physics.autoSyncTransforms disabled.
        bool rotating = InteractionActive && GazeTargetVicinity.ControllerCurrent(RightTelemetry,
            TelemetryFrame, TelemetryTime, Time.frameCount, Time.realtimeSinceStartupAsDouble);
        RotateHoveredObject(RightTelemetry.target, rotating, Time.fixedDeltaTime);
    }

    void Update()
    {
        // Set masks before the next XRI cast; restore normal room/UI behavior outside search.
        bool search = InteractionActive;
        foreach (var ray in m_Rays)
        {
            if (ray == null || !m_OriginalMasks.TryGetValue(ray, out int mask)) continue;
            SetMask(ray, search && ThroughWallSearch ? 1 << 8 : mask);
            ray.enableNearCasting = false; // Study interactions use the right far ray, including setup.
        }
    }

    static void SetMask(NearFarInteractor ray, int mask)
    {
        if (ray.farInteractionCaster is CurveInteractionCaster caster) caster.raycastMask = mask;
    }

    void LateUpdate()
    {
        ClearGazeShine();
        ClearHighlights();
        bool search = InteractionActive;
        bool rightPress = ReadPress(XRNode.RightHand, search, ref m_RightArmed);
        // Sample both hands before a capture callback can reset or destroy the trial.
        LeftTelemetry = ReadTelemetry(InteractorHandedness.Left, search);
        RightTelemetry = ReadTelemetry(InteractorHandedness.Right, search);
        TelemetryFrame = Time.frameCount;
        TelemetryTime = Time.realtimeSinceStartupAsDouble;
        if (!search) { m_LastRayTargets.Clear(); return; }
        foreach (var ray in m_Rays)
        {
            if (ray == null || ray.handedness != InteractorHandedness.Right) continue;
            if (!ray.isActiveAndEnabled) { m_LastRayTargets.Remove(ray); continue; }
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked)
            {
                m_LastRayTargets.Remove(ray);
                continue;
            }
            var target = RayTarget(ray);
            if (Exercise != null && Exercise.Active && !Exercise.Contains(target)) target = null;
            m_LastRayTargets.TryGetValue(ray, out var previousTarget);
            m_LastRayTargets[ray] = target;
            if (target == null) continue;
            // One pulse on entry or switching objects on the study's right controller.
            if (target != previousTarget && m_HoverHapticAmplitude > 0f && m_HoverHapticDuration > 0f)
                ray.SendHapticImpulse(m_HoverHapticAmplitude, m_HoverHapticDuration);
            Highlight(target);
            if (rightPress && m_LastCaptureFrame != Time.frameCount)
            {
                m_LastCaptureFrame = Time.frameCount;
                SelectingHand = "right";
                try
                {
                    if (Exercise != null && Exercise.Active) Exercise.Capture(target);
                    else OnObjectCaptured?.Invoke(target);
                }
                finally { SelectingHand = null; }
                // Capture callbacks may destroy objects or pause/end the trial.
                return;
            }
        }
        ApplyGazeShine();
    }

    // Rotate the real object, so collider orientation and recorded replay poses
    // agree. Position/scale and stimulus identity stay fixed. Stop in place on exit.
    static void RotateHoveredObject(GameObject target, bool eligible, float seconds)
    {
        if (!eligible || target == null || !target.activeInHierarchy || !float.IsFinite(seconds) || seconds <= 0 ||
            !target.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled ||
            !target.TryGetComponent<Collider>(out var collider) || !collider.enabled) return;
        target.transform.Rotate(Vector3.up, 60f * Mathf.Min(seconds, .1f), Space.World);
    }

    void ApplyGazeShine()
    {
        if (SharedGazeStream.Instance == null || !SharedGazeStream.Instance.TryGetValid(out var gaze)) return;
        if (m_Vicinity == null) m_Vicinity = GetComponent<GazeTargetVicinity>();
        var winner = GazeTargetVicinity.ClosestObject(gaze.worldOrigin, gaze.worldDirection,
            InteractionObjects, m_Vicinity != null ? m_Vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees,
            out _, out _, out _);
        Shine(winner);
    }

    void Shine(GameObject target)
    {
        if (target == null || !target.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled ||
            renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(k_BaseColor)) return;
        m_Block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(m_Block);
        m_PreShineColor = m_Block.HasColor(k_BaseColor) ? m_Block.GetColor(k_BaseColor) : renderer.sharedMaterial.GetColor(k_BaseColor);
        // Lift luminance, preserving the object's hue and alpha. The shader's
        // emissive rim makes gaze visible without replacing its stimulus color.
        Color shine = m_PreShineColor * 1.6f; shine.a = m_PreShineColor.a;
        m_Block.SetColor(k_BaseColor, shine);
        // Keep the familiar blue rim when the right controller also points here.
        if (m_Highlighted.Add(renderer))
        {
            m_Block.SetColor(k_EdgeColor, new Color(1f, .79f, .016f, 1f));
            m_Block.SetFloat(k_Falloff, 1.5f);
        }
        renderer.SetPropertyBlock(m_Block);
        m_GazeShineRenderer = renderer;
    }

    void ClearGazeShine()
    {
        if (m_GazeShineRenderer != null)
        {
            m_GazeShineRenderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, m_PreShineColor);
            m_GazeShineRenderer.SetPropertyBlock(m_Block);
        }
        m_GazeShineRenderer = null;
    }

    static bool ReadPress(XRNode node, bool search, ref bool armed)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        bool valid = device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        bool pressed = false;
        valid = valid && device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);
        return ConsumePress(valid, pressed, search, ref armed);
    }

    static bool ConsumePress(bool valid, bool pressed, bool search, ref bool armed)
    {
        if (!valid || !search) { armed = false; return false; }
        if (!pressed) { armed = true; return false; }
        bool confirm = armed;
        armed = false;
        return confirm;
    }

    static GameObject RayTarget(NearFarInteractor ray)
    {
        if (!ray.enableFarCasting || ray.TryGetCurveEndPoint(out _) != EndPointType.ValidCastHit)
            return null; // Includes UI hits, no hit, and invalid/occluded objects.
        var hit = ((IXRRayProvider)ray).rayEndTransform;
        if (hit == null) return null;
        var info = hit.GetComponentInParent<SpawnableObjectInfo>();
        return info != null && info.gameObject.layer == 8 && info.gameObject.activeInHierarchy
            ? info.gameObject : null;
    }

    void Highlight(GameObject target)
    {
        m_Block ??= new MaterialPropertyBlock();
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !m_Highlighted.Add(renderer)) continue;
            renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_EdgeColor, new Color(0.7f, 0.87f, 1f, 1f));
            m_Block.SetFloat(k_Falloff, 1.5f);
            renderer.SetPropertyBlock(m_Block);
        }
    }

    void ClearHighlights()
    {
        foreach (var renderer in m_Highlighted)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_EdgeColor, Color.clear);
            renderer.SetPropertyBlock(m_Block);
        }
        m_Highlighted.Clear();
    }

    public void ResetSelection()
    {
        m_RightArmed = false;
        m_LastRayTargets.Clear();
        ClearGazeShine(); ClearHighlights();
    }

    void OnDisable()
    {
        ResetSelection();
        foreach (var entry in m_OriginalMasks)
            if (entry.Key != null) SetMask(entry.Key, entry.Value);
        foreach (var entry in m_OriginalNearCasting)
            if (entry.Key != null) entry.Key.enableNearCasting = entry.Value;
        m_OriginalNearCasting.Clear();
        m_OriginalMasks.Clear();
    }
}
