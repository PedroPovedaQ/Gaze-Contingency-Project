using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Eye-gaze dwell selector: looking at an object charges a progressive glow
/// over 1.6 seconds, then "captures" it with a satisfying boop sound.
/// Replaces controller-based grab interaction entirely.
/// Runs at high execution order to override any residual controller highlights.
/// </summary>
[DefaultExecutionOrder(200)]
public class GazeHighlightManager : MonoBehaviour
{
    static readonly int k_EdgeHighlightColor = Shader.PropertyToID("_EdgeHighlightColor");
    static readonly int k_EdgeHighlightFalloff = Shader.PropertyToID("_EdgeHighlightFalloff");
    static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
    const string k_Tag = "[GazeDwell]";
    const float k_DwellDuration = 1.6f;
    const float k_CaptureFlashDuration = 0.25f;

    // Glow ramp: dim warm orange → bright HDR yellow-white
    static readonly Color k_GlowStart = new Color(1f, 0.5f, 0.1f, 0.4f);
    static readonly Color k_GlowEnd = new Color(1f, 0.95f, 0.5f, 1f) * 2.5f;
    static readonly Color k_CaptureFlash = new Color(1f, 1f, 1f, 1f) * 3f;

    // Falloff ramp: tight rim → wide full-object glow
    const float k_FalloffStart = 4f;
    const float k_FalloffEnd = 0.6f;

    // Idle glow for non-target hovered objects
    static readonly Color k_IdleGlow = new Color(1f, 0.5f, 0.1f, 0.25f);
    const float k_IdleFalloff = 4.5f;

    /// <summary>Fires when an object is captured via gaze dwell.</summary>
    public event System.Action<GameObject> OnObjectCaptured;

    /// <summary>Current dwell progress 0–1 (for analytics/logging).</summary>
    public float DwellProgress => m_DwellTarget != null && !m_CapturedThisTarget
        ? Mathf.Clamp01(m_DwellTime / k_DwellDuration) : 0f;

    /// <summary>The object currently being dwelled on, or null.</summary>
    public GameObject DwellTarget => m_DwellTarget;

    XRBaseInputInteractor m_GazeInteractor;
    MaterialPropertyBlock m_PropertyBlock;

    // Dwell state
    GameObject m_DwellTarget;
    float m_DwellTime;
    bool m_CapturedThisTarget;
    bool m_Locked; // prevents new captures during flash

    // Capture flash
    Renderer[] m_FlashRenderers;
    float m_FlashTimer;

    // Track highlighted renderers for cleanup
    readonly HashSet<Renderer> m_PrevHighlighted = new HashSet<Renderer>();
    readonly HashSet<Renderer> m_CurrentHighlighted = new HashSet<Renderer>();

    // Audio
    AudioSource m_AudioSource;
    AudioClip m_BoopClip;

    void OnEnable()
    {
        m_PropertyBlock = new MaterialPropertyBlock();
        m_GazeInteractor = GetComponent<XRBaseInputInteractor>();
        if (m_GazeInteractor == null)
        {
            Debug.LogWarning($"{k_Tag} No XR interactor found on {gameObject.name}");
            return;
        }

        CreateBoopSound();
        m_AudioSource = gameObject.AddComponent<AudioSource>();
        m_AudioSource.spatialBlend = 0f;
        m_AudioSource.playOnAwake = false;
        m_AudioSource.volume = 0.65f;

        Debug.Log($"{k_Tag} Initialized gaze dwell selector ({k_DwellDuration}s) on {gameObject.name}");
    }

    void CreateBoopSound()
    {
        int sampleRate = 44100;
        float duration = 0.2f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            // Rising frequency sweep for a playful "boop"
            float freq = Mathf.Lerp(520f, 980f, t * t);
            // Bell-curve envelope with slight decay
            float envelope = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.3f);
            // Add subtle harmonic for richness
            float fundamental = Mathf.Sin(2f * Mathf.PI * freq * t);
            float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.15f;
            samples[i] = (fundamental + harmonic) * envelope * 0.45f;
        }

        m_BoopClip = AudioClip.Create("Boop", sampleCount, 1, sampleRate, false);
        m_BoopClip.SetData(samples, 0);
    }

    void LateUpdate()
    {
        if (m_GazeInteractor == null) return;

        // Handle capture flash animation
        if (m_FlashTimer > 0f)
        {
            m_FlashTimer -= Time.deltaTime;
            if (m_FlashTimer <= 0f)
            {
                ClearFlash();
                m_Locked = false;
            }
            else
            {
                float flashT = m_FlashTimer / k_CaptureFlashDuration;
                ApplyFlash(Color.Lerp(Color.clear, k_CaptureFlash, flashT));
            }
        }

        m_CurrentHighlighted.Clear();

        var hovered = m_GazeInteractor.interactablesHovered;

        // Find primary hovered spawned object (layer 8)
        // Guard against destroyed interactables left in the hover list
        GameObject hoveredObj = null;
        for (int h = 0; h < hovered.Count; h++)
        {
            var interactable = hovered[h];
            if (interactable == null || interactable.transform == null) continue;
            var candidate = interactable.transform.gameObject;
            if (candidate == null || !candidate.activeInHierarchy) continue;
            if (candidate.layer == 8)
            {
                hoveredObj = candidate;
                break;
            }
        }

        // Update dwell state
        if (!m_Locked)
        {
            if (hoveredObj != null && hoveredObj == m_DwellTarget && !m_CapturedThisTarget)
            {
                m_DwellTime += Time.deltaTime;

                if (m_DwellTime >= k_DwellDuration)
                    CaptureObject(hoveredObj);
            }
            else if (hoveredObj != m_DwellTarget)
            {
                m_DwellTarget = hoveredObj;
                m_DwellTime = 0f;
                m_CapturedThisTarget = false;
            }
        }

        // Apply glow to all hovered spawned objects
        for (int i = 0; i < hovered.Count; i++)
        {
            var interactable = hovered[i];
            if (interactable == null || interactable.transform == null) continue;
            var obj = interactable.transform.gameObject;
            if (obj == null || !obj.activeInHierarchy) continue;
            if (obj.layer != 8) continue;

            // Get root renderer first (most reliable), then any children
            var rootRenderer = interactable.transform.GetComponent<Renderer>();
            var childRenderers = interactable.transform.GetComponentsInChildren<Renderer>();
            bool isDwellTarget = obj == m_DwellTarget;
            float progress = isDwellTarget ? Mathf.Clamp01(m_DwellTime / k_DwellDuration) : 0f;

            foreach (var r in childRenderers)
            {
                // Include the root renderer even if children are disabled
                if (r == null) continue;
                if (!r.enabled && r != rootRenderer) continue;
                m_CurrentHighlighted.Add(r);

                // Flash overrides glow
                if (m_CapturedThisTarget && isDwellTarget) continue;

                r.GetPropertyBlock(m_PropertyBlock);

                if (isDwellTarget && !m_CapturedThisTarget)
                {
                    // Progressive charge-up glow
                    Color glowColor = Color.Lerp(k_GlowStart, k_GlowEnd, progress);
                    float falloff = Mathf.Lerp(k_FalloffStart, k_FalloffEnd, progress);
                    m_PropertyBlock.SetColor(k_EdgeHighlightColor, glowColor);
                    m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, falloff);
                    // Fallback for URP/Lit materials that don't have edge highlight
                    m_PropertyBlock.SetColor(k_EmissionColor, glowColor * 0.5f);
                }
                else
                {
                    // Subtle idle glow for non-target hover
                    m_PropertyBlock.SetColor(k_EdgeHighlightColor, k_IdleGlow);
                    m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, k_IdleFalloff);
                    m_PropertyBlock.SetColor(k_EmissionColor, k_IdleGlow * 0.3f);
                }

                r.SetPropertyBlock(m_PropertyBlock);
            }
        }

        // Clear renderers no longer hovered
        foreach (var r in m_PrevHighlighted)
        {
            if (r == null) continue;
            if (m_CurrentHighlighted.Contains(r)) continue;

            r.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
            m_PropertyBlock.SetColor(k_EmissionColor, Color.black);
            r.SetPropertyBlock(m_PropertyBlock);
        }

        m_PrevHighlighted.Clear();
        foreach (var r in m_CurrentHighlighted)
            m_PrevHighlighted.Add(r);
    }

    void CaptureObject(GameObject obj)
    {
        m_CapturedThisTarget = true;
        m_Locked = true;
        Debug.Log($"{k_Tag} Captured: {obj.name}");

        // Play boop
        if (m_AudioSource != null && m_BoopClip != null)
            m_AudioSource.PlayOneShot(m_BoopClip);

        // Start capture flash
        m_FlashRenderers = obj.GetComponentsInChildren<Renderer>();
        m_FlashTimer = k_CaptureFlashDuration;
        ApplyFlash(k_CaptureFlash);

        OnObjectCaptured?.Invoke(obj);
    }

    void ApplyFlash(Color color)
    {
        if (m_FlashRenderers == null) return;
        foreach (var r in m_FlashRenderers)
        {
            if (r == null || !r.enabled) continue;
            r.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, color);
            m_PropertyBlock.SetFloat(k_EdgeHighlightFalloff, 0.3f);
            r.SetPropertyBlock(m_PropertyBlock);
        }
    }

    void ClearFlash()
    {
        if (m_FlashRenderers == null) return;
        foreach (var r in m_FlashRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_EdgeHighlightColor, Color.clear);
            r.SetPropertyBlock(m_PropertyBlock);
        }
        m_FlashRenderers = null;
    }

    /// <summary>
    /// Fully resets all state — dwell, flash, highlight tracking.
    /// Call between rounds when objects are destroyed and replaced.
    /// </summary>
    public void ResetDwell()
    {
        m_DwellTarget = null;
        m_DwellTime = 0f;
        m_CapturedThisTarget = false;
        m_Locked = false;
        m_FlashTimer = 0f;
        m_FlashRenderers = null;
        m_PrevHighlighted.Clear();
        m_CurrentHighlighted.Clear();
    }
}
