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
    static readonly XRSelectFilterDelegate k_NoGrab = new XRSelectFilterDelegate((_, __) => false);
    readonly List<NearFarInteractor> m_Rays = new();
    readonly Dictionary<NearFarInteractor, int> m_OriginalMasks = new();
    readonly Dictionary<NearFarInteractor, bool> m_OriginalNearCasting = new();
    readonly HashSet<Renderer> m_Highlighted = new();
    readonly Dictionary<NearFarInteractor, GameObject> m_LastRayTargets = new();
    [SerializeField, Range(0f, 1f)] float m_HoverHapticAmplitude = 0.2f;
    [SerializeField, Min(0f)] float m_HoverHapticDuration = 0.04f;
    MaterialPropertyBlock m_Block;
    FindObjectGameManager m_Game;
    bool m_LeftArmed, m_RightArmed;
    int m_LastCaptureFrame = -1;

    public bool ThroughWallSearch { get; set; }
    public event Action<GameObject> OnObjectCaptured;

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

    void Update()
    {
        // Set masks before the next XRI cast; restore normal room/UI behavior outside search.
        bool search = m_Game != null && m_Game.SearchActive;
        foreach (var ray in m_Rays)
        {
            if (ray == null || !m_OriginalMasks.TryGetValue(ray, out int mask)) continue;
            SetMask(ray, search && ThroughWallSearch ? 1 << 8 : mask);
            ray.enableNearCasting = !search && m_OriginalNearCasting[ray];
        }
    }

    static void SetMask(NearFarInteractor ray, int mask)
    {
        if (ray.farInteractionCaster is CurveInteractionCaster caster) caster.raycastMask = mask;
    }

    void LateUpdate()
    {
        ClearHighlights();
        bool search = m_Game != null && m_Game.SearchActive;
        bool leftPress = ReadPress(XRNode.LeftHand, search, ref m_LeftArmed);
        bool rightPress = ReadPress(XRNode.RightHand, search, ref m_RightArmed);
        if (!search) { m_LastRayTargets.Clear(); return; }
        foreach (var ray in m_Rays)
        {
            if (ray == null) continue;
            if (!ray.isActiveAndEnabled) { m_LastRayTargets.Remove(ray); continue; }
            bool left = ray.handedness == InteractorHandedness.Left;
            var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            if (!device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked)
            {
                m_LastRayTargets.Remove(ray);
                continue;
            }
            var target = RayTarget(ray);
            m_LastRayTargets.TryGetValue(ray, out var previousTarget);
            m_LastRayTargets[ray] = target;
            if (target == null) continue;
            // One pulse on entry or switching objects, independently for each controller.
            if (target != previousTarget && m_HoverHapticAmplitude > 0f && m_HoverHapticDuration > 0f)
                ray.SendHapticImpulse(m_HoverHapticAmplitude, m_HoverHapticDuration);
            Highlight(target);
            if ((left ? leftPress : rightPress) && m_LastCaptureFrame != Time.frameCount)
            {
                m_LastCaptureFrame = Time.frameCount;
                OnObjectCaptured?.Invoke(target);
                // Capture callbacks may destroy objects or pause/end the trial.
                return;
            }
        }
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
        m_LeftArmed = m_RightArmed = false;
        m_LastRayTargets.Clear();
        ClearHighlights();
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
