using System;
class RotationalLayoutChecks
{
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Main()
    {
        var points = RotationalSearchLayout.Build(1.5f);
        Check(points.Length == 56, "exactly 56 objects");
        var counts = new int[8];
        foreach (var p in points)
        {
            counts[p.plane]++;
            double a = p.azimuth * Math.PI / 180;
            Check(Math.Abs(p.x * Math.Sin(a) + p.z * Math.Cos(a) - 1.5) < 0.0001, "every object lies on its vertical tangent plane");
            Check(Math.Abs(p.y) <= 0.26, "objects remain near seated eye height");
        }
        for (int i=0; i<8; i++) Check(counts[i] == 7, "seven objects on every plane");
        // End objects should reach toward the polygon corners, leaving 15 cm for
        // distinct hitboxes. This catches the old fixed, narrow object clusters.
        double expectedEdge = 1.5 * Math.Tan(Math.PI / 8) - 0.15;
        Check(Math.Abs(points[1].x - expectedEdge) < 0.0001, "objects fill the usable face width");
        for (int i=0; i<points.Length; i++) for (int j=i+1; j<points.Length; j++)
        {
            double x=points[i].x-points[j].x, y=points[i].y-points[j].y, z=points[i].z-points[j].z;
            Check(Math.Sqrt(x*x+y*y+z*z) >= 0.249, "gaze targets do not overlap");
        }
        Check(points[0].z > 0 && Math.Abs(points[0].x)<0.001, "zero degrees is forward");
        Check(points[14].x > 0 && Math.Abs(points[14].z)<0.001, "90 degrees is right");
        Check(points[28].z < 0 && Math.Abs(points[28].x)<0.001, "180 degrees is behind");
        var again = RotationalSearchLayout.Build(1.5f);
        for (int i=0;i<56;i++) Check(points[i].x == again[i].x && points[i].z == again[i].z, "layout deterministic");
        bool rejected=false;
        try { RotationalSearchLayout.Build(0); } catch (ArgumentOutOfRangeException) { rejected=true; }
        Check(rejected, "invalid radius rejected");
        foreach (float radius in new[] { 1f, 1.5f, 3f }) CheckConnectedRing(radius);
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, 0.99f, 3.01f })
        {
            rejected = false;
            try { RotationalSearchLayout.FrameHalfWidth(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "invalid outline radius rejected");
        }
        Console.WriteLine("PASS: 56 deterministic objects, expanded face coverage, closed octagon corners, spacing at 1–3m, handedness and radius validation.");
    }

    static void CheckConnectedRing(float radius)
    {
        var points = RotationalSearchLayout.Build(radius);
        double frameRadius = radius + RotationalSearchLayout.FrameDepthOffset;
        double halfWidth = RotationalSearchLayout.FrameHalfWidth(radius);
        for (int plane = 0; plane < 8; plane++)
        {
            double a = plane * Math.PI / 4;
            double b = ((plane + 1) % 8) * Math.PI / 4;
            // Right edge of this face must equal the next face's left edge,
            // including the closing 315-to-0-degree seam.
            double rightX = frameRadius * Math.Sin(a) + halfWidth * Math.Cos(a);
            double rightZ = frameRadius * Math.Cos(a) - halfWidth * Math.Sin(a);
            double leftX = frameRadius * Math.Sin(b) - halfWidth * Math.Cos(b);
            double leftZ = frameRadius * Math.Cos(b) + halfWidth * Math.Sin(b);
            Check(Math.Abs(rightX - leftX) < 0.00001 && Math.Abs(rightZ - leftZ) < 0.00001, "adjacent outline corners join");
            var edge = points[plane * 7 + 1];
            double tangentOffset = edge.x * Math.Cos(a) - edge.z * Math.Sin(a);
            Check(Math.Abs(radius * Math.Tan(Math.PI / 8) - tangentOffset - 0.15) < 0.00001, "15cm object clearance from polygon corner");
            Check(edge.plane == plane && edge.slot == 1, "stable plane and slot identities");
        }
        for (int i = 0; i < points.Length; i++) for (int j = i + 1; j < points.Length; j++)
        {
            double x = points[i].x - points[j].x, y = points[i].y - points[j].y, z = points[i].z - points[j].z;
            Check(Math.Sqrt(x*x + y*y + z*z) >= 0.249, "objects remain separated within and across faces at every radius");
        }
    }
}
