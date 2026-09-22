using System;

/// <summary>Deterministic local geometry; +Z is forward and +X is right.</summary>
public static class RotationalSearchLayout
{
    public const string LayoutTag = "rotational_octagon_v2";
    public const int PlaneCount = 8;
    public const int ObjectsPerPlane = 7;
    public const float DefaultRadius = 1.5f;
    public const float FrameDepthOffset = 0.08f;
    public const float PlaneAngleDegrees = 360f / PlaneCount;
    const float k_ObjectCornerInset = 0.15f;
    public struct Slot
    {
        public float x, y, z, azimuth;
        public int plane, slot;
    }

    static void ValidateRadius(float radius)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 1f || radius > 3f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Beta radius must be between 1 and 3 metres.");
    }

    /// <summary>Half the tangent face at the outline radius; adjacent corners coincide.</summary>
    public static float FrameHalfWidth(float radius)
    {
        ValidateRadius(radius);
        return (float)((radius + FrameDepthOffset) * Math.Tan(Math.PI / PlaneCount));
    }

    public static Slot[] Build(float radius)
    {
        ValidateRadius(radius);
        // Spread the existing seven slots across each octagon face without putting
        // adjacent faces' end objects together at the shared corner.
        double halfSpan = radius * Math.Tan(Math.PI / PlaneCount) - k_ObjectCornerInset;
        var result = new Slot[PlaneCount * ObjectsPerPlane];
        for (int plane = 0; plane < PlaneCount; plane++)
        {
            double angle = plane * 2 * Math.PI / PlaneCount;
            for (int slot = 0; slot < ObjectsPerPlane; slot++)
            {
                double offset = slot == 0 ? 0 : halfSpan * Math.Cos((slot - 1) * Math.PI / 3);
                double height = slot == 0 ? 0 : 0.25 * Math.Sin((slot - 1) * Math.PI / 3);
                result[plane * ObjectsPerPlane + slot] = new Slot
                {
                    x = (float)(radius * Math.Sin(angle) + offset * Math.Cos(angle)),
                    y = (float)height,
                    z = (float)(radius * Math.Cos(angle) - offset * Math.Sin(angle)),
                    plane = plane, slot = slot, azimuth = plane * PlaneAngleDegrees
                };
            }
        }
        return result;
    }
}
