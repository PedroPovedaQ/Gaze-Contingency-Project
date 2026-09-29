using System;
class RotationalLayoutChecks
{
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Main()
    {
        var selfRight = new System.Collections.Generic.HashSet<string>();
        for (int i = -12; i < 12; i++)
        {
            string phrase = DirectionalHintPolicy.Variant(DirectionalHintPolicy.RightPhrase, i, true);
            Check(phrase != "Try looking right.", "self-similar right rotation omits rejected phrase");
            selfRight.Add(phrase);
        }
        Check(selfRight.Count == 3, "self-similar right rotation has three distinct alternatives");
        Check(DirectionalHintPolicy.Variant(DirectionalHintPolicy.RightPhrase, 2) == "Try looking right.",
            "neutral right rotation remains unchanged");
        Check(DirectionalHintPolicy.Variant(DirectionalHintPolicy.LeftPhrase, 2, true) == "Try looking left.",
            "left rotation remains unchanged");
        foreach (int degrees in new[] { -179, -135, -91, -90, -45, 0, 45, 90, 91, 135, 179 })
        {
            double radians = degrees * Math.PI / 180;
            string direction = DirectionalHintPolicy.Direction(0, 1, (float)Math.Sin(radians), (float)Math.Cos(radians), out bool far);
            Check(far == (Math.Abs(degrees) > 90), "strong cue uses strictly over two 45-degree steps");
            string phrase = DirectionalHintPolicy.Variant(direction, 0, false, far);
            if (far) Check(phrase == (direction == DirectionalHintPolicy.LeftPhrase ? "Look way left." : "Look way right."),
                "large turn produces a matching strong directional phrase");
        }
        DirectionalHintPolicy.Direction(0, 10, 10, 0, out bool exactlyTwo);
        Check(!exactlyTwo, "exactly ninety degrees stays ordinary regardless of vector length");
        double almostFull = 359 * Math.PI / 180, justPastZero = Math.PI / 180;
        string wrapped = DirectionalHintPolicy.Direction((float)Math.Sin(almostFull), (float)Math.Cos(almostFull),
            (float)Math.Sin(justPastZero), (float)Math.Cos(justPastZero), out bool farWrap);
        Check(wrapped == DirectionalHintPolicy.RightPhrase && !farWrap, "359-to-1 degree wrap is a short right turn");
        DirectionalHintPolicy.Direction(float.NaN, 1, 1, 0, out bool farInvalid);
        Check(!farInvalid, "invalid gaze does not produce a strong cue");
        Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), "Look way left.") >= 0 &&
            Array.IndexOf(DirectionalHintPolicy.AllPhrases(), "Look way right.") >= 0, "both strong cues are preloaded");
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
        foreach (string direction in new[] { "Look left.", "Look right." })
        {
            var variants = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 4; i++) variants.Add(DirectionalHintPolicy.Variant(direction, i));
            Check(variants.Count == 4, "original plus three distinct alternatives in each direction");
            Check(DirectionalHintPolicy.Variant(direction, 4) == direction, "phrases cycle before repeating");
            foreach (var phrase in variants)
                Check(phrase.Contains(direction == "Look left." ? "left" : "right"), "variant preserves direction");
        }
        Check(DirectionalHintPolicy.Direction(0,1,-1,0) == "Look left.", "left from forward gaze");
        Check(DirectionalHintPolicy.Direction(0,1,1,0) == "Look right.", "right from forward gaze");
        Check(DirectionalHintPolicy.Direction(1,0,0,1) == "Look left.", "rotated gaze reference");
        Check(DirectionalHintPolicy.Direction(0,-1,1,0) == "Look left.", "rear-facing reference");
        Check(DirectionalHintPolicy.Direction(0,1,0,-1) == "Look right.", "stable turn for directly behind");
        Check(DirectionalHintPolicy.Direction(0,1,0,1) == null, "aligned bearing is not a left/right cue");
        Check(DirectionalHintPolicy.Direction(0,0,1,0) == null, "vertical gaze has no horizontal cue");
        Check(DirectionalHintPolicy.Direction(float.NaN,1,1,0) == null, "invalid vector abstains");
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
        foreach (double pitch in new[] { -89d, -80d, -45d, 0d, 45d, 80d, 89d })
        {
            float horizontal = (float)Math.Cos(pitch * Math.PI / 180);
            Check(RotationalSearchLayout.IsInHorizontalSector(0, 0, 0, horizontal, 1.5f, out bool validSector) && validSector,
                "looking up/down keeps the same horizontal area");
        }
        Check(!RotationalSearchLayout.IsInHorizontalSector(0, 0, 1, 1, 1.5f, out bool adjacentValid) && adjacentValid,
            "adjacent wall is a genuine horizontal exit");
        Check(!RotationalSearchLayout.IsInHorizontalSector(0, 0, 0, 0, 1.5f, out bool verticalValid) && !verticalValid,
            "straight up/down abstains instead of recording an exit");
        Check(RotationalSearchLayout.IsInHorizontalSector(0.2f, 0.3f, -0.2f, 1.2f, 1.5f, out _),
            "offset eye origin intersects the correct wall");
        Check(!RotationalSearchLayout.IsInHorizontalSector(0, 0, 0, -1, 1.5f, out bool backValid) && backValid,
            "looking behind is a real exit");
        gate.Reset(); gate.Update(true, true, 0); gate.Update(true, true, 0.11); gate.MarkSpoken(0.11);
        foreach (double pitch in new[] { 80d, -80d, 0d })
        {
            bool insideSector = RotationalSearchLayout.IsInHorizontalSector(0, 0, 0,
                (float)Math.Cos(pitch * Math.PI / 180), 1.5f, out bool validSector);
            Check(!gate.Update(validSector, insideSector, 20), "vertical scans never reannounce the correct area");
        }
        gate.Update(false, false, 21); gate.Update(true, true, 22);
        Check(!gate.Update(true, true, 22.2), "vertical/invalid gap does not rearm an already spoken area cue");
        var returning = new GazeReturnCueGate();
        Check(!returning.Update(true, false, 0), "return cue requires a previous visit");
        returning.Update(true, true, 1); returning.Update(true, false, 1.05);
        Check(!returning.Update(true, false, 1.5), "brief pass-through is not a confirmed visit");
        returning.Reset(); returning.Update(true, true, 2); returning.Update(true, true, 2.11);
        Check(!returning.Update(true, false, 2.2) && returning.IsDebouncingExit, "exit must stabilize first");
        Check(!returning.Update(true, false, 2.4), "boundary flicker cannot trigger go back");
        Check(returning.Update(true, false, 2.46), "stable recent exit triggers go back");
        Check(returning.Update(true, false, 2.5), "canceled fade does not consume pending cue");
        returning.MarkSpoken(2.5);
        Check(!returning.Update(true, false, 2.6), "one cue per visit");
        returning.Update(true, true, 3); returning.Update(true, true, 3.11);
        returning.Update(true, false, 3.2);
        Check(!returning.Update(true, false, 3.5), "return cooldown prevents boundary spam");
        Check(!returning.Update(true, false, 9), "expired exit never triggers a late return cue");
        returning.Update(true, true, 10); returning.Update(true, true, 10.11);
        returning.Update(true, false, 10.2);
        Check(returning.Update(true, false, 10.5), "new stable visit can rearm after cooldown");
        returning.Update(false, false, 10.51);
        Check(!returning.Update(true, false, 10.8), "tracking loss invalidates return history");
        returning.Reset(); returning.Update(true, true, 20); returning.Update(true, true, 20.11);
        returning.Update(true, false, 20.2); returning.Update(true, true, 20.3);
        Check(!returning.IsPending(20.5), "regaining area cancels pending return cue");
        returning.Reset(); Check(!returning.Update(true, false, 30), "new objective clears visit memory");
        gate.Reset(); gate.Update(true, true, 40); gate.Update(true, true, 40.11); gate.MarkSpoken(40.11);
        gate.Update(true, false, 40.2); gate.Update(true, false, 40.8); gate.Update(true, true, 41);
        Check(!gate.Update(true, true, 41.2) && gate.InsideStable, "arrival stays detectable during cue cooldown");
        Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), DirectionalHintPolicy.ReturnPhrase) >= 0,
            "return cue is part of preloaded guidance");
        Console.WriteLine("PASS: 168 seeded scattered objects, bounds, spacing, seams and gaze entry/return debounce, expiry, rearm and cooldown.");
    }
}
