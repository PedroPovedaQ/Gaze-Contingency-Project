using System;
using System.IO;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    const string IntakeProtocol = "audio-intake-v4-20261101";
    const int IntakeQuestionCount = 9;
    bool m_IntakeAutomatic, m_IntakePaused = true, m_WithdrawRequested;
    int m_IntakeEpoch, m_NoSpeechIndex = -1, m_NoSpeechRepeats, m_ActivityOffset;
    string m_QueuedIntakeControl;
    int m_QueuedQuestionIndex;
    TrialReviewVoiceActivity m_VoiceActivity;
    float[] m_ActivityFrame;
    IntakeJob m_WithdrawalJob;
    bool AutomaticCaptureAllowed => !m_CaptureMode && m_IntakeOpen && Consented && m_IntakeAutomatic &&
        !m_IntakePaused && !m_WithdrawRequested && (m_Intake.phase == "interview" || m_Intake.phase == "fallback");

    void PauseAutomaticIntake()
    {
        m_IntakePaused=true;m_IntakeEpoch++;
        m_InterviewSpeaker?.Stop();
        if(m_AnswerClip!=null)StopAnswer(false); // End microphone immediately; preserve partial take locally.
    }
    void QueueIntakeControl(string action)
    {
        if(m_CaptureMode)return;
        PauseAutomaticIntake();
        m_QueuedIntakeControl=action;m_QueuedQuestionIndex=m_Intake.questionIndex;
    }
    void AdvanceAutomaticIntake()
    {
        if(m_CaptureMode || m_Intake==null)return;
        FinishIntakeJob(ref m_WithdrawalJob);
        if(m_QueuedIntakeControl!=null && m_IntakeJob==null && !m_IntakeStarting && !m_PromptPlaying && Consented)
        {
            string action=m_QueuedIntakeControl;m_QueuedIntakeControl=null;
            m_IntakeAutomatic=true;m_IntakePaused=false;
            // An answer being saved when Skip was clicked may already have advanced. Never skip the next question.
            if(m_Intake.questionIndex==m_QueuedQuestionIndex)
            {
                m_PendingAnswer=null;
                if(action=="skip"){m_IntakeJob=StartIntakeJob("answer",skip:true);return;}
                m_NoSpeechRepeats=0;
            }
            m_IntakePlayedRevision=0;
        }
        if(!AutomaticCaptureAllowed || m_IntakeJob!=null || m_IntakeStarting || m_PromptPlaying || m_AnswerClip!=null)return;
        if(m_PendingAnswer!=null){m_IntakePaused=true;return;}
        if(string.IsNullOrEmpty(m_Intake.promptAudio)){m_IntakeJob=StartIntakeJob("prompt");return;}
        if(m_IntakePlayedRevision!=m_Intake.promptRevision)
        {m_IntakePlayedRevision=m_Intake.promptRevision;StartCoroutine(PlayInterviewPrompt());}
    }
    void PollIntakeCapture()
    {
        if(m_CaptureMode || m_AnswerClip==null)return;
        if(!AutomaticCaptureAllowed){StopAnswer(false);return;}
        int position=Microphone.IsRecording(m_IntakeMic)?Microphone.GetPosition(m_IntakeMic):m_AnswerClip.samples;
        if(m_ActivityFrame==null || m_ActivityFrame.Length!=TrialReviewVoiceActivity.FrameSamples*m_AnswerClip.channels)
            m_ActivityFrame=new float[TrialReviewVoiceActivity.FrameSamples*m_AnswerClip.channels];
        while(m_ActivityOffset+TrialReviewVoiceActivity.FrameSamples<=position && m_VoiceActivity.EndReason==null)
        {
            m_AnswerClip.GetData(m_ActivityFrame,m_ActivityOffset);m_VoiceActivity.Feed(m_ActivityFrame,m_AnswerClip.channels);
            m_ActivityOffset+=TrialReviewVoiceActivity.FrameSamples;
        }
        if(m_VoiceActivity.EndReason==null)return;
        string reason=m_VoiceActivity.EndReason;
        try
        {
            File.WriteAllText(Path.Combine(m_IntakeFolder,m_CaptureName+".end.json"),JsonUtility.ToJson(new IntakeCaptureEnd {
                reason=reason, speechDetected=m_VoiceActivity.SpeechDetected, seconds=m_VoiceActivity.Seconds,
                silenceSeconds=m_VoiceActivity.SilenceSeconds, repeat=m_NoSpeechRepeats }));
        }
        catch(Exception e){m_IntakePaused=true;m_IntakeError="Capture receipt could not be saved: "+e.Message;}
        finally{StopAnswer(!m_IntakePaused && reason!="no_speech");}
        if(m_IntakePaused)return;
        if(reason!="no_speech")return;
        m_PendingAnswer=null; // Silent attempts remain on disk, never count as usable answers or clone input.
        if(RepeatAfterNoSpeech())m_IntakePlayedRevision=0;
        else m_IntakeJob=StartIntakeJob("answer",skip:true);
    }
    bool RepeatAfterNoSpeech()
    {
        if(m_NoSpeechIndex!=m_Intake.questionIndex){m_NoSpeechIndex=m_Intake.questionIndex;m_NoSpeechRepeats=0;}
        return m_NoSpeechRepeats++==0;
    }
    [Serializable] sealed class IntakeCaptureEnd
    { public string reason; public bool speechDetected; public double seconds,silenceSeconds; public int repeat; }
    void DrawAutomaticIntakeControls()
    {
        GUI.enabled=Consented && !m_WithdrawRequested;
        GUILayout.BeginHorizontal();
        if(PrimaryButton(m_IntakePaused?"Start / resume automatic intake":"Pause intake"))
        {
            if(!m_IntakePaused)PauseAutomaticIntake();
            else {m_IntakeAutomatic=true;m_IntakePaused=false;m_IntakePlayedRevision=0;m_IntakeError=null;}
        }
        if(Button("Repeat question"))QueueIntakeControl("repeat");
        if(Button("Skip this question"))QueueIntakeControl("skip");
        GUILayout.EndHorizontal();
        GUI.enabled=Consented && m_IntakePaused && m_IntakeJob==null;
        if(m_PendingAnswer!=null && Button("Save paused answer / continue"))
        {m_IntakePaused=false;m_IntakeJob=StartIntakeJob("answer",m_PendingAnswer);}
        GUI.enabled=true;
    }
}
