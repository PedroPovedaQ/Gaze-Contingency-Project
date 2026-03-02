using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Per-object highlight component. Subscribes to the object's own
/// XRGrabInteractable hover events and applies _EdgeHighlightColor
/// via MaterialPropertyBlock.
///
/// Gaze hover = orange, controller hover = blue, gaze takes precedence.
/// No dependency on the affordance system (which was on destroyed children).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class InteractableHighlight : MonoBehaviour
{
    static readonly int k_EdgeHighlightColor = Shader.PropertyToID("_EdgeHighlightColor");
    static readonly int k_EdgeHighlightFalloff = Shader.PropertyToID("_EdgeHighlightFalloff");

    static readonly Color k_GazeColor = new Color(1f, 0.5f, 0f, 1f);       // orange
    static readonly Color k_ControllerColor = new Color(0.7f, 0.87f, 1f, 1f); // light blue
    const float k_Falloff = 1.5f;

    XRGrabInteractable m_Grab;
    MeshRenderer m_Renderer;
    MaterialPropertyBlock m_PropertyBlock;
    bool m_IsGazeHovering;
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
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (args.interactorObject is XRGazeInteractor)
            m_IsGazeHovering = true;
        else
            m_IsControllerHovering = true;

        ApplyHighlight();
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject is XRGazeInteractor)
            m_IsGazeHovering = false;
        else
            m_IsControllerHovering = false;

        ApplyHighlight();
    }

    void ApplyHighlight()
    {
        if (m_Renderer == null) return;

        m_Renderer.GetPropertyBlock(m_PropertyBlock);

        if (m_IsGazeHovering)
        {
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, k_GazeColor);
            m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, k_Falloff);
        }
        else if (m_IsControllerHovering)
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
