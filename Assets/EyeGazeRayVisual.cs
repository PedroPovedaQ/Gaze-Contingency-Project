using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Configures an orange XRInteractorLineVisual on the Gaze Interactor
/// to visually distinguish eye gaze from cyan controller rays.
/// </summary>
public class EyeGazeRayVisual : MonoBehaviour
{
    static readonly Color k_OrangeColor = new Color(1f, 0.5f, 0f, 1f);

    [Tooltip("Flip the X position to correct left/right eye swap on VIVE.")]
    [SerializeField] bool m_FlipX = true;

    const string k_Tag = "[EyeGazeRay]";

    void Awake()
    {
        Debug.Log($"{k_Tag} Awake on {gameObject.name}");

        var lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.widthMultiplier = 0.02f;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 4;
        lineRenderer.useWorldSpace = true;

        var lineVisual = GetComponent<XRInteractorLineVisual>();
        if (lineVisual == null)
            lineVisual = gameObject.AddComponent<XRInteractorLineVisual>();

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

        Debug.Log($"{k_Tag} Setup complete. LineRenderer={lineRenderer != null}, LineVisual={lineVisual != null}");
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

    void LateUpdate()
    {
        if (m_FlipX)
        {
            var pos = transform.localPosition;
            pos.x = -pos.x;
            transform.localPosition = pos;
        }
    }

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
