using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Small live-status sidecar available before replay has a trial header.
/// Never starts a trial, changes its clock, or writes participant/voice identifiers.
/// </summary>
public sealed class HeadsetPreparationStatus : MonoBehaviour
{
    [Serializable]
    sealed class Status
    {
        public int schema = 1;
        public string sessionId = Guid.NewGuid().ToString("N");
        public long revision;
        public string phase = "ready";
        public string message = "Voice setup";
        public bool progressKnown;
        public float progress01;
        public double updatedUtc;
    }

    readonly Status m_Status = new Status();
    FindObjectGameManager m_Game;
    string m_Path;
    double m_NextWrite;
    bool m_Paused, m_Unfocused, m_Warned;

    void Awake()
    {
        m_Path = Path.Combine(Application.persistentDataPath, "PreparationLogs", "live-headset-state.json");
        m_Game = GetComponent<FindObjectGameManager>();
        if (m_Game != null)
        {
            m_Game.OnRoundTransitionStarted += Transition;
            m_Game.OnSearchStarted += SearchStarted;
            m_Game.OnSessionStopped += Stopped;
            m_Game.OnGameCompleted += Completed;
        }
        Write();
    }

    public void Set(string phase, string message, bool progressKnown = false, float progress = 0)
    {
        progress = float.IsNaN(progress) || float.IsInfinity(progress) ? 0 : Mathf.Clamp01(progress);
        bool changed = m_Status.phase != phase || m_Status.message != message ||
            m_Status.progressKnown != progressKnown;
        m_Status.phase = phase;
        m_Status.message = message;
        m_Status.progressKnown = progressKnown;
        m_Status.progress01 = progress;
        // Progress callbacks can arrive every frame; phase changes still write promptly
        // and ordinary progress is coalesced into the half-second heartbeat.
        if (changed) m_NextWrite = Math.Min(m_NextWrite, Time.realtimeSinceStartupAsDouble + .1);
    }

    void Update()
    {
        if (Time.realtimeSinceStartupAsDouble >= m_NextWrite) Write();
    }

    void Transition(int round, string color, string shape) => Set("ready", "Waiting for the next round");
    void SearchStarted(int round) => Set("trial", "Trial in progress");
    void Stopped(string reason) => Set("error", "Session stopped");
    void Completed(float elapsed) => Set("ready", "Session complete");

    void OnApplicationPause(bool paused) { m_Paused = paused; Write(); }
    void OnApplicationFocus(bool focused) { m_Unfocused = !focused; Write(); }
    void OnApplicationQuit() { m_Paused = true; Write(); }

    void Write()
    {
        if (string.IsNullOrEmpty(m_Path)) return;
        m_NextWrite = Time.realtimeSinceStartupAsDouble + .5;
        string phase = m_Status.phase, message = m_Status.message;
        bool known = m_Status.progressKnown;
        try
        {
            if (m_Paused || m_Unfocused)
            {
                m_Status.phase = "paused";
                m_Status.message = "Headset app paused";
                m_Status.progressKnown = false;
            }
            m_Status.revision++;
            m_Status.updatedUtc = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Directory.CreateDirectory(Path.GetDirectoryName(m_Path));
            string temp = m_Path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(m_Status));
            // Same-volume atomic rename prevents the receiver reading partial JSON.
            if (File.Exists(m_Path)) File.Replace(temp, m_Path, null);
            else File.Move(temp, m_Path);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
        {
            if (!m_Warned)
            {
                m_Warned = true;
                Debug.LogWarning("[HeadsetStatus] Live status unavailable: " + e.GetType().Name);
            }
        }
        finally
        {
            m_Status.phase = phase;
            m_Status.message = message;
            m_Status.progressKnown = known;
        }
    }

    void OnDestroy()
    {
        if (m_Game != null)
        {
            m_Game.OnRoundTransitionStarted -= Transition;
            m_Game.OnSearchStarted -= SearchStarted;
            m_Game.OnSessionStopped -= Stopped;
            m_Game.OnGameCompleted -= Completed;
        }
        m_Paused = true;
        Write();
    }
}
