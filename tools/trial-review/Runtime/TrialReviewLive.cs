using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    [Serializable] public sealed class LiveStatus
    {
        public int schema, revision, audioRevision, files, verifiedFiles;
        public string state, message, endpoint, recording, archive, controlId, controlResult;
        public double updatedUtc, lastDataUtc, receivedBytes, pendingBytes, replayUpdatedUtc, replayPendingBytes, headsetStateObservedUtc;
        public TrialReviewHeadsetState headsetState;
    }
    bool m_LivePanel, m_FollowLive, m_ViewLive;
    string m_Endpoint = "", m_LiveOutput, m_BridgeError = "";
    Process m_Bridge;
    LiveStatus m_LiveStatus;
    TrialReviewLiveStream m_LiveStream;
    TrialReviewLiveFollow m_LiveFollow = new TrialReviewLiveFollow();
    TrialReviewLiveAudioClock m_LiveAudioClock = new TrialReviewLiveAudioClock();
    double m_LastStatusRead, m_LastAudioRefresh;
    int m_LiveIndexed, m_AudioRevision = -1;
    bool m_JoinLive, m_LiveAudioDirty, m_LiveSignalActive, m_DrainingLive;
    bool HeadsetPreparing => m_FollowLive && TrialReviewHeadsetState.Fresh(m_LiveStatus?.headsetState, ReceiverConnected, UtcSeconds, m_LiveStatus?.headsetStateObservedUtc ?? 0) && m_LiveStatus.headsetState.phase == "preparing";
    bool WaitingForHeadset => m_FollowLive && (!m_LiveSignalActive || HeadsetPreparing);
    string LiveWaitingReason => HeadsetPreparing ? "Preparing experiment" : m_LiveFollow.WaitingReason(ReceiverConnected, UtcSeconds,
        m_LiveStatus?.replayUpdatedUtc ?? 0, m_LiveStatus?.replayPendingBytes ?? 0, m_LiveStream?.CaughtUp ?? true);
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
        if (!m_DetachedReview && Array.IndexOf(args, "--live") >= 0) { m_LivePanel = true; ConnectLive(); }
    }

    static string Argument(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    void ConnectLive()
    {
        if(m_DetachedReview) { m_Error="Live recording stays in the main window."; return; }
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
            string commandPath = Path.Combine(m_LiveOutput, "headset-command.json");
            if (File.Exists(commandPath)) File.Delete(commandPath);
            m_HeadsetCommand = null; m_ControlError = null;
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

    void SetLiveMode(bool enabled)
    {
        if(enabled){m_LivePanel=true;ConnectLive();return;}
        // Pause following at the current cursor while preserving the background archive transfer.
        m_FollowLive=false;m_JoinLive=false;m_LiveSignalActive=false;m_Playing=false;
        m_Audio?.Stop();
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
                m_LiveFollow = new TrialReviewLiveFollow(); m_LiveAudioClock = new TrialReviewLiveAudioClock(); m_LiveSignalActive = false;
                m_LiveIndexed = 0; m_JoinLive = true; m_ViewLive = false;
                m_Playing = false; m_Audio?.Stop(); m_Error = null;
            }
            catch (Exception e) { m_Error = e.Message; m_FollowLive = false; }
        }
        if (HeadsetPreparing) { m_LiveSignalActive = false; m_Playing = false; m_Audio?.Stop(); }
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
                InitializeRunExport();
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
            // Starts/cancellations take effect on the next viewer update. Only
            // asset-arrival retries are throttled; cached speech need not wait.
            if (m_LiveAudioDirty || now - m_LastAudioRefresh > .2 && m_AudioRevision != m_LiveStatus?.audioRevision)
            {
                m_Audio.Refresh(timeline); m_LastAudioRefresh = now; m_AudioRevision = m_LiveStatus?.audioRevision ?? -1; m_LiveAudioDirty = false;
            }
            if (!m_FollowLive) return;
            bool active = !HeadsetPreparing && m_LiveFollow.Ready(ReceiverConnected, UtcSeconds,
                m_LiveStatus?.replayUpdatedUtc ?? 0, m_LiveStatus?.replayPendingBytes ?? 0, m_LiveStream.CaughtUp, m_LiveSignalActive);
            m_DrainingLive = !m_JoinLive && !HeadsetPreparing && ReceiverConnected && m_LiveFollow.CanDrain(m_Time);
            active |= m_DrainingLive;
            if (!active)
            {
                m_LiveSignalActive = false;
                m_Playing = false; m_Audio?.Stop(); return;
            }
            double next = TrialReviewLiveFollow.Cursor(m_Time, timeline.Trials[0].time, timeline.Duration, m_JoinLive);
            bool joining = m_JoinLive;
            if (joining) m_LiveAudioClock.Resume();
            if (!joining)
            {
                next = Math.Max(next, Math.Min(timeline.Duration, m_Time + Time.unscaledDeltaTime));
                next = m_LiveAudioClock.Advance(m_Time, next, Time.unscaledDeltaTime, now, timeline, m_Audio.Plan);
            }
            m_LiveSignalActive = true; m_Speed = 1;
            m_Time = next; m_Sequence = long.MaxValue; m_JoinLive = false;
            m_Renderer.Prepare(timeline, m_Time);
            m_Playing = ReceiverConnected && timeline.Duration - m_Time > .000001;
            Sync(joining); // Catch-up no longer force-restarts an active phrase.

        }
        catch (Exception e)
        {
            m_Error = "Live preview stopped; wireless archiving continues: " + e.Message;
            LeaveLiveView(); m_Playing = false; Sync(true);
        }
    }

    [Serializable] sealed class HeadsetCommand
    {
        public int schema = 1;
        public string id, sessionId, action = "set_walls";
        public int observedRevision;
        public double createdUtc, observedUtc;
        public bool wallsVisible;
        public ParticipantSessionAssignment assignment;
    }
    HeadsetCommand m_HeadsetCommand;
    string m_ControlError;

    void HeadsetControls()
    {
        var state = m_LiveStatus?.headsetState;
        bool fresh = TrialReviewHeadsetState.Fresh(state, ReceiverConnected, UtcSeconds, m_LiveStatus?.headsetStateObservedUtc ?? 0);
        bool available = TrialReviewHeadsetState.CanControl(state, fresh, m_FollowLive, m_DetachedReview);
        string feedback = "Changes walls and degree labels inside the headset";
        bool pending = false;
        if (m_HeadsetCommand != null)
        {
            if (state?.sessionId != m_HeadsetCommand.sessionId) feedback = "Headset session changed; request retired";
            else if (fresh && state.commandId == m_HeadsetCommand.id)
                feedback = state.commandResult == "applied" ? "Confirmed by headset" : "Headset rejected request; send again";
            else if (m_LiveStatus?.controlId == m_HeadsetCommand.id &&
                (m_LiveStatus.controlResult == "failed" || m_LiveStatus.controlResult == "rejected"))
                feedback = "Request not delivered; send again";
            else if (UtcSeconds - m_HeadsetCommand.createdUtc > 12) feedback = "No confirmation; check connection and send again";
            else { pending = true; feedback = "Waiting for headset confirmation…"; }
        }
        if (!fresh) feedback = "Waiting for a fresh headset connection";
        else if (!state.wallControlAvailable) feedback = "Headset build does not support remote walls";
        else if (state.phase == "paused") feedback = "Wake the headset to change its settings";
        if (!string.IsNullOrEmpty(m_ControlError)) feedback = m_ControlError;
        if (fresh && m_FollowLive && !m_DetachedReview)
        {
            var assistance = state.assistance;
            bool recorded = assistance != null && !string.IsNullOrEmpty(assistance.provider);
            GUILayout.Label(new GUIContent(recorded ? "Headset selected: " + assistance.SelectionLabel + " · " + assistance.ConnectionLabel : "Headset assistance: not recorded",
                recorded ? "Model: " + (assistance.modelVersion ?? "not used") + "\nPolicy: " + assistance.policy + "\nConfigured does not guarantee cloud reachability." : "Older headset build has no provider status"), m_Heading);
            GUILayout.Label(recorded ? "Last accepted: " + assistance.LastDecisionLabel + " · Queued does not confirm playback" : "No model decision recorded", m_MutedLabel);
        }
        else GUILayout.Label("Headset assistance: waiting for a fresh live status", m_MutedLabel);
        GUILayout.BeginHorizontal();
        if (Button("New participant session", GUILayout.Width(190))) OpenIntake();
        GUILayout.Label("Headset controls", m_Heading, GUILayout.Width(150));
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && available && !pending;
        if (Button(fresh && state.wallControlAvailable ? state.wallsVisible ? "Walls: ON · Hide" : "Walls: OFF · Show" : "Walls: unknown", GUILayout.Width(175)))
        {
            try
            {
                var command = new HeadsetCommand { id = Guid.NewGuid().ToString("N"), sessionId = state.sessionId,
                    observedRevision = state.revision, observedUtc = state.updatedUtc, createdUtc = UtcSeconds, wallsVisible = !state.wallsVisible };
                string path = Path.Combine(m_LiveOutput, "headset-command.json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(command));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                m_HeadsetCommand = command; m_ControlError = null;
            }
            catch (Exception e) { m_ControlError = "Could not send headset setting: " + e.Message; }
        }
        GUI.enabled = wasEnabled;
        GUILayout.Label(feedback, m_MutedLabel);
        GUILayout.EndHorizontal();
    }

    void LiveControls(float width)
    {
        GUILayout.BeginArea(new Rect(22, 73, width - 44, m_FollowLive ? 182 : 86));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Headset Wi-Fi IP", m_Label, GUILayout.Width(125));
        GUI.SetNextControlName("text:wifi-endpoint");
        m_Endpoint = GUILayout.TextField(m_Endpoint, m_Field, GUILayout.Width(185));
        if (Button(ReceiverRunning ? "Resume live" : "Connect Wi-Fi", GUILayout.Width(125))) SetLiveMode(true);
        if (ReceiverRunning && Button("Stop receiver", GUILayout.Width(125))) StopReceiver();
        if (Button("Review window", GUILayout.Width(130))) OpenReviewWindow();
        if (Button("Open saved files", GUILayout.Width(140))) { Directory.CreateDirectory(m_LiveOutput); Application.OpenURL(new Uri(m_LiveOutput).AbsoluteUri); }
        GUILayout.FlexibleSpace();
        GUILayout.Label("Viewer audio",m_Label,GUILayout.Width(95));
        if(Button(m_Muted?"Unmute voice":"Mute voice",GUILayout.Width(110)))SetViewerAudio(m_Volume,!m_Muted);
        float volume=GUILayout.HorizontalSlider(m_Volume,0,1,GUILayout.Width(110));
        GUILayout.Label($"{m_Volume*100:0}%",m_Label,GUILayout.Width(48));
        if(volume!=m_Volume)SetViewerAudio(volume,m_Muted);
        GUILayout.EndHorizontal();
        string state = m_LiveStatus == null ? ReceiverRunning ? "Connecting…" : "Connect by USB once or enter the headset IP. Transfers use Wi-Fi." :
            $"{(ReceiverConnected ? "WI-FI CONNECTED" : "NOT RECEIVING")} · {m_LiveStatus.receivedBytes / 1000000:F1} MB saved · {m_LiveStatus.verifiedFiles}/{m_LiveStatus.files} files hash-verified · {m_LiveStatus.pendingBytes / 1000000:F1} MB catching up";
        GUILayout.Label(state, m_Heading);
        string view = m_FollowLive ? WaitingForHeadset ? LiveWaitingReason :
            $"Following · {Math.Max(0, m_Timeline.Duration - m_Time):F1}s behind received data" : "Reviewing history; receiver continues saving while connected";
        if (m_FollowLive && m_DrainingLive) view = "Playing buffered recording · " + view;
        if (m_FollowLive && m_LiveAudioClock.WaitingForAudio) view = "Buffering recorded speech audio · " + view;
        if (m_LiveAudioClock.UnavailableCount > 0) view += $" · {m_LiveAudioClock.UnavailableCount} audio asset(s) unavailable after 5 s";
        GUILayout.Label(m_LiveStatus?.state == "error" ? m_LiveStatus.message : view + " · Headset keeps the original recording", m_MutedLabel);
        if (m_FollowLive) HeadsetControls();
        GUILayout.EndArea();
    }
}

// Headset-owned heartbeat; never infer preparation from a silent replay or cached WAVs.
[Serializable]
public sealed class TrialReviewHeadsetState
{
    public AssistanceRuntimeSnapshot assistance;
    public int schema, revision;
    public bool intakeAvailable;
    public string participantSessionId, participantCode, configuration, assignmentState, nextParticipantCode;
    public string sessionId, phase;
    public bool progressKnown, wallControlAvailable, wallsVisible;
    public string commandId, commandResult;
    public float progress01;
    public double updatedUtc;
    public static bool CanControl(TrialReviewHeadsetState state, bool fresh, bool live, bool detached) =>
        fresh && live && !detached && state != null && state.wallControlAvailable && state.phase != "paused";

    public static bool Fresh(TrialReviewHeadsetState state, bool connected, double now, double observed)
    {
        // Receipt age is independent of headset/Mac clock offset. The receiver only
        // advances observed time when the headset heartbeat itself changes.
        return connected && state != null && state.schema == 1 && !string.IsNullOrEmpty(state.sessionId)
            && observed > 0 && now >= observed && now - observed < 5;
    }
}
