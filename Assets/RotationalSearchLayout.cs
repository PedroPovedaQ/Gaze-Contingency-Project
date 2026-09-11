using System;

/// <summary>Deterministic local geometry; +Z is forward and +X is right.</summary>
public static class RotationalSearchLayout
{
    public const int PlaneCount = 8;
    public const int ObjectsPerPlane = 7;
    public const float DefaultRadius = 1.5f;
    public struct Slot
    {
        public float x, y, z, azimuth;
        public int plane, slot;
    }

    public static Slot[] Build(float radius)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 1f || radius > 3f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Beta radius must be between 1 and 3 metres.");
        var result = new Slot[PlaneCount * ObjectsPerPlane];
        for (int plane = 0; plane < PlaneCount; plane++)
        {
            double angle = plane * Math.PI / 4;
            for (int slot = 0; slot < ObjectsPerPlane; slot++)
            {
                double offset = slot == 0 ? 0 : 0.25 * Math.Cos((slot - 1) * Math.PI / 3);
                double height = slot == 0 ? 0 : 0.25 * Math.Sin((slot - 1) * Math.PI / 3);
                result[plane * ObjectsPerPlane + slot] = new Slot
                {
                    x = (float)(radius * Math.Sin(angle) + offset * Math.Cos(angle)),
                    y = (float)height,
                    z = (float)(radius * Math.Cos(angle) - offset * Math.Sin(angle)),
                    plane = plane, slot = slot, azimuth = plane * 45f
                };
            }
        }
        return result;
    }
}
