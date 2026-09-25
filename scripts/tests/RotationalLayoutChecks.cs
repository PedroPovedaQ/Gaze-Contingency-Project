using System;
class RotationalLayoutChecks
{
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Main()
    {
        foreach (float radius in new[] { 1f, 1.5f, 3f })
        for (int seed = 42000; seed < 42030; seed++)
        {
            var points = RotationalSearchLayout.Build(radius, seed: seed);
            var again = RotationalSearchLayout.Build(radius, seed: seed);
            Check(points.Length == 168, "168 objects");
            var counts = new int[8];
            float low = 10, high = 0;
            for (int i=0;i<points.Length;i++)
            {
                var p=points[i]; counts[p.plane]++;
                double a=p.azimuth*Math.PI/180;
                Check(Math.Abs(p.x*Math.Sin(a)+p.z*Math.Cos(a)-radius)<0.00001, "objects stay on their wall");
                Check(Math.Abs(p.x*Math.Cos(a)-p.z*Math.Sin(a)) <= radius*Math.Tan(Math.PI/8)-0.14999, "corner clearance");
                Check(p.y >= 0.1f && p.y <= 3.048f, "floor height bounds");
                low=Math.Min(low,p.y); high=Math.Max(high,p.y);
                Check(p.x==again[i].x && p.y==again[i].y && p.z==again[i].z, "seed reproduces all positions");
                for(int j=0;j<i;j++)
                {
                    double x=p.x-points[j].x,y=p.y-points[j].y,z=p.z-points[j].z;
                    Check(x*x+y*y+z*z >= 0.039999, "minimum 20cm separation across all walls");
                }
            }
            Check(low < 0.3 && high > 2.8, "scatter covers the vertical range");
            for(int plane=0;plane<8;plane++)
            {
                Check(counts[plane]==21,"21 objects per wall");
                double a=plane*Math.PI/4,b=(plane+1)*Math.PI/4;
                double r=radius+RotationalSearchLayout.FrameDepthOffset,w=RotationalSearchLayout.FrameHalfWidth(radius);
                Check(Math.Abs(r*Math.Sin(a)+w*Math.Cos(a)-r*Math.Sin(b)+w*Math.Cos(b))<0.00001,"connected corners X");
                Check(Math.Abs(r*Math.Cos(a)-w*Math.Sin(a)-r*Math.Cos(b)-w*Math.Sin(b))<0.00001,"connected corners Z");
            }
        }
        var first=RotationalSearchLayout.Build(1.5f,seed:42);
        var next=RotationalSearchLayout.Build(1.5f,seed:43);
        Check(first[0].y!=next[0].y,"different trial seeds vary heights");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,0f,-1f})
        {
            bool rejected=false;
            try{RotationalSearchLayout.Build(1.5f,0.1f,invalid);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"invalid height rejected");
        }
        var gate=new GazeZoneEntryGate();
        Check(!gate.Update(true,true,0),"entry not immediate");
        Check(!gate.Update(true,true,0.05),"brief sweep ignored");
        Check(gate.Update(true,true,0.11),"stable entry ready");gate.MarkSpoken(0.11);
        Check(!gate.Update(true,true,8),"remaining inside does not repeat");
        gate.Update(false,false,8.1);
        Check(!gate.Update(true,true,9),"tracking loss does not rearm");
        gate.Update(true,false,9.1);gate.Update(true,false,9.7);
        Check(!gate.Update(true,true,10),"reentry must dwell");
        Check(gate.Update(true,true,10.31),"stable exit allows reentry");gate.MarkSpoken(10.31);
        gate.Update(true,false,11);gate.Update(true,false,11.6);gate.Update(true,true,12);
        Check(!gate.Update(true,true,12.4),"cooldown prevents spam");
        gate.Reset();Check(!gate.Update(true,true,20),"new trial resets dwell");
        Console.WriteLine("PASS: 168 seeded scattered objects, bounds, spacing, seams and gaze entry dwell/rearm/cooldown.");
    }
}
