using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.Passthrough;
using VIVE.OpenXR.CompositionLayer;

public class VivePassthrough : MonoBehaviour
{
    VIVE.OpenXR.Passthrough.XrPassthroughHTC passthroughHandle;
    bool created = false;
    float retryTimer = 0f;

    void Update()
    {
        if (!created)
        {
            retryTimer += Time.deltaTime;
            if (retryTimer >= 2f)
            {
                retryTimer = 0f;
                Debug.Log("VivePassthrough: Attempting new PassthroughAPI...");
                XrResult result = PassthroughAPI.CreatePlanarPassthrough(
                    out passthroughHandle,
                    LayerType.Underlay,
                    onDestroyPassthroughSessionHandler: null,
                    alpha: 1f,
                    compositionDepth: 0u
                );
                Debug.Log("VivePassthrough: Result = " + result);
                if (result == XrResult.XR_SUCCESS)
                {
                    created = true;
                    Debug.Log("VivePassthrough: Passthrough created successfully!");
                }
            }
        }
    }

    void OnDestroy()
    {
        if (created)
            PassthroughAPI.DestroyPassthrough(passthroughHandle);
    }
}