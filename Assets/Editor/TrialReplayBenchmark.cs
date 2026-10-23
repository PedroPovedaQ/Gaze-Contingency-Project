using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

// Explicit synthetic capacity probe; never silently opens participant recordings.
public static class TrialReplayBenchmark
{
    [Serializable] sealed class Report
    {
        public string schema="unity-replay-capacity-v1", sourceSha256, sourceType, unity, os, cpu, seekMode;
        public string seekPattern="Descending late/middle/early (90%, 50%, 10%) within each saved trial interval";
        public int trials, records, systemMemoryMB, seekQueries;
        public long sourceBytes, managedBytesAfterLoad, processWorkingSetAfterLoad;
        public double loadSeconds, seekP95Milliseconds, seekMaxMilliseconds;
        public bool allTrialIdentitiesVerified;
        public string limitation="Synthetic data; synchronous state seeks only. No rendering, acoustic, sensor-rate or headset validation. Host not isolated; managed memory excludes native allocations.";
    }
    public static void Run()
    {
        string source=Environment.GetEnvironmentVariable("GAZE_REPLAY_BENCHMARK_SOURCE");
        string output=Environment.GetEnvironmentVariable("GAZE_REPLAY_BENCHMARK_OUTPUT");
        if(string.IsNullOrEmpty(source)||string.IsNullOrEmpty(output)||File.Exists(output))
            throw new InvalidOperationException("Choose an existing synthetic source and new output path in GAZE_REPLAY_BENCHMARK_SOURCE/OUTPUT.");
        string hash;
        using(var sha=SHA256.Create()) using(var file=File.OpenRead(source))
            hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
        using(var reader=new StreamReader(source))
        {
            var header=JsonUtility.FromJson<TrialReplayRecord>(reader.ReadLine());
            if(header.sourceType!="synthetic-benchmark")throw new InvalidDataException("Capacity probe requires explicit synthetic-benchmark header.");
        }
        var timer=Stopwatch.StartNew();var timeline=TrialReplayFile.Read(source);timer.Stop();
        var report=new Report{sourceSha256=hash,sourceType=timeline.Header.sourceType,unity=Application.unityVersion,
            os=SystemInfo.operatingSystem,cpu=SystemInfo.processorType,systemMemoryMB=SystemInfo.systemMemorySize,
            trials=timeline.Trials.Count,records=timeline.Records.Count,sourceBytes=new FileInfo(source).Length,
            loadSeconds=timer.Elapsed.TotalSeconds,managedBytesAfterLoad=GC.GetTotalMemory(false),
            processWorkingSetAfterLoad=Process.GetCurrentProcess().WorkingSet64};
        bool fullPrefix=Environment.GetEnvironmentVariable("GAZE_REPLAY_BENCHMARK_MODE")=="full-prefix";
        report.seekMode=fullPrefix ? "full-prefix-reference" : "indexed-trial-snapshots";
        var reference=new TrialReplayState();
        var durations=new List<double>();
        // Descending order forces real backward reconstruction on every trial.
        for(int i=timeline.Trials.Count-1;i>=0;i--)
        {
            var trial=timeline.Trials[i];
            double end=i+1<timeline.Trials.Count ? timeline.Trials[i+1].time : timeline.Duration;
            foreach(double fraction in new[]{.9,.5,.1})
            {
                double time=trial.time+(end-trial.time)*fraction;
                timer.Restart();
                if(fullPrefix)
                {
                    reference.Reset();
                    foreach(var record in timeline.Records) { if(record.time>time)break;reference.Apply(record); }
                }
                else timeline.Seek(time);
                timer.Stop();durations.Add(timer.Elapsed.TotalMilliseconds);
                var state=fullPrefix ? reference : timeline.State;
                if(state.Trial?.trialId!=trial.trialId || state.Sample?.trialId!=trial.trialId ||
                   state.Transforms.Count!=trial.objects.Length)
                    throw new Exception("Trial identity/object/sample reconstruction failed at "+trial.trialId);
                foreach(var obj in trial.objects)
                    if(state.Transforms[obj.id].position!=obj.position)throw new Exception("Synthetic static position changed.");
            }
        }
        report.seekQueries=durations.Count;
        durations.Sort();report.seekP95Milliseconds=durations[(int)Math.Ceiling(.95*durations.Count)-1];
        report.seekMaxMilliseconds=durations[durations.Count-1];report.allTrialIdentitiesVerified=true;
        using(var writer=new StreamWriter(new FileStream(output,FileMode.CreateNew,FileAccess.Write)))writer.Write(JsonUtility.ToJson(report,true));
        UnityEngine.Debug.Log("[TrialReplayBenchmark] PASS: "+report.trials+" trial identities; load "+report.loadSeconds+" s, backward-seek p95 "+report.seekP95Milliseconds+" ms; report "+output);
    }
}
