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
            var fixture=typeof(TrialReviewApp).GetMethod("CaptureIntake",flags);
            var intake=typeof(TrialReviewApp).GetField("m_Intake",flags);
            var step=typeof(TrialReviewApp).GetProperty("IntakeStep",flags);
            foreach(var pair in new[]{("awaiting_consent",1),("select_voice",2),("interview",3),("eligibility_review",4),("interview_complete",5),("screening_stopped",4),("withdrawn",-1)})
            {
                intake.SetValue(app,fixture.Invoke(app,new object[]{pair.Item1}));
                if((int)step.GetValue(app)!=pair.Item2)throw new Exception("Intake step does not match existing phase: "+pair.Item1);
            }
            intake.SetValue(app,fixture.Invoke(app,new object[]{"interview"}));
            call("StartIntakeJob",new object[]{"clone",null,false});
            call("StartParticipantAudio",new object[]{"prepare"});
            call("InitializeLive",new object[]{new[]{"--live","--endpoint","fixture.invalid"}});
            foreach(string name in new[]{"PollIntake","PollParticipantAudio","ConnectLive","PollLive","SendIntake","InitializeRunExport","UpdateRunExport","UpdateEvaluation","InitializeCatalog"})call(name,null);
            foreach(string name in new[]{"m_IntakeFolder","m_IntakeJob","m_CloneJob","m_IntakeAudioJob","m_Bridge","m_LiveOutput","m_RunCsvProcess","m_Catalog","m_IntakeError"})
                if(typeof(TrialReviewApp).GetField(name,flags).GetValue(app)!=null)throw new Exception("Capture started work: "+name);
            var answer=(System.Collections.IEnumerator)typeof(TrialReviewApp).GetMethod("StartAnswer",flags).Invoke(app,null);
            if(answer.MoveNext())throw new Exception("Capture requested microphone authorization");
            Debug.Log("TRIAL_REVIEW_INTAKE_STEPPER_PASS existing phases, eligibility return to audio intake, capture microphone bypass");
            Debug.Log("TRIAL_REVIEW_CAPTURE_ISOLATION_PASS no jobs, polling, live, catalog or intake folder");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
