using System;
using System.Collections.Generic;

/// <summary>Engineering stability filter; thresholds are not validated anatomical limits.</summary>
public sealed class FitSeparationWindow
{
    public const int MinimumSamples = 40;
    public const double MinimumSeconds = 2;
    readonly Queue<Tuple<double, float>> m_Samples = new Queue<Tuple<double, float>>();
    double m_LastTime = double.NegativeInfinity;
    public static bool Plausible(float mm) => !float.IsNaN(mm) && !float.IsInfinity(mm) && mm >= 40 && mm <= 85;
    public FitSeparationMeasurement Add(double time, float mm, string source, string utc)
    {
        if (!Plausible(mm) || time <= m_LastTime || time - m_LastTime > .2) m_Samples.Clear();
        m_LastTime = time;
        if (!Plausible(mm)) return null;
        m_Samples.Enqueue(Tuple.Create(time, mm));
        while (m_Samples.Count > 1 && time - m_Samples.Peek().Item1 > 2.2) m_Samples.Dequeue();
        if (m_Samples.Count < MinimumSamples || time - m_Samples.Peek().Item1 < MinimumSeconds) return null;
        float[] values = new float[m_Samples.Count]; int i = 0;
        foreach (var sample in m_Samples) values[i++] = sample.Item2;
        Array.Sort(values);
        float range = values[values.Length - 1] - values[0];
        if (range > 1) return null;
        return new FitSeparationMeasurement { recorded = true, samples = values.Length, source = source,
            recordedUtc = utc, windowSeconds = (float)(time - m_Samples.Peek().Item1), rangeMm = range,
            millimetres = (values[(values.Length - 1) / 2] + values[values.Length / 2]) * .5f };
    }
}

/// <summary>Observes stable view changes; never infers an adjustment-run status.</summary>
public sealed class FitSeparationChange
{
    float m_Baseline;
    double m_BaselineTime;
    string m_Source;
    public void Observe(ParticipantFitMeasurements fit, FitSeparationMeasurement view, double time)
    {
        if (m_Baseline == 0 || m_Source != view.source)
        { m_Baseline = view.millimetres; m_BaselineTime = time; m_Source = view.source; }
        float change = view.millimetres - m_Baseline;
        if (Math.Abs(change) >= .5f && Math.Abs(change) > Math.Abs(fit.separationChangeMm))
        {
            fit.separationChange = "change_observed"; fit.separationChangeMm = change;
            fit.separationChangeUtc = view.recordedUtc; fit.separationChangeSource = view.source;
        }
        else if (fit.separationChange != "change_observed" && time - m_BaselineTime >= 2)
            fit.separationChange = "no_change_observed";
    }
}
