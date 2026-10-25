using System;

/// <summary>Deterministic local geometry; +Z is forward and +X is right.</summary>
public static class RotationalSearchLayout
{
    public const string LayoutTag = "rotational_scatter_v6_precue_theta";
    public const int PlaneCount = 8;
    public const int ObjectsPerPlane = 21;
    public const float DefaultRadius = 1.5f;
    public const float DefaultMinHeight = 0.1f;
    public const float TopMargin = .1f;
    // Preserve floor clearance and object size; reduce the full former 3.148 m wall by 30%.
    public const float DefaultWallHeight = (3.048f + TopMargin) * .7f;
    public const float DefaultMaxHeight = DefaultWallHeight - TopMargin;
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

    /// <summary>Tests a gaze ray in one wall's local XZ coordinates, independent of eye height/pitch.</summary>
    public static bool IsInHorizontalSector(float originX, float originZ, float directionX, float directionZ,
        float radius, out bool valid)
    {
        double lengthSquared = (double)directionX * directionX + (double)directionZ * directionZ;
        valid = !float.IsNaN(originX) && !float.IsInfinity(originX) &&
            !float.IsNaN(originZ) && !float.IsInfinity(originZ) &&
            !double.IsNaN(lengthSquared) && !double.IsInfinity(lengthSquared) && lengthSquared >= 0.0001 &&
            !float.IsNaN(radius) && !float.IsInfinity(radius) && radius > 0;
        if (!valid) return false; // Nearly vertical gaze has no reliable horizontal sector.
        double length = Math.Sqrt(lengthSquared);
        double x = directionX / length, z = directionZ / length;
        if (z <= 0.0001) return false;
        double distance = (radius - originZ) / z;
        return distance > 0 && Math.Abs(originX + x * distance) <= radius * Math.Tan(Math.PI / PlaneCount);
    }

    public static float UsableHalfWidth(float radius)
    {
        ValidateRadius(radius);
        return (float)(radius * Math.Tan(Math.PI / PlaneCount) - k_ObjectCornerInset);
    }

    // Intersect the assigned bearing from the observed eye origin with its planned
    // wall. Abstain if translation puts that intersection outside the usable face.
    public static bool TryTarget(float radius, float originX, float originZ,
        float crossX, float crossZ, float signedTheta, int targetPlane, int targetSlot,
        float height, out Slot target)
    {
        target = default;
        ValidateRadius(radius);
        if (targetPlane < 0 || targetPlane >= PlaneCount || targetSlot < 0 || targetSlot >= ObjectsPerPlane)
            return false;
        foreach (float value in new[] { originX, originZ, crossX, crossZ, signedTheta, height })
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
        double x = crossX - originX, z = crossZ - originZ;
        if (x * x + z * z < .0001) return false;
        double turn = signedTheta * Math.PI / 180;
        double dx = x * Math.Cos(turn) + z * Math.Sin(turn);
        double dz = z * Math.Cos(turn) - x * Math.Sin(turn);
        double wall = targetPlane * Math.PI / 4, nx = Math.Sin(wall), nz = Math.Cos(wall);
        double dot = dx * nx + dz * nz;
        if (dot <= .000001) return false;
        double distance = (radius - originX * nx - originZ * nz) / dot;
        double tx = originX + distance * dx, tz = originZ + distance * dz;
        if (distance <= 0 || Math.Abs(tx * nz - tz * nx) > UsableHalfWidth(radius)) return false;
        target = new Slot { x = (float)tx, y = height, z = (float)tz,
            plane = targetPlane, slot = targetSlot, azimuth = targetPlane * PlaneAngleDegrees };
        return true;
    }

    public static Slot[] Build(float radius, float minHeight = DefaultMinHeight, float maxHeight = DefaultMaxHeight, int seed = SeedBase, Slot? pinnedTarget = null)
    {
        ValidateRadius(radius);
        if (float.IsNaN(minHeight) || float.IsInfinity(minHeight) ||
            float.IsNaN(maxHeight) || float.IsInfinity(maxHeight) || minHeight < 0 || maxHeight <= minHeight)
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "Heights must be finite, nonnegative and increasing.");
        double halfSpan = radius * Math.Tan(Math.PI / PlaneCount) - k_ObjectCornerInset;
        var rng = new Random(seed);
        var result = new Slot[PlaneCount * ObjectsPerPlane];
        int pinnedIndex = -1;
        if (pinnedTarget.HasValue)
        {
            var pinned = pinnedTarget.Value;
            double a = pinned.plane * Math.PI / 4;
            if (pinned.plane < 0 || pinned.plane >= PlaneCount || pinned.slot < 0 || pinned.slot >= ObjectsPerPlane ||
                float.IsNaN(pinned.x) || float.IsInfinity(pinned.x) || float.IsNaN(pinned.z) || float.IsInfinity(pinned.z) ||
                float.IsNaN(pinned.y) || pinned.y < minHeight || pinned.y > maxHeight ||
                Math.Abs(pinned.x * Math.Sin(a) + pinned.z * Math.Cos(a) - radius) > .0001 ||
                Math.Abs(pinned.x * Math.Cos(a) - pinned.z * Math.Sin(a)) > halfSpan + .00001)
                throw new ArgumentOutOfRangeException(nameof(pinnedTarget));
            pinnedIndex = pinned.plane * ObjectsPerPlane + pinned.slot;
            result[pinnedIndex] = pinned;
        }
        for (int plane = 0; plane < PlaneCount; plane++)
        {
            double angle = plane * 2 * Math.PI / PlaneCount;
            for (int slot = 0; slot < ObjectsPerPlane; slot++)
            {
                int index = plane * ObjectsPerPlane + slot;
                if (index == pinnedIndex) continue;
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
                    for (int j = 0; j < result.Length; j++)
                    {
                        if (j >= index && j != pinnedIndex) continue;
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
