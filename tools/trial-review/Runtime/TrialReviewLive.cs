using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    [Serializable] public sealed class LiveStatus
    {
        public int schema, revision, audioRevision, files, verifiedFiles;
        public string state, message, endpoint, recording, archive;
        public double updatedUtc, lastDataUtc, receivedBytes, pendingBytes, replayUpdatedUtc, replayPendingBytes;
    }
    bool m_LivePanel, m_FollowLive, m_ViewLive;
    string m_Endpoint = "", m_LiveOutput, m_BridgeError = "";
    Process m_Bridge;
    LiveStatus m_LiveStatus;
    TrialReviewLiveStream m_LiveStream;
    TrialReviewLiveFollow m_LiveFollow = new TrialReviewLiveFollow();
    double m_LastStatusRead, m_LastAudioRefresh;
    int m_LiveIndexed, m_AudioRevision = -1;
    bool m_JoinLive, m_LiveAudioDirty, m_LiveSignalActive;
    bool WaitingForHeadset => m_FollowLive && !m_LiveSignalActive;
    static double UtcSeconds => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
    bool ReceiverConnected => m_LiveStatus != null && m_LiveStatus.state == "connected" && UtcSeconds - m_LiveStatus.updatedUtc < 8;
    bool ReceiverRunning => m_Bridge != null && !m_Bridge.HasExited;

    void InitializeLive(string[] args)
    {
        m_Endpoint = PlayerPrefs.GetString("LiveEndpoint", "");
        // Mono on macOS maps MyDocuments to HOME, so use the explicit Documents folder.
        m_LiveOutput = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Trial Review Live");
        int output = Array.IndexOf(args, "--live-output");
        if (output >= 0 && output + 1 < args.Length) m_LiveOutput = Path.GetFullPath(args[output + 1]);
        int endpoint = Array.IndexOf(args, "--endpoint");
        if (endpoint >= 0 && endpoint + 1 < args.Length) m_Endpoint = args[endpoint + 1];
        if (Array.IndexOf(args, "--live") >= 0) { m_LivePanel = true; ConnectLive(); }
    }

    static string Argument(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    void ConnectLive()
    {
        try
        {
            if (ReceiverRunning)
            {
                // Repeated clicks while following must not rewind or restart speech.
                if (!m_FollowLive) { m_JoinLive = true; m_LiveSignalActive = false; }
                m_FollowLive = true; return;
            }
            string script = Path.Combine(Application.streamingAssetsPath, "live_bridge.py");
            string python = "/usr/bin/python3";
            if (!File.Exists(python)) throw new FileNotFoundException("Python 3 is required for the wireless receiver.");
            Directory.CreateDirectory(m_LiveOutput);
            string stop = Path.Combine(m_LiveOutput, "stop-request");
            if (File.Exists(stop)) File.Delete(stop);
            m_BridgeError = ""; m_LiveStatus = null;
            m_Bridge?.Dispose();
            m_Bridge = new Process { StartInfo = new ProcessStartInfo(python,
                Argument(script) + " --output " + Argument(m_LiveOutput) + " --endpoint " + Argument(m_Endpoint.Trim()) + " --parent " + Process.GetCurrentProcess().Id)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } };
            m_Bridge.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) m_BridgeError = e.Data; };
            m_Bridge.Start(); m_Bridge.BeginErrorReadLine();
            m_FollowLive = true; m_JoinLive = true; m_LiveSignalActive = false;
            m_Playing = false; m_Audio?.Stop(); m_Error = null;
        }
        catch (Exception e) { m_Error = "Wireless receiver: " + e.Message; }
    }

    void StopReceiver()
    {
        if (ReceiverRunning) File.WriteAllText(Path.Combine(m_LiveOutput, "stop-request"), "stop");
        m_FollowLive = false; m_Playing = false; Sync(true);
    }

    void LeaveLiveView()
    {
        m_ViewLive = m_FollowLive = false;
        m_LiveSignalActive = false;
        m_LiveStream?.Dispose(); m_LiveStream = null;
    }

    void PollLive()
    {
        // Saved-only viewing does not need to touch the protected Documents archive.
        if(m_Bridge==null&&m_LiveStream==null)return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now - m_LastStatusRead > .1)
        {
            m_LastStatusRead = now;
            string statusPath = Path.Combine(m_LiveOutput, "live-status.json");
            if (File.Exists(statusPath))
            {
                try
                {
                    var status = JsonUtility.FromJson<LiveStatus>(File.ReadAllText(statusPath));
                    if (status.schema != 1) throw new InvalidDataException("Unsupported receiver status");
                    m_LiveStatus = status;
                    if (!string.IsNullOrEmpty(status.endpoint)) PlayerPrefs.SetString("LiveEndpoint", status.endpoint);
                }
                catch (Exception e) { m_Error = "Receiver status unavailable: " + e.Message; }
            }
            if (m_Bridge != null && m_Bridge.HasExited && !string.IsNullOrEmpty(m_BridgeError)) m_Error = m_BridgeError;
        }
        if (m_FollowLive && !string.IsNullOrEmpty(m_LiveStatus?.recording) && File.Exists(m_LiveStatus.recording) &&
            (m_LiveStream == null || m_LiveStream.Timeline.FilePath != m_LiveStatus.recording))
        {
            try
            {
                m_LiveStream?.Dispose();
                m_LiveStream = new TrialReviewLiveStream(m_LiveStatus.recording);
                m_LiveFollow = new TrialReviewLiveFollow(); m_LiveSignalActive = false;
                m_LiveIndexed = 0; m_JoinLive = true; m_ViewLive = false;
                m_Playing = false; m_Audio?.Stop(); m_Error = null;
            }
            catch (Exception e) { m_Error = e.Message; m_FollowLive = false; }
        }
        if (m_LiveStream == null) return;
        try
        {
            int added = m_LiveStream.Poll();
            var timeline = m_LiveStream.Timeline;
            if (timeline.Trials.Count == 0) return;
            if (m_Timeline != timeline)
            {
                bool first = m_Renderer == null || m_Renderer.RecordedHead;
                bool overhead = m_Renderer != null && m_Renderer.Overhead;
                float yaw=m_Renderer?.Yaw??35,pitch=m_Renderer?.Pitch??28,distance=m_Renderer?.Distance??7;
                m_Audio?.Dispose(); m_Renderer?.Dispose();
                m_Timeline = timeline;
                m_Renderer = new TrialReplayRenderer(timeline) { RecordedHead = first, Overhead = overhead, ShowGaze = false };
                m_Renderer.Yaw=yaw;m_Renderer.Pitch=pitch;m_Renderer.Distance=distance;
                m_Audio = new TrialReplayAudioPreview(timeline, new TrialReviewAudioOutput());
                m_Correction = new TrialReviewGazeCorrection(timeline, null);
                m_Path = timeline.FilePath; m_Hash = null; m_Summary = null; m_ExportPath = "";
                m_Samples.Clear(); m_Events.Clear(); m_ViewLive = true;
            }
            for (; m_LiveIndexed < timeline.Records.Count; m_LiveIndexed++)
            {
                var record = timeline.Records[m_LiveIndexed];
                m_LiveFollow.Observe(record);
                if (record.kind.StartsWith("audio_", StringComparison.Ordinal) || record.kind == "stop" || record.kind == "end") m_LiveAudioDirty = true;
                if (record.kind == "sample" || record.kind == "selection" && record.schema >= 2) m_Samples.Add(record);
                if (record.kind != "sample" && record.kind != "header" && record.kind != "trial") m_Events.Add(record);
            }
            if (now - m_LastAudioRefresh > .2 && (m_LiveAudioDirty || m_AudioRevision != m_LiveStatus?.audioRevision))
            {
                m_Audio.Refresh(timeline); m_LastAudioRefresh = now; m_AudioRevision = m_LiveStatus?.audioRevision ?? -1; m_LiveAudioDirty = false;
            }
            if (!m_FollowLive) return;
            bool active = m_LiveFollow.Ready(ReceiverConnected, UtcSeconds,
                m_LiveStatus?.replayUpdatedUtc ?? 0, m_LiveStatus?.replayPendingBytes ?? 0, m_LiveStream.CaughtUp, m_LiveSignalActive);
            if (!active)
            {
                m_LiveSignalActive = false; m_JoinLive = true;
                m_Playing = false; m_Audio?.Stop(); return;
            }
            double next = TrialReviewLiveFollow.Cursor(m_Time, timeline.Trials[0].time, timeline.Duration, m_JoinLive);
            m_LiveSignalActive = true; m_Speed = 1;
            if (m_JoinLive || next != m_Time)
            {
                m_Time = next; m_Sequence = long.MaxValue; m_JoinLive = false;
                m_Renderer.Prepare(timeline, m_Time); Sync(true);
            }
            // Freeze explicitly on transport loss. Never extrapolate gaze/head across a dropout.
            bool buffered = timeline.Duration - m_Time > .000001;
            m_Playing = ReceiverConnected && buffered;
            if (!m_Playing) Sync(false);
        }
        catch (Exception e)
        {
            m_Error = "Live preview stopped; wireless archiving continues: " + e.Message;
            LeaveLiveView(); m_Playing = false; Sync(true);
        }
    }

    void LiveControls(float width)
    {
        GUILayout.BeginArea(new Rect(22, 73, width - 44, 86));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Headset Wi-Fi IP", m_Label, GUILayout.Width(125));
        m_Endpoint = GUILayout.TextField(m_Endpoint, m_Field, GUILayout.Width(185));
        if (Button(ReceiverRunning ? "Follow live" : "Connect Wi-Fi", GUILayout.Width(125))) ConnectLive();
        if (ReceiverRunning && Button("Stop receiver", GUILayout.Width(125))) StopReceiver();
        if (Button("Open saved files", GUILayout.Width(140))) { Directory.CreateDirectory(m_LiveOutput); Application.OpenURL(new Uri(m_LiveOutput).AbsoluteUri); }
        GUILayout.EndHorizontal();
        string state = m_LiveStatus == null ? ReceiverRunning ? "Connecting…" : "Connect by USB once or enter the headset IP. Transfers use Wi-Fi." :
            $"{(ReceiverConnected ? "WI-FI CONNECTED" : "NOT RECEIVING")} · {m_LiveStatus.receivedBytes / 1000000:F1} MB saved · {m_LiveStatus.verifiedFiles}/{m_LiveStatus.files} files hash-verified · {m_LiveStatus.pendingBytes / 1000000:F1} MB catching up";
        GUILayout.Label(state, m_Heading);
        string view = m_FollowLive ? WaitingForHeadset ? "Waiting for headset signal" :
            $"Following · {Math.Max(0, m_Timeline.Duration - m_Time):F1}s behind received data" : "Reviewing history; receiver continues saving while connected";
        GUILayout.Label(m_LiveStatus?.state == "error" ? m_LiveStatus.message : view + " · Headset keeps the original recording", m_MutedLabel);
        GUILayout.EndArea();
    }
}
