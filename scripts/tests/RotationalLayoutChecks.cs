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
        Console.WriteLine("PASS: eight planes, seven objects each, handedness, spacing, deterministic layout, radius validation.");
    }
}
