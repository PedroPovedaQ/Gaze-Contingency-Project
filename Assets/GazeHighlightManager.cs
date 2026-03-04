using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Applies an orange highlight to objects the eye gaze hovers over.
/// Polls interactablesHovered every frame in LateUpdate instead of
/// relying on events, ensuring it works for dynamically spawned objects.
/// Runs at high execution order to override controller blue highlights.
/// </summary>
[DefaultExecutionOrder(200)]
public class GazeHighlightManager : MonoBehaviour
{
    static readonly int k_EdgeHighlightColor = Shader.PropertyToID("_EdgeHighlightColor");
    static readonly int k_EdgeHighlightFalloff = Shader.PropertyToID("_EdgeHighlightFalloff");
    static readonly Color k_GazeHighlightColor = new Color(1f, 0.5f, 0f, 1f);
    const float k_GazeHighlightFalloff = 1.5f;
    const string k_Tag = "[GazeHighlight]";

    XRBaseInputInteractor m_GazeInteractor;
    MaterialPropertyBlock m_PropertyBlock;
    float m_NextLogTime;

    // Track previously highlighted renderers so we can clear them
    readonly HashSet<Renderer> m_PrevHighlighted = new HashSet<Renderer>();
    readonly HashSet<Renderer> m_CurrentHighlighted = new HashSet<Renderer>();

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
    }

    void LateUpdate()
    {
        if (m_GazeInteractor == null) return;

        m_CurrentHighlighted.Clear();

        // Gather all renderers from currently gaze-hovered interactables
        var hovered = m_GazeInteractor.interactablesHovered;

        // Periodic debug log to confirm polling is running and see hover count
        if (Time.time > m_NextLogTime)
        {
            m_NextLogTime = Time.time + 3f;
            if (hovered.Count > 0)
            {
                string names = "";
                for (int i = 0; i < hovered.Count; i++)
                    names += (i > 0 ? ", " : "") + hovered[i]?.transform.name;
                Debug.Log($"{k_Tag} Gaze hovering {hovered.Count}: {names}");
            }
        }

        for (int i = 0; i < hovered.Count; i++)
        {
            var interactable = hovered[i];
            if (interactable == null) continue;

            var renderers = interactable.transform.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                m_CurrentHighlighted.Add(r);

                // Apply orange highlight
                r.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(k_EdgeHighlightColor, k_GazeHighlightColor);
                m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, k_GazeHighlightFalloff);
                r.SetPropertyBlock(m_PropertyBlock);
            }
        }

        // Clear renderers that were highlighted last frame but aren't anymore
        foreach (var r in m_PrevHighlighted)
        {
            if (r == null) continue;
            if (m_CurrentHighlighted.Contains(r)) continue;

            r.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
            r.SetPropertyBlock(m_PropertyBlock);
        }

        // Swap sets
        m_PrevHighlighted.Clear();
        foreach (var r in m_CurrentHighlighted)
            m_PrevHighlighted.Add(r);
    }
}
