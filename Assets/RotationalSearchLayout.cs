using System;

/// <summary>Deterministic local geometry; +Z is forward and +X is right.</summary>
public static class RotationalSearchLayout
{
    public const string LayoutTag = "rotational_scatter_v4";
    public const int PlaneCount = 8;
    public const int ObjectsPerPlane = 21;
    public const float DefaultRadius = 1.5f;
    public const float DefaultMinHeight = 0.1f;
    public const float DefaultMaxHeight = 3.048f; // 10 feet, measured from the floor.
    public const float MinimumSpacing = 0.2f;
    public const int SeedBase = 42000;
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

    public static Slot[] Build(float radius, float minHeight = DefaultMinHeight, float maxHeight = DefaultMaxHeight, int seed = SeedBase)
    {
        ValidateRadius(radius);
        if (float.IsNaN(minHeight) || float.IsInfinity(minHeight) ||
            float.IsNaN(maxHeight) || float.IsInfinity(maxHeight) || minHeight < 0 || maxHeight <= minHeight)
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "Heights must be finite, nonnegative and increasing.");
        double halfSpan = radius * Math.Tan(Math.PI / PlaneCount) - k_ObjectCornerInset;
        var rng = new Random(seed);
        var result = new Slot[PlaneCount * ObjectsPerPlane];
        for (int plane = 0; plane < PlaneCount; plane++)
        {
            double angle = plane * 2 * Math.PI / PlaneCount;
            for (int slot = 0; slot < ObjectsPerPlane; slot++)
            {
                int index = plane * ObjectsPerPlane + slot;
                bool placed = false;
                for (int attempt = 0; attempt < 20000; attempt++)
                {
                    double offset = (rng.NextDouble() * 2 - 1) * halfSpan;
                    var candidate = new Slot {
                        x = (float)(radius * Math.Sin(angle) + offset * Math.Cos(angle)),
                        y = (float)(minHeight + rng.NextDouble() * (maxHeight - minHeight)),
                        z = (float)(radius * Math.Cos(angle) - offset * Math.Sin(angle)),
                        plane = plane, slot = slot, azimuth = plane * PlaneAngleDegrees
                    };
                    bool clear = true;
                    for (int j = 0; j < index; j++)
                    {
                        double dx = candidate.x - result[j].x, dy = candidate.y - result[j].y, dz = candidate.z - result[j].z;
                        if (dx * dx + dy * dy + dz * dz < MinimumSpacing * MinimumSpacing)
                        { clear = false; break; }
                    }
                    if (!clear) continue;
                    result[index] = candidate; placed = true; break;
                }
                if (!placed) throw new InvalidOperationException("Height/radius range cannot fit the requested scatter with minimum spacing.");
            }
        }
        return result;
    }
}
