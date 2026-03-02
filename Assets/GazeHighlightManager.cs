using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Applies an orange highlight to objects the eye gaze hovers over.
/// Overrides the existing _EdgeHighlightColor (controller blue) with orange.
/// Runs after the affordance system via execution order to take precedence.
/// </summary>
[DefaultExecutionOrder(100)]
public class GazeHighlightManager : MonoBehaviour
{
    static readonly int k_EdgeHighlightColor = Shader.PropertyToID("_EdgeHighlightColor");
    static readonly int k_EdgeHighlightFalloff = Shader.PropertyToID("_EdgeHighlightFalloff");
    static readonly Color k_GazeHighlightColor = new Color(1f, 0.5f, 0f, 1f);
    const float k_GazeHighlightFalloff = 1.5f;
    const string k_Tag = "[GazeHighlight]";

    [SerializeField] bool m_DebugLog;

    XRBaseInputInteractor m_GazeInteractor;
    readonly HashSet<Renderer> m_HighlightedRenderers = new HashSet<Renderer>();
    MaterialPropertyBlock m_PropertyBlock;

    void OnEnable()
    {
        m_PropertyBlock = new MaterialPropertyBlock();
        m_GazeInteractor = GetComponent<XRBaseInputInteractor>();
        if (m_GazeInteractor == null)
        {
            Debug.LogWarning($"{k_Tag} No XR interactor found on {gameObject.name}");
            return;
        }

        Debug.Log($"{k_Tag} Initialized on {gameObject.name}, interactor={m_GazeInteractor.GetType().Name}");

        m_GazeInteractor.hoverEntered.AddListener(OnGazeHoverEntered);
        m_GazeInteractor.hoverExited.AddListener(OnGazeHoverExited);

        if (m_DebugLog)
            InvokeRepeating(nameof(LogState), 3f, 5f);
    }

    void OnDisable()
    {
        if (m_GazeInteractor != null)
        {
            m_GazeInteractor.hoverEntered.RemoveListener(OnGazeHoverEntered);
            m_GazeInteractor.hoverExited.RemoveListener(OnGazeHoverExited);
        }

        ClearAllHighlights();
    }

    void OnGazeHoverEntered(HoverEnterEventArgs args)
    {
        var obj = args.interactableObject;
        if (m_DebugLog)
            Debug.Log($"{k_Tag} HOVER ENTER: {obj.transform.name}");

        // Skip objects that have their own InteractableHighlight component
        if (obj.transform.GetComponent<InteractableHighlight>() != null)
            return;

        var renderers = obj.transform.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            m_HighlightedRenderers.Add(r);
    }

    void OnGazeHoverExited(HoverExitEventArgs args)
    {
        var obj = args.interactableObject;
        if (m_DebugLog)
            Debug.Log($"{k_Tag} HOVER EXIT: {obj.transform.name}");

        var renderers = obj.transform.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            m_HighlightedRenderers.Remove(r);
    }

    void LateUpdate()
    {
        foreach (var r in m_HighlightedRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, k_GazeHighlightColor);
            m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, k_GazeHighlightFalloff);
            r.SetPropertyBlock(m_PropertyBlock);
        }
    }

    void ClearAllHighlights()
    {
        foreach (var r in m_HighlightedRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
            r.SetPropertyBlock(m_PropertyBlock);
        }
        m_HighlightedRenderers.Clear();
    }

    void LogState()
    {
        if (m_GazeInteractor == null) return;
        Debug.Log($"{k_Tag} state: hoveredByInteractor={m_GazeInteractor.interactablesHovered.Count}, " +
            $"highlightedRenderers={m_HighlightedRenderers.Count}");
    }
}
