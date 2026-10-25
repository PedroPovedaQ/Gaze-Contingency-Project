using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace UnityEngine
{
    public class MonoBehaviour { public static object Game; public T GetComponent<T>() where T : class => Game as T; }
    public static class Application { public static string persistentDataPath; }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class Mathf { public static float Clamp01(float v) => Math.Max(0, Math.Min(1, v)); }
    public static class JsonUtility
    {
        public static T FromJson<T>(string s)
        {
            try { return new JavaScriptSerializer().Deserialize<T>(s); }
            catch (InvalidOperationException e) { throw new ArgumentException("Invalid JSON", e); }
        }
        public static string ToJson(object obj) => new JavaScriptSerializer().Serialize(obj);
    }
    public static class Debug { public static void LogWarning(string s) => Console.WriteLine(s); }
}
public class FindObjectGameManager
{
    public bool DegreeGuidesVisible = true;
    public int CurrentObjectiveIndex = 3;
    public int Changes;
    public void SetDegreeGuidesVisible(bool value) { DegreeGuidesVisible = value; Changes++; }
    public event Action<int, string, string> OnRoundTransitionStarted;
    public event Action<int> OnSearchStarted;
    public event Action<string> OnSessionStopped;
    public event Action<float> OnGameCompleted;
}
public class TrialReplayRecorder { public void RecordHeadsetControl(string detail) {} }
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
            var game = new FindObjectGameManager(); UnityEngine.MonoBehaviour.Game = game;
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
            string commandPath = Path.Combine(dir, "PreparationLogs", "live-headset-command.json");
            Action<string, string, long, double, string, bool> send = (id, session, revision, utc, action, visible) =>
            {
                File.WriteAllText(commandPath, UnityEngine.JsonUtility.ToJson(new {
                    schema = 1, id, sessionId = session, observedRevision = revision, observedUtc = utc, action, wallsVisible = visible }));
                Call(status, "ReadCommand"); Call(status, "Write");
            };
            var live = Read(path);
            string sessionId = (string)live["sessionId"], id1 = Guid.NewGuid().ToString("N");
            long rev = Convert.ToInt64(live["revision"]); double utc = Convert.ToDouble(live["updatedUtc"]);
            send(id1, sessionId, rev, utc, "set_walls", false);
            Require(!game.DegreeGuidesVisible && (string)Read(path)["commandResult"] == "applied", "Valid remote command applies and acknowledges actual state");
            send(id1, sessionId, rev, utc, "set_walls", true);
            Require(!game.DegreeGuidesVisible && game.Changes == 1, "Duplicate command cannot apply twice");
            send(Guid.NewGuid().ToString("N"), "another launch", rev, utc, "set_walls", true);
            Require(game.Changes == 1, "Previous launch commands cannot apply");
            send(Guid.NewGuid().ToString("N"), sessionId, rev, utc - 20, "set_walls", true);
            Require(game.Changes == 1, "Expired command cannot apply after sleep");
            send(Guid.NewGuid().ToString("N"), sessionId, rev, utc, "start_trial", true);
            Require(game.Changes == 1, "Unsupported actions cannot start a trial");
            Call(status, "OnApplicationPause", true);
            send(Guid.NewGuid().ToString("N"), sessionId, rev, utc, "set_walls", true);
            Require(game.Changes == 1, "Paused headset rejects settings");
            Call(status, "OnApplicationPause", false);
            send(Guid.NewGuid().ToString("N"), sessionId, rev, utc, "set_walls", true);
            Require(game.Changes == 1, "A command queued before sleep must not apply after wake");
            live = Read(path); rev = Convert.ToInt64(live["revision"]); utc = Convert.ToDouble(live["updatedUtc"]);
            send(Guid.NewGuid().ToString("N"), sessionId, rev + 100, utc, "set_walls", true);
            Require(game.Changes == 1, "Future revision is invalid");
            send(Guid.NewGuid().ToString("N"), sessionId, rev, utc, "set_walls", true);
            Require(game.DegreeGuidesVisible && game.Changes == 2, "Subsequent valid command restores walls");
            string auditPath = Path.Combine(dir, "PreparationLogs", "controls_" + sessionId, "commands.jsonl");
            Require(File.ReadAllLines(auditPath).Length == 2, "Only applied commands have audit records");
            var acknowledged = Read(path);
            File.WriteAllText(commandPath, "{broken"); Call(status, "ReadCommand"); Call(status, "Write");
            Require((string)Read(path)["commandId"] == (string)acknowledged["commandId"] &&
                (string)Read(path)["commandResult"] == "applied", "Malformed input preserves the last acknowledged result");
            Require(game.Changes == 2, "Malformed input is ignored without changing state");
            Call(status, "OnDestroy");
            Require((string)Read(path)["phase"] == "paused", "Destroyed producer must retire live status");
            Require(!File.Exists(path + ".tmp"), "Atomic replacement must not leave a partial reader file");
            Console.WriteLine("Headset preparation heartbeat checks passed.");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
