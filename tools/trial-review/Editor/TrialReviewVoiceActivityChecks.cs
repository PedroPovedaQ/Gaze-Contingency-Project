using System;
using UnityEngine;

public static class TrialReviewVoiceActivityChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        var speech=new float[480];var silence=new float[480];var dc=new float[480];var quiet=new float[480];
        for(int i=0;i<480;i++){speech[i]=.1f*(float)Math.Sin(2*Math.PI*220*i/24000);dc[i]=.3f;quiet[i]=speech[i]*.01f;}
        void Feed(TrialReviewVoiceActivity vad,float[] frame,int count){for(int i=0;i<count;i++)vad.Feed(frame,1);}
        foreach(bool open in new[]{false,true})
        {
            var vad=new TrialReviewVoiceActivity(open);Feed(vad,speech,20);
            int frames=open?250:150;
            Feed(vad,silence,frames-1);Check(vad.EndReason==null,"Must wait full silence window");
            Feed(vad,silence,1);Check(vad.EndReason=="sustained_silence","End at configured silence");
        }
        var pauses=new TrialReviewVoiceActivity(true);Feed(pauses,speech,50);Feed(pauses,silence,200);
        Check(pauses.EndReason==null,"Natural four-second pause must not cut an open answer");
        Feed(pauses,speech,50);Feed(pauses,silence,249);Check(pauses.EndReason==null,"Speech resets silence clock");
        Feed(pauses,silence,1);Check(pauses.EndReason=="sustained_silence","Five-second open-answer ending");
        foreach(var noise in new[]{silence,dc,quiet})
        {
            var vad=new TrialReviewVoiceActivity(true);Feed(vad,noise,499);Check(vad.EndReason==null,"Wait ten seconds for speech");
            Feed(vad,noise,1);Check(!vad.SpeechDetected&&vad.EndReason=="no_speech","DC and quiet noise are not speech");
        }
        var click=new TrialReviewVoiceActivity(false);Feed(click,speech,1);Feed(click,silence,499);
        Check(!click.SpeechDetected&&click.EndReason=="no_speech","Single impulse cannot satisfy speech onset");
        var cap=new TrialReviewVoiceActivity(true);Feed(cap,speech,6000);Check(cap.EndReason=="maximum_duration","120 second cap");
        var stereo=new float[960];for(int i=0;i<480;i++)stereo[i*2]=stereo[i*2+1]=speech[i];
        var multi=new TrialReviewVoiceActivity(true);for(int i=0;i<10;i++)multi.Feed(stereo,2);
        Check(multi.SpeechDetected,"Multichannel input is downmixed");
        Debug.Log("TRIAL_REVIEW_VAD_PASS synthetic PCM: 3s/5s silence, 4s natural pause, reset, 10s no speech, DC/noise/impulse, 120s cap, stereo");
    }
}
