using System;
using UnityEditor;
using UnityEngine;

public static class StudyGazeAccuracyChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("Tools/Codex/Check Study Gaze Accuracy")]
    public static void Run()
    {
        var accuracy=new StudyGazeAccuracy();
        for(int step=0;step<5;step++)
        {
            double start=step*3+1;
            accuracy.StartPoint(step,start);
            for(int i=0;i<60;i++)
            {
                var row=new GazeObservation {recordingId="r",deviceId=9,raySource="HTC_binocular_mean",
                    htcQueryReceiptTime=start+i*.03,valid=true,alignmentApplied=true,
                    validationTargetPresent=true,worldOrigin=Vector3.zero,validationTarget=Vector3.forward*2,
                    unalignedWorldDirection=Quaternion.Euler(0,4,0)*Vector3.forward,
                    worldDirection=Quaternion.Euler(0,i<17?20:1,0)*Vector3.forward};
                accuracy.Observe(row,step);
                accuracy.Observe(row,step); // Heartbeat copies must not count twice.
            }
            accuracy.EndPoint(step);
        }
        var result=accuracy.Summarize();
        Check(result.passed && Math.Abs(result.meanDegrees-1)<.01,"Settling excludes transitions; known one-degree error passes");
        Check(result.points[0].observations==43,"Only distinct post-settle queries count");
        Check(!new StudyGazeAccuracy().Summarize().passed,"Missing dots cannot pass");
        var bad=new StudyGazeAccuracy();
        for(int step=0;step<5;step++)
        {
            bad.StartPoint(step,step*3+1);
            for(int i=0;i<60;i++)bad.Observe(new GazeObservation {recordingId="r",deviceId=9,raySource="OpenXR_EXT",inputEventTime=step*100+i+1,
                receiptTime=step*3+2+i*.01,valid=true,alignmentApplied=true,validationTargetPresent=true,
                worldDirection=Quaternion.Euler(0,6,0)*Vector3.forward,unalignedWorldDirection=Vector3.forward,validationTarget=Vector3.forward},step);
            bad.EndPoint(step);
        }
        Check(!bad.Summarize().passed && bad.Summarize().points[0].medianDegrees>5.9,"Large residual error is not a pass");
        var uncorrected=new StudyGazeAccuracy();uncorrected.StartPoint(0,1);
        uncorrected.Observe(new GazeObservation {raySource="OpenXR_EXT",inputEventTime=1,receiptTime=2,valid=true,
            validationTargetPresent=true,worldDirection=Vector3.forward,validationTarget=Vector3.forward},0);
        Check(uncorrected.Summarize().points[0].observations==0,"Lost alignment cannot be reported as corrected accuracy");
        foreach(bool helps in new[]{true,false})
        {
            var compare=new StudyGazeAccuracy(3);
            for(int step=0;step<5;step++)
            {
                compare.StartPoint(step,step*3+1);
                for(int i=0;i<60;i++)compare.Observe(new GazeObservation {raySource="OpenXR_EXT",inputEventTime=step*100+i+1,
                    receiptTime=step*3+2+i*.01,valid=true,alignmentApplied=true,validationTargetPresent=true,
                    worldDirection=Quaternion.Euler(0,1,0)*Vector3.forward,
                    unalignedWorldDirection=Quaternion.Euler(0,helps?4:.5f,0)*Vector3.forward,validationTarget=Vector3.forward},step);
                compare.EndPoint(step);
            }
            Check(compare.Summarize().passed==helps,"Correction must help on the separate check even when absolute error is within limits");
        }
        Debug.Log("[StudyGazeAccuracyChecks] PASS: settling, known angles, deduplication, incomplete checks, residual error and alignment loss.");
    }
}
