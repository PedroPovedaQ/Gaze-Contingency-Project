using UnityEngine;

/// <summary>Presentation only: a thin first-person line along the saved gaze ray.</summary>
public static class TrialReviewGazeLine
{
    public static void FitToCamera(LineRenderer line, Camera camera, float viewportHeight)
    {
        if (!line.enabled) return;
        Vector3 a = line.GetPosition(0), b = line.GetPosition(1);
        Vector3 forward = camera.transform.forward, eye = camera.transform.position;
        float za = Vector3.Dot(a - eye, forward), zb = Vector3.Dot(b - eye, forward);
        float near = camera.nearClipPlane + .001f;
        if (za < near && zb < near) { line.enabled = false; return; }
        // Clip along the existing line, never move the recorded origin sideways.
        if (za < near) { a = Vector3.Lerp(a, b, (near - za) / (zb - za)); za = near; }
        if (zb < near) { b = Vector3.Lerp(a, b, (near - za) / (zb - za)); zb = near; }
        line.SetPosition(0, a); line.SetPosition(1, b);
        // Perspective divides by depth. Width proportional to depth cancels the
        // near-camera magnification that otherwise makes the ribbon a triangle.
        float widthPerDepth = 2.5f * 2 * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2)
            / Mathf.Max(1, viewportHeight);
        line.startWidth = za * widthPerDepth;
        line.endWidth = zb * widthPerDepth;
    }
}
