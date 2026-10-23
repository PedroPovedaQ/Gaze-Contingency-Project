using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Templates.MR;
using TMPro;
using VIVE.OpenXR;
using VIVE.OpenXR.Passthrough;
using VIVE.OpenXR.CompositionLayer;

/// <summary>Connect the existing wrist passthrough switch to HTC's native underlay.</summary>
public class VivePassthrough : MonoBehaviour
{
    VIVE.OpenXR.Passthrough.XrPassthroughHTC m_Handle;
    bool m_Created;
    float m_RetryTimer;
    ARFeatureController m_Features;
    Camera m_Camera;
    CameraClearFlags m_OriginalClearFlags;
    Color m_OriginalBackground;
    public bool PassthroughEnabled { get; private set; } = true;
    public bool IsReady => m_Created && PassthroughEnabled;

    void Start()
    {
        BindFeatureController(FindFirstObjectByType<ARFeatureController>());
        foreach (var toggle in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!toggle.name.Contains("Passthrough")) continue;
            var label = toggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Mixed reality";
            RayMenuFeedback.Attach(toggle);
        }
        // The study bypasses template onboarding, which formerly faded away
        // the virtual environment. Explicitly reveal the room at startup.
        SetPassthroughEnabled(PassthroughEnabled);
    }

    void BindFeatureController(ARFeatureController features)
    {
        if (m_Features != null) m_Features.onARPassthroughFeatureChanged.RemoveListener(ApplyPassthrough);
        m_Features = features;
        if (m_Features != null) m_Features.onARPassthroughFeatureChanged.AddListener(ApplyPassthrough);
    }

    public void SetPassthroughEnabled(bool visible)
    {
        ApplyPassthrough(visible);
        // Synchronize the existing toggle and template environment fade. The
        // listener only applies state; it never calls this method recursively.
        m_Features?.TogglePassthrough(visible);
    }

    void ApplyPassthrough(bool visible)
    {
        PassthroughEnabled = visible;
        ApplyCameraBackground();
        if (m_Created && !PassthroughAPI.SetPassthroughAlpha(m_Handle, visible ? 1f : 0f))
        {
            ReleaseLayer(); // A stale session handle can be recreated on the next update.
        }
        m_RetryTimer = 0;
        Debug.Log($"[VivePassthrough] Mixed reality requested: {visible}");
    }

    void ApplyCameraBackground()
    {
        var camera = Camera.main;
        if (camera == null) return;
        if (camera != m_Camera)
        {
            RestoreCamera();
            m_Camera = camera;
            m_OriginalClearFlags = camera.clearFlags;
            m_OriginalBackground = camera.backgroundColor;
        }
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = PassthroughEnabled ? Color.clear : Color.black;
    }

    void Update()
    {
        if (Camera.main != m_Camera) ApplyCameraBackground();
        if (!PassthroughEnabled || m_Created) return;
        m_RetryTimer -= Time.unscaledDeltaTime;
        if (m_RetryTimer > 0) return;
        m_RetryTimer = 2;
        var result = PassthroughAPI.CreatePlanarPassthrough(out m_Handle, LayerType.Underlay,
            alpha: 1f, compositionDepth: 0u);
        m_Created = result == XrResult.XR_SUCCESS;
        Debug.Log($"[VivePassthrough] Native underlay creation: {result}");
    }

    void ReleaseLayer()
    {
        if (m_Created) PassthroughAPI.DestroyPassthrough(m_Handle);
        m_Created = false; m_Handle = 0;
    }
    void RestoreCamera()
    {
        if (m_Camera == null) return;
        m_Camera.clearFlags = m_OriginalClearFlags;
        m_Camera.backgroundColor = m_OriginalBackground;
        m_Camera = null;
    }
    void OnEnable()
    {
        if (m_Features == null) return; // Initial binding happens in Start.
        BindFeatureController(m_Features); SetPassthroughEnabled(PassthroughEnabled);
    }
    void OnDisable()
    {
        if (m_Features != null) m_Features.onARPassthroughFeatureChanged.RemoveListener(ApplyPassthrough);
        ReleaseLayer(); RestoreCamera();
    }
    void OnDestroy()
    {
        if (m_Features != null) m_Features.onARPassthroughFeatureChanged.RemoveListener(ApplyPassthrough);
        ReleaseLayer(); RestoreCamera();
    }
}
