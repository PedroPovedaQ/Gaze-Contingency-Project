using System;
using UnityEngine;

public static class ParticipantFitChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        var window = new FitSeparationWindow(); FitSeparationMeasurement result = null;
        for (int i=0;i<45;i++) result=window.Add(i*.051,64+(i%2==0?.1f:-.1f),"synthetic","fixture");
        Check(result!=null && result.samples>=40 && Math.Abs(result.millimetres-64)<.11,"Stable median missing");
        Check(window.Add(2.4,float.NaN,"synthetic","fixture")==null,"NaN accepted");
        Check(window.Add(2.5,64,"synthetic","fixture")==null,"Invalid interval must reset window");
        Check(!FitSeparationWindow.Plausible(0)&&!FitSeparationWindow.Plausible(101),"Implausible fit accepted");
        window=new FitSeparationWindow();
        for(int i=0;i<60;i++)Check(window.Add(i*.051,i%2==0?60:65,"synthetic","fixture")==null,"Unstable fit accepted");
        var fit=new ParticipantFitMeasurements {viewSeparation=result};var copy=fit.Copy();copy.viewSeparation.millimetres=70;
        Check(fit.viewSeparation.millimetres!=70,"Fit copy aliases another participant");
        var change=new FitSeparationChange();fit=new ParticipantFitMeasurements();
        var view=new FitSeparationMeasurement{recorded=true,millimetres=64,source="fixture",recordedUtc="first"};
        change.Observe(fit,view,0);Check(fit.separationChange=="insufficient_observation","One stable window is not a no-change result");
        change.Observe(fit,view,2);Check(fit.separationChange=="no_change_observed","Stable repeated windows not recognized");
        view.millimetres=64.6f;view.recordedUtc="changed";change.Observe(fit,view,3);
        Check(fit.separationChange=="change_observed"&&Math.Abs(fit.separationChangeMm-.6f)<.001&&fit.separationChangeUtc=="changed","Change size/time missing");
        view.millimetres=64.1f;change.Observe(fit,view,4);Check(fit.separationChangeUtc=="changed","Smaller change erased observed event");
        view.source="other_api";view.millimetres=70;change.Observe(fit,view,5);
        Check(fit.separationChangeSource=="fixture"&&fit.separationChangeMm<1,"API switch created a false change");
        Check(fit.autoIpdStatus=="unavailable","Change must never infer auto-IPD status");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"auto-fit-csharp-fixture.json"),JsonUtility.ToJson(fit));
        Debug.Log("[ParticipantFitChecks] PASS stable median, invalid/missing/unstable windows, bounds and copy isolation (synthetic only)");
    }
}
