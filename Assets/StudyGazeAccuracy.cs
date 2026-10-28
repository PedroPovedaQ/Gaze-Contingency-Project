using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent five-dot spatial check. Never fits a correction or claims sensor-rate validation.</summary>
public sealed class StudyGazeAccuracy
{
    public const double SettleSeconds=.5;
    [Serializable] public sealed class Point
    {
        public int index,observations;
        public bool completed;
        public float meanDegrees,medianDegrees,p90Degrees;
        public float beforeMedianDegrees,beforeP90Degrees;
    }
    [Serializable] public sealed class Result
    {
        public string method="five_dot_equal_target_mean_500ms_query_deduplicated_v2";
        public bool passed;
        public double settleSeconds=SettleSeconds;
        public int minimumObservations=GazeQuickAlignment.MinimumUpdates;
        public float maxMedianDegrees=GazeQuickAlignment.MaxMedianErrorDegrees,maxP90Degrees=GazeQuickAlignment.MaxP90ErrorDegrees;
        public float meanDegrees;
        public bool checkImprovement;
        public float correctionMagnitudeDegrees,meanMedianImprovementDegrees;
        public Point[] points;
    }
    readonly List<float>[] m_Errors=new List<float>[5];
    readonly List<float>[] m_Before=new List<float>[5];
    readonly float m_CorrectionMagnitude;
    readonly bool[] m_Completed=new bool[5];
    readonly HashSet<string> m_Seen=new HashSet<string>();
    double m_Start;
    int m_Step=-1;
    public StudyGazeAccuracy(float correctionMagnitude=0)
    {m_CorrectionMagnitude=correctionMagnitude;for(int i=0;i<5;i++){m_Errors[i]=new List<float>();m_Before[i]=new List<float>();}}
    public void StartPoint(int step,double time){m_Step=step;m_Start=time;}
    public void EndPoint(int step){m_Completed[step]=true;m_Step=-1;}
    public void Observe(GazeObservation row,int step)
    {
        // Deduplicate before selecting the running/settled window, like the offline audit.
        string key=row.recordingId+":"+row.deviceId+":"+row.raySource+":";
        double time;
        if(row.raySource=="HTC_binocular_mean")
        {
            time=row.htcQueryReceiptTime;
            var stamp=row.htcTiming?.gaze??default;
            key+=stamp.observed==1?$"{stamp.epoch}:{stamp.tracker}:{stamp.baseSpace}:{stamp.session}:{stamp.beforeNs}:{stamp.afterNs}":time.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        }
        else if(row.raySource=="OpenXR_EXT")
        {time=row.receiptTime;if(!Finite(row.inputEventTime))return;key+=row.inputEventTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
        else return;
        if(!Finite(time) || time<=0)return;
        // During an untimed instruction, retain only the last delivery to exclude
        // its cached copy at onset without accumulating an unbounded idle history.
        if(step<0){m_Seen.Clear();m_Seen.Add(key);return;}
        if(!m_Seen.Add(key))return;
        if(step!=m_Step || step<0 || step>=5 || time<m_Start+SettleSeconds || !row.valid || !row.alignmentApplied || !row.validationTargetPresent)return;
        var target=row.validationTarget-row.worldOrigin;
        if(!Finite(target) || !Finite(row.worldDirection) || target.sqrMagnitude<1e-12f || row.worldDirection.sqrMagnitude<1e-12f)return;
        if(!Finite(row.unalignedWorldDirection) || row.unalignedWorldDirection.sqrMagnitude<1e-12f)return;
        m_Errors[step].Add(Vector3.Angle(row.worldDirection,target));
        m_Before[step].Add(Vector3.Angle(row.unalignedWorldDirection,target));
    }
    static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
    static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
    static float Percentile(List<float> sorted,float fraction)
    {float p=(sorted.Count-1)*fraction;int low=(int)p;return Mathf.Lerp(sorted[low],sorted[Math.Min(low+1,sorted.Count-1)],p-low);}
    public Result Summarize()
    {
        var result=new Result {passed=true,points=new Point[5],checkImprovement=m_CorrectionMagnitude>=.25f,correctionMagnitudeDegrees=m_CorrectionMagnitude};
        for(int i=0;i<5;i++)
        {
            var values=new List<float>(m_Errors[i]);values.Sort();
            var before=new List<float>(m_Before[i]);before.Sort();
            var point=new Point {index=i,completed=m_Completed[i],observations=values.Count};result.points[i]=point;
            if(values.Count>0)
            {foreach(float value in values)point.meanDegrees+=value;point.meanDegrees/=values.Count;point.medianDegrees=Percentile(values,.5f);point.p90Degrees=Percentile(values,.9f);
                point.beforeMedianDegrees=Percentile(before,.5f);point.beforeP90Degrees=Percentile(before,.9f);}
            result.meanDegrees+=point.meanDegrees/5;
            result.meanMedianImprovementDegrees+=(point.beforeMedianDegrees-point.medianDegrees)/5;
            result.passed &= point.completed && point.observations>=result.minimumObservations &&
                point.medianDegrees<=result.maxMedianDegrees && point.p90Degrees<=result.maxP90Degrees;
            if(result.checkImprovement)result.passed &= point.medianDegrees<=point.beforeMedianDegrees+.1f && point.p90Degrees<=point.beforeP90Degrees+.25f;
        }
        if(result.checkImprovement)result.passed &= result.meanMedianImprovementDegrees>=.25f;
        return result;
    }
}
