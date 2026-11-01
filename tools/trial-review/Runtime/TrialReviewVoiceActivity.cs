using System;

// Local energy-based activity check. Not a validated speech/noise classifier.
public sealed class TrialReviewVoiceActivity
{
    public const int Rate = 24000, FrameSamples = 480;
    public const double Threshold = .01, OnsetSeconds = .2, NoSpeechSeconds = 10, MaximumSeconds = 120;
    public readonly double SilenceSeconds;
    public bool SpeechDetected { get; private set; }
    public string EndReason { get; private set; }
    public double Seconds => m_Frames * .02;
    public double SilentSeconds => m_SilentFrames * .02;
    int m_Frames, m_ActiveFrames, m_SilentFrames;
    public TrialReviewVoiceActivity(bool openEnded) { SilenceSeconds = openEnded ? 5 : 3; }
    public void Feed(float[] interleaved, int channels)
    {
        if (EndReason != null) return;
        if (channels < 1 || interleaved.Length != FrameSamples * channels) throw new ArgumentException("Expected 20 ms PCM");
        double sum = 0, squares = 0;
        for (int i=0; i<FrameSamples; i++)
        {
            double value=0;
            for(int c=0;c<channels;c++)value+=interleaved[i*channels+c]/channels;
            sum+=value;squares+=value*value;
        }
        double mean=sum/FrameSamples;
        bool active=Math.Sqrt(Math.Max(0,squares/FrameSamples-mean*mean))>=Threshold;
        m_Frames++;
        if(active){m_ActiveFrames++;m_SilentFrames=0;}else{m_ActiveFrames=0;m_SilentFrames++;}
        if(m_ActiveFrames>=10)SpeechDetected=true;
        if(Seconds>=MaximumSeconds)EndReason="maximum_duration";
        else if(SpeechDetected && SilentSeconds>=SilenceSeconds)EndReason="sustained_silence";
        else if(!SpeechDetected && Seconds>=NoSpeechSeconds)EndReason="no_speech";
    }
}
