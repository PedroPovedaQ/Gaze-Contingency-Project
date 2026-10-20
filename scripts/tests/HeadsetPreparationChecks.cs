using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace UnityEngine
{
    public class MonoBehaviour { public T GetComponent<T>() where T : class => null; }
    public static class Application { public static string persistentDataPath; }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class Mathf { public static float Clamp01(float v) => Math.Max(0, Math.Min(1, v)); }
    public static class JsonUtility
    {
        public static string ToJson(object obj) => new JavaScriptSerializer().Serialize(obj);
    }
    public static class Debug { public static void LogWarning(string s) => Console.WriteLine(s); }
}
public class FindObjectGameManager
{
    public event Action<int, string, string> OnRoundTransitionStarted;
    public event Action<int> OnSearchStarted;
    public event Action<string> OnSessionStopped;
    public event Action<float> OnGameCompleted;
}
static class HeadsetPreparationChecks
{
    static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Dictionary<string, object> Read(string path) =>
        (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path));
    public static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "headset-status-" + Guid.NewGuid().ToString("N"));
        UnityEngine.Application.persistentDataPath = dir;
        try
        {
            var status = new HeadsetPreparationStatus();
            string path = Path.Combine(dir, "PreparationLogs", "live-headset-state.json");
            Call(status, "Awake");
            var first = Read(path);
            Require((string)first["phase"] == "ready", "Initial setup must not claim a trial or loading");
            status.Set("preparing", "Preparing experiment", true, .4f);
            UnityEngine.Time.realtimeSinceStartupAsDouble = .11;
            Call(status, "Update");
            var preparing = Read(path);
            Require((string)preparing["phase"] == "preparing" && (bool)preparing["progressKnown"], "Loading must exist before a replay");
            Require(!Directory.Exists(Path.Combine(dir, "GazeReplays")), "Status must not start replay");
            UnityEngine.Time.realtimeSinceStartupAsDouble = .7;
            Call(status, "Update");
            var heartbeat = Read(path);
            Require((int)heartbeat["revision"] > (int)preparing["revision"], "Unchanged progress needs a fresh heartbeat");
            Require((string)heartbeat["sessionId"] == (string)first["sessionId"], "Heartbeat identity must be stable");
            Call(status, "OnApplicationPause", true);
            Require((string)Read(path)["phase"] == "paused" && !(bool)Read(path)["progressKnown"], "Pause must override loading");
            Call(status, "OnApplicationPause", false);
            Require((string)Read(path)["phase"] == "preparing", "Resume restores real setup state");
            Call(status, "SearchStarted", 1);
            UnityEngine.Time.realtimeSinceStartupAsDouble = 1;
            Call(status, "Update");
            Require((string)Read(path)["phase"] == "trial", "Search event must clear loading");
            Call(status, "Stopped", "sensitive detail");
            UnityEngine.Time.realtimeSinceStartupAsDouble = 1.2;
            Call(status, "Update");
            Require((string)Read(path)["phase"] == "error" && !File.ReadAllText(path).Contains("sensitive"), "Errors must use generic status");
            Call(status, "OnDestroy");
            Require((string)Read(path)["phase"] == "paused", "Destroyed producer must retire live status");
            Require(!File.Exists(path + ".tmp"), "Atomic replacement must not leave a partial reader file");
            Console.WriteLine("Headset preparation heartbeat checks passed.");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
