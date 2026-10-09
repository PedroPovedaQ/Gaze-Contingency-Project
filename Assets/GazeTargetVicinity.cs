using UnityEngine;

/// <summary>
/// Circular angular region enclosing the target collider, plus an adjustable margin.
/// This is a guidance tolerance, not a fixation measure or a larger selection collider.
/// Its optional ring and adjustments are restricted to DEV_NEUTRAL sessions.
/// </summary>
public sealed class GazeTargetVicinity : MonoBehaviour
{
    public const float DefaultPaddingDegrees = 6f;
    float m_PaddingDegrees = DefaultPaddingDegrees;
    bool m_ShowRing;
    LineRenderer m_Ring;
    Material m_Material;
    public float PaddingDegrees => SessionConfig.NeutralQuickTest ? m_PaddingDegrees : DefaultPaddingDegrees;
    public bool RingVisible => SessionConfig.NeutralQuickTest && m_ShowRing;

    public void SetPadding(float degrees)
    {
        if (SessionConfig.NeutralQuickTest && float.IsFinite(degrees))
            m_PaddingDegrees = Mathf.Clamp(degrees, 0f, 20f);
    }
    public void SetRingVisible(bool visible)
    {
        m_ShowRing = SessionConfig.NeutralQuickTest && visible;
        if (!m_ShowRing) Hide();
    }

    public static bool Measure(Vector3 origin, Vector3 direction, Bounds bounds, float padding,
        out float errorDegrees, out float radiusDegrees)
    {
        errorDegrees = radiusDegrees = 0;
        Vector3 delta = bounds.center - origin;
        float distance = delta.magnitude, radius = bounds.extents.magnitude;
        if (!float.IsFinite(distance) || !float.IsFinite(radius) || !float.IsFinite(direction.sqrMagnitude) ||
            !float.IsFinite(padding) || distance <= radius || direction.sqrMagnitude < .0001f) return false;
        radiusDegrees = Mathf.Min(89f, Mathf.Asin(radius / distance) * Mathf.Rad2Deg + Mathf.Clamp(padding, 0f, 20f));
        errorDegrees = Vector3.Angle(direction, delta);
        return true;
    }

    public static bool ControllerCurrent(ControllerRaySelector.TelemetrySample sample, int frame,
        double receiptTime, int currentFrame, double now) => sample.available && sample.rayAvailable &&
        sample.tracked == 1 && frame >= 0 && frame <= currentFrame && currentFrame - frame <= 1 &&
        GazeQualityPolicy.IsFresh(true, receiptTime, now, .1);

    public void Draw(Vector3 origin, Bounds bounds, float radiusDegrees, bool inside)
    {
        if (!RingVisible) { Hide(); return; }
        if (m_Ring == null)
        {
            var go = new GameObject("DEV target gaze vicinity");
            go.transform.SetParent(transform, false); go.layer = 2; // No collider or ray target.
            m_Ring = go.AddComponent<LineRenderer>();
            m_Material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m_Ring.sharedMaterial = m_Material;
            m_Ring.useWorldSpace = true; m_Ring.loop = true; m_Ring.positionCount = 64;
            m_Ring.widthMultiplier = .008f;
        }
        m_Ring.enabled = true;
        m_Material.SetColor("_BaseColor", inside ? Color.green : Color.cyan);
        Vector3 delta = bounds.center - origin;
        // A plane facing the eye, so the rendered circumference matches the angular test.
        Quaternion facing = Quaternion.LookRotation(delta);
        float radius = delta.magnitude * Mathf.Tan(radiusDegrees * Mathf.Deg2Rad);
        for (int i = 0; i < m_Ring.positionCount; i++)
        {
            float angle = i * 2 * Mathf.PI / m_Ring.positionCount;
            m_Ring.SetPosition(i, bounds.center + facing * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }
    public void Hide() { if (m_Ring != null) m_Ring.enabled = false; }
    void OnDisable() => Hide();
    void OnDestroy() { if (m_Material != null) Destroy(m_Material); }
}
