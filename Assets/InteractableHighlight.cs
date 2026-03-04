using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Per-object highlight for CONTROLLER hover (blue glow).
/// Gaze orange glow is handled by GazeHighlightManager (higher execution order).
/// Replaces the destroyed affordance children.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(XRGrabInteractable))]
public class InteractableHighlight : MonoBehaviour
{
    static readonly int k_EdgeHighlightColor = Shader.PropertyToID("_EdgeHighlightColor");
    static readonly int k_EdgeHighlightFalloff = Shader.PropertyToID("_EdgeHighlightFalloff");

    static readonly Color k_ControllerColor = new Color(0.7f, 0.87f, 1f, 1f); // light blue
    const float k_Falloff = 1.5f;

    XRGrabInteractable m_Grab;
    MeshRenderer m_Renderer;
    MaterialPropertyBlock m_PropertyBlock;
    bool m_IsControllerHovering;

    void OnEnable()
    {
        m_Grab = GetComponent<XRGrabInteractable>();
        m_Renderer = GetComponent<MeshRenderer>();
        m_PropertyBlock = new MaterialPropertyBlock();

        if (m_Grab != null)
        {
            m_Grab.hoverEntered.AddListener(OnHoverEntered);
            m_Grab.hoverExited.AddListener(OnHoverExited);
        }
    }

    void OnDisable()
    {
        if (m_Grab != null)
        {
            m_Grab.hoverEntered.RemoveListener(OnHoverEntered);
            m_Grab.hoverExited.RemoveListener(OnHoverExited);
        }

        if (m_Renderer != null && m_PropertyBlock != null)
        {
            m_Renderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        // Ignore gaze hovers — GazeHighlightManager handles those
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
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, k_ControllerColor);
            m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, k_Falloff);
        }
        else
        {
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
        }

        m_Renderer.SetPropertyBlock(m_PropertyBlock);
    }
}
