using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Templates.MR;

/// <summary>
/// Wires a Toggle's onValueChanged to ARFeatureController.ToggleGazeRay at runtime.
/// Place on the same GameObject as the Toggle component.
/// </summary>
public class GazeToggleConnector : MonoBehaviour
{
    [SerializeField] ARFeatureController m_FeatureController;

    void Start()
    {
        var toggle = GetComponent<Toggle>();
        if (toggle != null && m_FeatureController != null)
        {
            toggle.onValueChanged.AddListener(m_FeatureController.ToggleGazeRay);
        }
    }
}
