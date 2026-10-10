using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Configures an orange XRInteractorLineVisual on the Gaze Interactor
/// to visually distinguish eye gaze from cyan controller rays.
/// </summary>
public class EyeGazeRayVisual : MonoBehaviour
{
    static readonly Color k_OrangeColor = new Color(1f, 0.5f, 0f, 1f);

    [Tooltip("Legacy serialized flag; ignored. Test origin mirroring in SharedGazeStream diagnostics.")]
    [SerializeField] bool m_FlipX = false;

    const string k_Tag = "[EyeGazeRay]";
    LineRenderer m_Line;
    XRInteractorLineVisual m_Visual;

    void OnEnable() => Application.onBeforeRender += DrawSharedRay;
    void OnDisable() => Application.onBeforeRender -= DrawSharedRay;

    // Run after XRI's line update, preserving the existing wrist toggle but showing
    // the same validity-aware, optionally aligned ray that drives study guidance.
    // Never write to the interactor/TrackedPoseDriver transform.
    [BeforeRenderOrder(10000)]
    void DrawSharedRay()
    {
        if (m_Line == null || m_Visual == null || !m_Visual.enabled) return;
        var stream = SharedGazeStream.Instance;
        if (stream == null || !stream.TryGetValid(out var sample)) { m_Line.enabled = false; return; }
        var end = sample.worldOrigin + sample.worldDirection * 10;
        if (Physics.Raycast(sample.worldOrigin, sample.worldDirection, out var hit, 10, 1 << 8, QueryTriggerInteraction.Ignore))
            end = hit.point;
        m_Line.useWorldSpace = true; m_Line.positionCount = 2;
        m_Line.SetPosition(0, sample.worldOrigin); m_Line.SetPosition(1, end); m_Line.enabled = true;
    }

    void Awake()
    {
        Debug.Log($"{k_Tag} Awake on {gameObject.name}");

        var lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        m_Line = lineRenderer;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.enabled = false; // gaze ray hidden by default
        lineRenderer.widthMultiplier = 0.02f;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 4;
        lineRenderer.useWorldSpace = true;

        var lineVisual = GetComponent<XRInteractorLineVisual>();
        if (lineVisual == null)
            lineVisual = gameObject.AddComponent<XRInteractorLineVisual>();
        m_Visual = lineVisual;

        lineVisual.lineWidth = 0.005f;
        lineVisual.overrideInteractorLineLength = true;
        lineVisual.lineLength = 10f;
        lineVisual.autoAdjustLineLength = true;
        lineVisual.minLineLength = 0.5f;
        lineVisual.useDistanceToHitAsMaxLineLength = true;
        lineVisual.stopLineAtFirstRaycastHit = true;
        lineVisual.stopLineAtSelection = true;
        lineVisual.treatSelectionAsValidState = true;

        lineVisual.setLineColorGradient = true;

        var alwaysOnGradient = new Gradient();
        alwaysOnGradient.SetKeys(
            new[] { new GradientColorKey(k_OrangeColor, 0f), new GradientColorKey(k_OrangeColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        lineVisual.validColorGradient = alwaysOnGradient;
        lineVisual.invalidColorGradient = alwaysOnGradient;
        lineVisual.blockedColorGradient = alwaysOnGradient;

        // Gaze ray hidden by default — gaze tracking still works, just no visible ray
        lineVisual.enabled = false;

        Debug.Log($"{k_Tag} Setup complete, ray hidden by default");
    }

    void Start()
    {
        var interactor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>();
        if (interactor != null)
            Debug.Log($"{k_Tag} Interactor found: {interactor.GetType().Name}, hasSelection={interactor.hasSelection}");
        else
            Debug.LogWarning($"{k_Tag} No XR interactor found on {gameObject.name}");

        var tpd = GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        if (tpd != null)
            Debug.Log($"{k_Tag} TrackedPoseDriver found, trackingType={tpd.trackingType}");
        else
            Debug.LogWarning($"{k_Tag} No TrackedPoseDriver found");

        InvokeRepeating(nameof(LogTrackingState), 2f, 5f);
    }

    // Measurement coordinates belong to SharedGazeStream. A visual must never
    // mutate the tracked pose after logging or apply an origin flip every frame.
    void LogTrackingState()
    {
        var t = transform;
        var lr = GetComponent<LineRenderer>();
        var lv = GetComponent<XRInteractorLineVisual>();
        Debug.Log($"{k_Tag} pos={t.position}, rot={t.rotation.eulerAngles}, active={gameObject.activeInHierarchy}" +
            $", LR.posCount={lr?.positionCount}, LR.enabled={lr?.enabled}, LR.mat={lr?.material?.shader?.name}" +
            $", LV.enabled={lv?.enabled}");
    }
}
