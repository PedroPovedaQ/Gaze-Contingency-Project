using UnityEngine;
using VIVE.OpenXR.Passthrough;

public class VivePassthrough : MonoBehaviour
{
    private int passthroughID;

    void Start()
    {
        passthroughID = PassthroughAPI.CreatePlanarPassthrough(LayerType.Underlay);
    }

    void OnDestroy()
    {
        PassthroughAPI.DestroyPassthrough(passthroughID);
    }
}