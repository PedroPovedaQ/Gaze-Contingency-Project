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
            foreach(var pair in new[]{("awaiting_consent",1),("select_voice",2),("interview",3),("fallback_available",3),("fallback",3),("eligibility_review",4),("interview_complete",5),("screening_stopped",4),("withdrawn",-1)})
            {
                intake.SetValue(app,fixture.Invoke(app,new object[]{pair.Item1}));
                if((int)step.GetValue(app)!=pair.Item2)throw new Exception("Intake step does not match existing phase: "+pair.Item1);
            }
            bool Property(string name)=>(bool)typeof(TrialReviewApp).GetProperty(name,flags).GetValue(app);
            void Field(object state,string name,object value)=>state.GetType().GetField(name).SetValue(state,value);
            foreach(string phase in new[]{"interview","fallback_available","fallback","eligibility_review","screening_stopped","interview_complete"})
            {
                var state=fixture.Invoke(app,new object[]{phase});intake.SetValue(app,state);
                bool reviewed=phase=="interview_complete";
                if(Property("CanCloneIntake")!=reviewed||Property("CanCacheIntake")!=reviewed)
                    throw new Exception("Clone/cache eligibility gate: "+phase);
                bool audioComplete=reviewed||phase=="eligibility_review"||phase=="screening_stopped";
                if(Property("AudioIntakeComplete")!=audioComplete)throw new Exception("Audio intake completion tick: "+phase);
                if(audioComplete&&((Array)state.GetType().GetField("answers").GetValue(state)).Length!=9)
                    throw new Exception("Completed capture must include all nine answers");
                if(reviewed)
                {
                    Field(state,"cloneState","not_started");
                    if(!Property("CanCloneIntake")||Property("CanCacheIntake"))throw new Exception("Positive review must allow automatic clone before cache");
                    Field(state,"speechSeconds",29f);
                    if(Property("CanCloneIntake"))throw new Exception("Speech threshold changed");
                    Field(state,"speechSeconds",30f);Field(state,"sampleWords",49);
                    if(Property("CanCloneIntake"))throw new Exception("Word threshold changed");
                    Field(state,"sampleWords",50);Field(state,"eligibility",null);
                    if(Property("CanCloneIntake"))throw new Exception("Missing review must block clone");
                }
                Field(state,"withdrawnUtc",1d);
                if(Property("CanCloneIntake")||Property("CanCacheIntake"))throw new Exception("Withdrawal must block clone/cache");
            }
            intake.SetValue(app,fixture.Invoke(app,new object[]{"interview"}));
            call("StartIntakeJob",new object[]{"clone",null,false});
            call("StartParticipantAudio",new object[]{"prepare"});
            call("InitializeLive",new object[]{new[]{"--live","--endpoint","fixture.invalid"}});
            foreach(string name in new[]{"PollIntake","PollParticipantAudio","ConnectLive","PollLive","SendIntake","InitializeRunExport","UpdateRunExport","UpdateEvaluation","InitializeCatalog","NeutralAudioControls"})call(name,null);
            foreach(string name in new[]{"m_IntakeFolder","m_IntakeJob","m_CloneJob","m_IntakeAudioJob","m_Bridge","m_LiveOutput","m_RunCsvProcess","m_Catalog","m_IntakeError"})
                if(typeof(TrialReviewApp).GetField(name,flags).GetValue(app)!=null)throw new Exception("Capture started work: "+name);
            var answer=(System.Collections.IEnumerator)typeof(TrialReviewApp).GetMethod("StartAnswer",flags).Invoke(app,null);
            if(answer.MoveNext())throw new Exception("Capture requested microphone authorization");
            Debug.Log("TRIAL_REVIEW_INTAKE_STEPPER_PASS nine answers then optional fallback then review; audio tick and clone/cache/withdrawal gates; capture microphone bypass");
            Debug.Log("TRIAL_REVIEW_CAPTURE_ISOLATION_PASS no jobs, polling, live, catalog or intake folder");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
