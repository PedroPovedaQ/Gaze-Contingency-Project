using System;
using System.Collections;

// Deterministic microphone/provider doubles; the production enrollment coroutine runs unchanged.
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public IEnumerator Pending;
        public T GetComponent<T>() => default(T);
        public void StartCoroutine(IEnumerator routine) { Pending = routine; }
        public void StopAllCoroutines() { Pending = null; }
        public static void Destroy(object value) { }
    }
    public class AudioClip { }
    public static class Time { public static float realtimeSinceStartup; }
    public static class Debug { public static void Log(object value) { } public static void LogWarning(object value) { } }
    public static class Mathf { public static int Clamp(int x, int low, int high) => Math.Max(low, Math.Min(high, x)); }
    public static class Microphone
    {
        public static string[] devices = { "test" };
        public static int Position, Capacity;
        public static bool Active;
        public static AudioClip Start(string device, bool loop, int seconds, int rate)
        { Position = 0; Capacity = seconds * rate; Active = true; return new AudioClip(); }
        public static int GetPosition(string device) => Position;
        public static bool IsRecording(string device) => Active;
        public static void End(string device) { Active = false; }
    }
}
public static class SessionConfig { public static string ParticipantId = "P001", SelfSimilarVoiceId; }
public static class WavUtility
{
    public static int EncodedFrames;
    public static byte[] EncodeFromClip(UnityEngine.AudioClip clip, int frames)
    { EncodedFrames = frames; return new byte[44 + frames * 2]; }
}
public class VoxtralClient
{
    public bool HasKey = true;
    public int Calls;
    public IEnumerator CloneVoice(byte[] wav, string name, Action<string> done, Action<string> error)
    { Calls++; done("test-clone"); yield break; }
}
public static class VoiceEnrollmentChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Run(bool finishEarly, bool interrupted)
    {
        UnityEngine.Time.realtimeSinceStartup = 0;
        var provider = new VoxtralClient();
        var enrollment = new VoiceEnrollment(); enrollment.Initialize(provider);
        enrollment.FinishRecording(); // An idle request must not stop the next recording.
        enrollment.RecordAndClone(40);
        var routine = enrollment.Pending;
        Require(routine.MoveNext(), "Recording should wait for input");
        enrollment.FinishRecording(); // Ignore accidental input before one second of samples.
        Require(!enrollment.CanFinishRecording, "Empty recording must not be submitted");
        int seconds = 0;
        do
        {
            seconds++; UnityEngine.Time.realtimeSinceStartup = seconds;
            UnityEngine.Microphone.Position = seconds * 16000;
            if (finishEarly && seconds == 5) { enrollment.FinishRecording(); enrollment.FinishRecording(); }
            if (interrupted) { UnityEngine.Microphone.Position = 0; UnityEngine.Microphone.Active = false; }
            if (!routine.MoveNext()) break;
            if (routine.Current is IEnumerator nested) while (nested.MoveNext()) { }
            Require(seconds < 45, "Recording failed to finish");
        } while (true);
        Require(!enrollment.IsBusy && !UnityEngine.Microphone.Active, "Microphone/busy state not released");
        if (interrupted)
            Require(enrollment.Current == VoiceEnrollment.State.Failed && provider.Calls == 0, "Interrupted empty audio was submitted");
        else
        {
            Require(enrollment.Current == VoiceEnrollment.State.Done && provider.Calls == 1, "Expected exactly one clone");
            Require(WavUtility.EncodedFrames == (finishEarly ? 5 : 40) * 16000, "Must encode captured samples, not unused buffer");
        }
    }
    public static void Main()
    {
        Run(true, false); Run(false, false); Run(false, true);
        Console.WriteLine("PASS: early finish, automatic deadline, duplicate/idle input, exact captured length and interrupted microphone.");
    }
}
