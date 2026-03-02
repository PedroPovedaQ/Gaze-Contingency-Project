using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.Toolkits.PlaneDetection;
using static VIVE.OpenXR.PlaneDetection.VivePlaneDetection;

/// <summary>
/// Bridge component that uses VIVE's native PlaneDetectionManager to detect
/// planes from Room Setup and creates GameObjects compatible with the existing
/// AR Foundation-based spawn/interaction pipeline.
/// </summary>
public class VivePlaneProvider : MonoBehaviour
{
    [SerializeField, Tooltip("Material for plane visualization. Assign the same material used by the ARPlane prefab.")]
    Material m_PlaneMaterial;

    [SerializeField, Tooltip("Layer index for detected planes. Must match 'Placeable Surface' layer.")]
    int m_PlaneLayer = 7;

    readonly List<GameObject> m_DetectedPlanes = new List<GameObject>();
    bool m_PlanesActive;
    bool m_VisualsEnabled = true;
    bool m_DetectionComplete;

    public event Action<List<GameObject>> PlanesDetected;

    public bool HasPlanes => m_DetectedPlanes.Count > 0;
    public IReadOnlyList<GameObject> DetectedPlanes => m_DetectedPlanes;

    /// <summary>
    /// Enable plane detection and show planes. Called by ARFeatureController.
    /// </summary>
    public void EnablePlanes()
    {
        m_PlanesActive = true;
        if (!m_DetectionComplete)
            StartCoroutine(DetectPlanes());
        else
            SetPlanesActive(true);
    }

    /// <summary>
    /// Disable and hide planes. Called by ARFeatureController.
    /// </summary>
    public void DisablePlanes()
    {
        m_PlanesActive = false;
        SetPlanesActive(false);
    }

    /// <summary>
    /// Toggle MeshRenderer visibility without affecting colliders.
    /// </summary>
    public void SetVisualsEnabled(bool enabled)
    {
        m_VisualsEnabled = enabled;
        foreach (var planeObj in m_DetectedPlanes)
        {
            if (planeObj == null) continue;
            var renderer = planeObj.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = m_VisualsEnabled;
        }
    }

    IEnumerator DetectPlanes()
    {
        // Wait briefly for OpenXR session to be fully ready
        yield return new WaitForSeconds(0.5f);

        if (!PlaneDetectionManager.IsSupported())
        {
            Debug.LogError("VivePlaneProvider: PlaneDetection feature not supported");
            yield break;
        }

        var pd = PlaneDetectionManager.CreatePlaneDetector();
        if (pd == null)
        {
            Debug.LogError("VivePlaneProvider: Failed to create PlaneDetector");
            yield break;
        }

        pd.BeginPlaneDetection();
        yield return null;

        // Poll for completion — Room Setup planes typically complete within one frame
        var state = pd.GetPlaneDetectionState();
        float timeout = Time.unscaledTime + 5f;
        while (state == XrPlaneDetectionStateEXT.PENDING_EXT)
        {
            if (Time.unscaledTime > timeout)
            {
                Debug.LogError("VivePlaneProvider: Detection timed out");
                PlaneDetectionManager.DestroyPlaneDetector(pd);
                yield break;
            }
            yield return null;
            state = pd.GetPlaneDetectionState();
        }

        if (state != XrPlaneDetectionStateEXT.DONE_EXT)
        {
            Debug.LogError($"VivePlaneProvider: Detection failed with state {state}");
            PlaneDetectionManager.DestroyPlaneDetector(pd);
            yield break;
        }

        if (pd.GetPlaneDetections(out var locations) != XrResult.XR_SUCCESS || locations == null)
        {
            Debug.LogError("VivePlaneProvider: GetPlaneDetections failed");
            PlaneDetectionManager.DestroyPlaneDetector(pd);
            yield break;
        }

        Debug.Log($"VivePlaneProvider: Detected {locations.Count} planes");

        foreach (var location in locations)
        {
            var planeGO = CreatePlaneGameObject(location);
            if (planeGO != null)
                m_DetectedPlanes.Add(planeGO);
        }

        PlaneDetectionManager.DestroyPlaneDetector(pd);
        m_DetectionComplete = true;

        PlanesDetected?.Invoke(m_DetectedPlanes);
        Debug.Log($"VivePlaneProvider: Created {m_DetectedPlanes.Count} plane GameObjects");
    }

    GameObject CreatePlaneGameObject(PlaneDetectorLocation location)
    {
        var go = new GameObject($"VivePlane_{location.semanticType}_{location.planeId}");
        go.layer = m_PlaneLayer;
        go.transform.SetParent(transform);

        // Position and rotation from VIVE pose (already converted to Unity coords)
        go.transform.position = location.pose.position;
        go.transform.rotation = location.pose.rotation;

        // Create rectangular mesh from plane size
        var mesh = CreateRectMesh(location.size.x, location.size.y);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        var meshRenderer = go.AddComponent<MeshRenderer>();
        if (m_PlaneMaterial != null)
            meshRenderer.material = m_PlaneMaterial;
        meshRenderer.enabled = m_VisualsEnabled;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var meshCollider = go.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        var planeData = go.AddComponent<VivePlaneData>();
        planeData.PlaneId = location.planeId;
        planeData.Size = new Vector2(location.size.x, location.size.y);
        planeData.Orientation = location.orientation;
        planeData.SemanticType = location.semanticType;

        Debug.Log($"VivePlaneProvider: Plane {location.planeId} " +
                  $"type={location.semanticType} orientation={location.orientation} " +
                  $"pos={location.pose.position} size=({location.size.x:F2}, {location.size.y:F2})");

        return go;
    }

    /// <summary>
    /// Creates a rectangular mesh in the XY plane with normal facing +Z.
    /// Matches VIVE's convention after coordinate conversion.
    /// </summary>
    static Mesh CreateRectMesh(float width, float height)
    {
        float hx = width / 2f;
        float hy = height / 2f;

        var mesh = new Mesh { name = "VivePlaneMesh" };
        mesh.vertices = new[]
        {
            new Vector3(-hx, -hy, 0),
            new Vector3( hx, -hy, 0),
            new Vector3(-hx,  hy, 0),
            new Vector3( hx,  hy, 0),
        };
        mesh.triangles = new[] { 0, 3, 2, 0, 1, 3 };
        mesh.uv = new[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
        };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    void SetPlanesActive(bool active)
    {
        foreach (var plane in m_DetectedPlanes)
        {
            if (plane != null) plane.SetActive(active);
        }
    }

    void OnDestroy()
    {
        foreach (var plane in m_DetectedPlanes)
        {
            if (plane != null) Destroy(plane);
        }
        m_DetectedPlanes.Clear();
    }
}
