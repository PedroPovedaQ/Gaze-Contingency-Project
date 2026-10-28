using System;
using System.Reflection;
using UnityEngine;

public static class TrialReviewCaptureChecks
{
    public static void Run()
    {
        var go=new GameObject("Capture isolation checks");
        try
        {
            var app=go.AddComponent<TrialReviewApp>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(TrialReviewApp).GetField("m_CaptureMode",flags).SetValue(app,true);
            Action<string,object[]> call=(name,args)=>typeof(TrialReviewApp).GetMethod(name,flags).Invoke(app,args);
            call("StartIntakeJob",new object[]{"clone",null,false});
            call("StartParticipantAudio",new object[]{"prepare"});
            call("InitializeLive",new object[]{new[]{"--live","--endpoint","fixture.invalid"}});
            foreach(string name in new[]{"PollIntake","PollParticipantAudio","ConnectLive","PollLive","SendIntake","InitializeRunExport","UpdateRunExport","UpdateEvaluation","InitializeCatalog"})call(name,null);
            foreach(string name in new[]{"m_IntakeFolder","m_IntakeJob","m_CloneJob","m_IntakeAudioJob","m_Bridge","m_LiveOutput","m_RunCsvProcess","m_Catalog"})
                if(typeof(TrialReviewApp).GetField(name,flags).GetValue(app)!=null)throw new Exception("Capture started work: "+name);
            Debug.Log("TRIAL_REVIEW_CAPTURE_ISOLATION_PASS no jobs, polling, live, catalog or intake folder");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
