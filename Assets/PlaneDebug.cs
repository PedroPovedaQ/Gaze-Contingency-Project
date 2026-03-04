using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;

public class PlaneDebug : MonoBehaviour
{
    ARPlaneManager planeManager;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(5f);
        
        planeManager = FindObjectOfType<ARPlaneManager>();
        
        if (planeManager == null)
        {
            Debug.LogError("PlaneDebug: No ARPlaneManager found!");
            yield break;
        }
        
        Debug.Log("PlaneDebug: ARPlaneManager found, enabled=" + planeManager.enabled);
        Debug.Log("PlaneDebug: subsystem=" + planeManager.subsystem);
        Debug.Log("PlaneDebug: trackables count=" + planeManager.trackables.count);
        
        if (planeManager.subsystem == null)
        {
            Debug.LogError("PlaneDebug: Plane subsystem is NULL - VIVE plane provider not registered");
        }
        else
        {
            Debug.Log("PlaneDebug: subsystem running=" + planeManager.subsystem.running);
        }
    }

    void Update()
    {
        if (planeManager != null && Time.frameCount % 300 == 0)
        {
            Debug.Log("PlaneDebug: trackables=" + planeManager.trackables.count + 
                      " subsystem=" + (planeManager.subsystem != null ? "exists" : "NULL"));
        }
    }
}