using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Retained as the serialized gaze anchor for telemetry and hint consumers.
/// ControllerRaySelector owns ring-vicinity shine and controller selection.
/// This anchor never selects objects or implements a gaze-dwell loop.
/// </summary>
public class GazeHighlightManager : MonoBehaviour
{
    public void ConfigureThroughWallSearch()
    {
        var ray = GetComponent<XRRayInteractor>();
        if (ray == null) return;
        ray.raycastMask = 1 << 8;
        ray.enableUIInteraction = false;
        ray.hitClosestOnly = true;
    }
}
