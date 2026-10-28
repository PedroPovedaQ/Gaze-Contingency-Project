using System;
using System.Collections.Generic;

[Serializable]
public sealed class EyeValidationStep
{
    public string exercise, condition, prompt;
    public float seconds, yaw, pitch, distance = 2.4f, gray = .3f;
    public int answer, block = -1;
    public bool response, rateAfter;
}

[Serializable]
public sealed class EyeValidationStatus
{
    public string id = "", protocol = EyeValidationProtocol.Version, stage = "idle", exercise = "", condition = "", prompt = "", recordingPath = "", error = "";
    public string participant = "";
    public int revision, step, count;
    public float remaining;
    public bool active, completed, measurementsValidated = false;
}

public static class EyeValidationProtocol
{
    public const string Version = "eye-validation-v1";
    public static readonly string[] Modes = { "full", "spatial", "saccades", "controls", "arithmetic", "search", "drift" };
    public static EyeValidationStep[] Build(string mode, int seed, bool hardFirst)
    {
        if (Array.IndexOf(Modes, mode) < 0) throw new ArgumentException("Unknown validation exercise");
        var steps = new List<EyeValidationStep>(); var rng = new Random(seed);
        Action<string,string,float,float,float,float> dot = (ex, prompt, seconds, yaw, pitch, distance) =>
            steps.Add(new EyeValidationStep { exercise=ex, condition=ex, prompt=prompt, seconds=seconds, yaw=yaw, pitch=pitch, distance=distance });
        if (mode=="full" || mode=="spatial")
            foreach (float d in new[]{1.2f,2.4f,3.2f}) foreach (float y in new[]{-15f,0f,15f}) foreach(float p in new[]{-10f,0f,10f})
                dot("spatial","Look at the dot with your eyes. Keep your head still. Dots move automatically.",2.5f,y,p,d);
        if (mode=="full" || mode=="saccades")
        {
            dot("fixation","Keep looking at the stationary dot. Blink naturally and keep your head still.",20,0,0,2.4f);
            int last=-1;
            for(int i=0;i<24;i++)
            {
                int n; do {n=rng.Next(8);} while(n==last); last=n;
                float[] ys={-15,15,0,0,-10,10,-10,10}, ps={0,0,-10,10,-10,-10,10,10};
                dot("saccades","Follow each jump with your eyes, keeping your head still. A dot jump is a cue, not a measured saccade.",1.5f+(float)rng.NextDouble()*.5f,ys[n],ps[n],2.4f);
            }
        }
        if(mode=="full" || mode=="controls")
        {
            for(int i=0;i<4;i++) dot("blinks","Look at the dot. When BLINK appears, blink once, then look steadily again.",8,0,0,2.4f);
            dot("head_turn","Keep your eyes on the world-fixed dot while slowly turning your head left and right. Stay comfortable.",30,0,0,2.4f);
            dot("pursuit","Follow the smoothly moving dot with your eyes. Keep your head still.",20,0,0,2.4f);
            foreach(float gray in new[]{.3f,.65f,.3f}) { dot("luminance","Look at the dot. The background will change slowly. Blink naturally.",20,0,0,2.4f);steps[steps.Count-1].gray=gray; }
            foreach(float y in new[]{0f,-15f,15f,0f}) dot("gaze_angle","Look at the stationary dot with your eyes; keep your head facing forward.",15,y,0,2.4f);
        }
        if(mode=="full" || mode=="arithmetic")
            for(int block=0;block<4;block++)
            {
                bool hard=(block==1 || block==2)^hardFirst;
                for(int p=0;p<3;p++)
                {
                    int a=hard?rng.Next(12,30):rng.Next(1,10), b=hard?rng.Next(3,9):rng.Next(1,10), c=rng.Next(2,9);
                    steps.Add(new EyeValidationStep { exercise="arithmetic",condition=hard?"hard":"easy",block=block,
                        prompt=hard?$"Calculate ({a} × {b}) − {c}":$"Calculate {a} + {b}",answer=hard?a*b-c:a+b,
                        seconds=10,response=true,rateAfter=p==2 });
                }
            }
        if(mode=="full" || mode=="search")
            for(int i=0;i<3;i++) steps.Add(new EyeValidationStep {exercise="search",condition="sandbox",prompt="Find the red cube. Look at it, point the right controller at it, and press the trigger. This is practice, not a study round.",seconds=25,answer=rng.Next(9)});
        if(mode=="full" || mode=="drift")
            for(int i=0;i<5;i++) dot("drift","Keep your head still and look at each dot. This check measures gaze without fitting a new correction.",2.5f,
                new[]{0f,-15f,15f,0f,0f}[i],new[]{0f,0f,0f,-10f,10f}[i],2.4f);
        return steps.ToArray();
    }
}

// All remote transitions are compare-and-set against the displayed validation revision.
public sealed class EyeValidationProgress
{
    readonly int count;
    public int index, revision;
    public string stage="instruction";
    public double since;
    public EyeValidationProgress(int count, double now=0) { this.count=count; since=now; }
    public bool Next(int expected,double now)
    {
        if(expected!=revision || stage!="instruction" || now-since<1) return false;
        Set("running",now);return true;
    }
    public bool FinishStimulus(double now,bool response)
    {
        if(stage!="running")return false;
        if(response)Set("response",now);else Advance(now);return true;
    }
    public bool Answer(int expected,double now)
    {
        if(expected!=revision || stage!="response")return false;
        Advance(now);return true;
    }
    public void Abort(){if(stage!="complete" && stage!="aborted"){stage="aborted";revision++;}}
    void Advance(double now) {index++;Set(index>=count?"complete":"instruction",now);}
    void Set(string value,double now){stage=value;since=now;revision++;}
}

[Serializable]
public sealed class EyeValidationRequest
{
    public string id="", mode="full", participant="", notes="";
    public int revision, seed, value;
    public bool hardFirst, alignFirst;
    public ParticipantFitMeasurements fit;
}
