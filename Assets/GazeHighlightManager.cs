using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Retained as the serialized gaze anchor for telemetry and hint consumers.
/// Gaze never highlights or selects search objects. Selection belongs to ControllerRaySelector.
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
