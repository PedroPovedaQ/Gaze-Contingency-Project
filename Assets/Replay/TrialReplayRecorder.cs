using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

/// <summary>Observes the study. Recording failures never change the live trial or its existing CSVs.</summary>
[DefaultExecutionOrder(300)]
public sealed class TrialReplayRecorder : MonoBehaviour
{
    FindObjectGameManager m_Game;
    GazeHighlightManager m_Dwell;
    XRGazeInteractor m_Gaze;
    XROrigin m_XrOrigin;
    VoiceSynthesizer m_Voice;
    TrialReplayWriter m_Writer;
    readonly Dictionary<int, string> m_Meshes = new Dictionary<int, string>();
    readonly Dictionary<int, string> m_Clips = new Dictionary<int, string>();
    readonly Dictionary<string, TrialReplayTransform> m_Transforms = new Dictionary<string, TrialReplayTransform>();
    readonly List<InputDevice> m_Eyes = new List<InputDevice>();
    Vector3 m_Origin;
    Quaternion m_OriginRotation;
    double m_Started, m_NextDeviceCheck, m_NextResolve;
    long m_Sequence;
    int m_TrialNumber;
    string m_TrialId, m_Directory, m_Failure;
    bool m_Finished;
    public string RecordingDirectory => m_Directory;

    void OnEnable()
    {
        m_Game = GetComponent<FindObjectGameManager>();
        if (m_Game == null) { enabled = false; return; }
        m_Game.OnRoundReady += ObjectsReady;
        m_Game.OnRoundTransitionStarted += Transition;
        m_Game.OnSearchStarted += SearchStarted;
        m_Game.OnCheckpoint += Checkpoint;
        m_Game.OnGameCompleted += Completed;
        m_Game.OnSessionStopped += Stopped;
    }

    void Resolve()
    {
        if (m_Dwell == null) m_Dwell = FindFirstObjectByType<GazeHighlightManager>();
        if (m_Gaze == null) m_Gaze = FindFirstObjectByType<XRGazeInteractor>();
        if (m_XrOrigin == null) m_XrOrigin = FindFirstObjectByType<XROrigin>();
        var voice = GetComponent<VoiceSynthesizer>();
        if (m_Voice != voice)
        {
            if (m_Voice != null) m_Voice.Telemetry -= AudioEvent;
            m_Voice = voice;
            if (m_Voice != null) m_Voice.Telemetry += AudioEvent;
        }
    }

    bool EnsureStarted()
    {
        if (m_Failure != null || m_Finished) return false;
        if (m_Writer != null) return true;
        try
        {
            Resolve();
            m_Started = Time.realtimeSinceStartupAsDouble;
            m_Origin = m_Game.SeatedOrigin;
            m_OriginRotation = Quaternion.Euler(0, m_Game.SeatedForwardYaw, 0);
            string id = Guid.NewGuid().ToString("N");
            m_Directory = Path.Combine(Application.persistentDataPath, "GazeReplays", DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "_" + id);
            Directory.CreateDirectory(Path.Combine(m_Directory, "audio"));
            m_Writer = new TrialReplayWriter(Path.Combine(m_Directory, "recording.jsonl"));
            var header = Record("header");
            header.recordingId = id; header.sourceType = Application.isEditor ? "editor_simulation" : "live_headset";
            header.utc = DateTime.UtcNow.ToString("O"); header.appVersion = Application.version;
            header.buildGuid = Application.buildGUID; header.unityVersion = Application.unityVersion;
            header.origin = m_Origin; header.originRotation = m_OriginRotation;
            header.detail = "Unity metres, +Y up, +Z forward; poses relative to fixed recording origin; software audio onset; gaze interaction proxy.";
            Write(header);
            Debug.Log("[TrialReplay] Recording to " + m_Directory);
            return true;
        }
        catch (Exception e) { Fail(e.Message); return false; }
    }

    TrialReplayRecord Record(string kind) => new TrialReplayRecord
    {
        kind = kind, sequence = m_Sequence++, time = Math.Max(0, Time.realtimeSinceStartupAsDouble - m_Started),
        frame = Time.frameCount, trialId = m_TrialId, round = m_Game.CurrentObjectiveIndex,
        practice = m_Game.IsPractice, voice = SessionConfig.VoiceTag, searchSeconds = m_Game.CurrentSearchSeconds,
        participantCode = SessionConfig.ParticipantId,
        runNumber = m_Game.IsPractice ? 0 : SessionConfig.RunNumber,
        sourceRunFolder = m_Game.IsPractice ? "" : Path.GetFileName(SessionConfig.CurrentRunFolder),
        sourceTrialId = m_Game.IsPractice || SessionConfig.RunNumber == 0 ? "" :
            $"{SessionConfig.ParticipantId}_run{SessionConfig.RunNumber:D3}_r{m_Game.CurrentObjectiveIndex:D2}"
    };
    void Write(TrialReplayRecord record)
    {
        if (m_Writer == null || m_Failure != null) return;
        try { if (!m_Writer.Append(record)) Fail(m_Writer.Error ?? "Recording closed unexpectedly."); }
        catch (Exception e) { Fail(e.Message); }
    }
    void Fail(string message)
    {
        m_Failure = message;
        Debug.LogError("[TrialReplay] Recording incomplete: " + message);
        m_Writer?.Dispose(); m_Writer = null;
    }
    Vector3 Position(Vector3 world) => Quaternion.Inverse(m_OriginRotation) * (world - m_Origin);
    Quaternion Rotation(Quaternion world) => Quaternion.Inverse(m_OriginRotation) * world;

    void ObjectsReady(int round, string color, string shape)
    {
        if (!EnsureStarted()) return;
        try
        {
            m_TrialId = (m_Game.IsPractice ? "practice_" : "trial_") + (++m_TrialNumber).ToString("D3");
            var r = Record("trial");
            r.layout = m_Game.RotationalBetaEnabled ? "rotational_beta_v1" : "shelves_v1";
            var objects = new List<TrialReplayObject>();
            var meshes = new List<TrialReplayMesh>();
            m_Transforms.Clear();
            foreach (var obj in m_Game.SpawnedObjects)
            {
                var info = obj.GetComponent<SpawnableObjectInfo>();
                var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
                if (!m_Meshes.TryGetValue(mesh.GetInstanceID(), out string meshId))
                {
                    meshId = "mesh_" + m_Meshes.Count;
                    m_Meshes.Add(mesh.GetInstanceID(), meshId);
                    meshes.Add(new TrialReplayMesh { id = meshId, vertices = mesh.vertices, normals = mesh.normals, triangles = mesh.triangles });
                }
                var mat = obj.GetComponent<Renderer>().sharedMaterial;
                Color rgba = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                bool target = info.shapeName == shape && info.colorName == color;
                var o = new TrialReplayObject { id = info.objectId, shape = info.shapeName, color = info.colorName,
                    mesh = meshId, rgba = rgba, target = target, position = Position(obj.transform.position),
                    rotation = Rotation(obj.transform.rotation), scale = obj.transform.lossyScale };
                objects.Add(o);
                m_Transforms.Add(o.id, new TrialReplayTransform { id = o.id, position = o.position, rotation = o.rotation, scale = o.scale });
                if (target) r.targetId = o.id;
            }
            r.objects = objects.ToArray(); r.meshes = meshes.ToArray(); Write(r);
        }
        catch (Exception e) { Fail(e.Message); }
    }
    void Transition(int round, string color, string shape)
    {
        if (EnsureStarted()) { var r = Record("transition"); r.detail = color + " " + shape; Write(r); }
    }
    void SearchStarted(int round) { if (m_Writer != null) Write(Record("search_start")); }
    void Checkpoint(string name) { if (m_Writer != null) Write(Record(name)); }
    void Completed(float elapsed) => Finish(true, "completed");
    void Stopped(string reason) => Finish(false, reason);

    // Called by the game manager BEFORE advancing the objective or resetting dwell.
    public void Selection(GameObject obj, bool correct)
    {
        if (m_Writer == null || m_TrialId == null) return;
        var info = obj.GetComponent<SpawnableObjectInfo>();
        if (info == null) return;
        var r = Record("selection"); r.objectId = info.objectId; r.correct = correct;
        r.dwell = 1; Write(r);
    }

    void LateUpdate()
    {
        if (m_Writer == null || m_TrialId == null) return;
        if (m_Writer.Error != null) { Fail(m_Writer.Error); return; }
        if (Time.realtimeSinceStartupAsDouble >= m_NextResolve)
        { Resolve(); m_NextResolve = Time.realtimeSinceStartupAsDouble + 2; }
        var r = Record("sample");
        r.searchActive = m_Game.SearchActive; r.visible = m_Game.SearchActive;
        var head = Camera.main;
        r.headAvailable = head != null;
        if (head != null) { r.headPosition = Position(head.transform.position); r.headRotation = Rotation(head.transform.rotation); }
        r.gazeAvailable = m_Gaze != null && m_Gaze.isActiveAndEnabled;
        if (r.gazeAvailable)
        {
            r.gazeOrigin = Position(m_Gaze.transform.position);
            r.gazeDirection = Quaternion.Inverse(m_OriginRotation) * m_Gaze.transform.forward;
        }
        r.xrOriginAvailable = m_XrOrigin != null;
        if (m_XrOrigin != null)
        { r.xrOriginPosition = Position(m_XrOrigin.transform.position); r.xrOriginRotation = Rotation(m_XrOrigin.transform.rotation); }
        var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (hmd.isValid && hmd.TryGetFeatureValue(CommonUsages.isTracked, out bool headTracked)) r.headTracked = headTracked ? 1 : 0;
        if (Time.realtimeSinceStartupAsDouble >= m_NextDeviceCheck)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, m_Eyes);
            m_NextDeviceCheck = Time.realtimeSinceStartupAsDouble + 2;
        }
        foreach (var eye in m_Eyes)
            if (eye.isValid && eye.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked)) { r.gazeTracked = tracked ? 1 : 0; break; }
        var hovered = m_Dwell != null && m_Dwell.DwellTarget != null ? m_Dwell.DwellTarget.GetComponent<SpawnableObjectInfo>() : null;
        if (hovered != null && m_Transforms.ContainsKey(hovered.objectId)) r.hoveredId = hovered.objectId;
        r.dwell = m_Dwell != null ? m_Dwell.DwellProgress : 0;
        List<TrialReplayTransform> changes = null;
        foreach (var obj in m_Game.SpawnedObjects)
        {
            if (obj == null) continue;
            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info == null || !m_Transforms.TryGetValue(info.objectId, out var old)) continue;
            var p = Position(obj.transform.position); var q = Rotation(obj.transform.rotation); var s = obj.transform.lossyScale;
            if (old.position.Equals(p) && old.rotation.Equals(q) && old.scale.Equals(s)) continue;
            var t = new TrialReplayTransform { id = info.objectId, position = p, rotation = q, scale = s };
            if (changes == null) changes = new List<TrialReplayTransform>();
            changes.Add(t); m_Transforms[info.objectId] = t;
        }
        r.changes = changes?.ToArray(); Write(r);
    }

    void AudioEvent(string kind, string context, string clipId, string detail)
    {
        if (m_Writer == null) return;
        var r = Record(kind); r.detail = context + ";" + detail;
        int textAt = detail == null ? -1 : detail.IndexOf("text=", StringComparison.Ordinal);
        if (textAt >= 0) r.text = detail.Substring(textAt + 5);
        if (kind == "audio_playback_start" && m_Voice.ReplayClip != null)
        {
            try
            {
                AudioClip clip = m_Voice.ReplayClip;
                if (!m_Clips.TryGetValue(clip.GetInstanceID(), out string relative))
                {
                    relative = "audio/clip_" + m_Clips.Count.ToString("D4") + ".wav";
                    var samples = new float[clip.samples * clip.channels];
                    if (!clip.GetData(samples, 0)) throw new IOException("Cannot read playback clip.");
                    byte[] bytes = TrialReplayAudio.Encode(samples, clip.channels, clip.frequency);
                    if (!m_Writer.Asset(Path.Combine(m_Directory, relative), bytes)) throw new IOException(m_Writer.Error);
                    m_Clips.Add(clip.GetInstanceID(), relative);
                }
                r.clip = relative;
            }
            catch (Exception e) { r.detail += ";audio_asset_missing=" + e.Message; }
        }
        Write(r);
    }

    void Finish(bool complete, string reason)
    {
        if (m_Writer == null) return;
        var r = Record("end"); r.complete = complete; r.detail = reason; Write(r);
        m_Finished = true;
        m_Writer?.Dispose();
        if (m_Writer?.Error != null) m_Failure = m_Writer.Error;
        m_Writer = null;
    }
    void OnDisable()
    {
        Finish(false, "recorder_disabled");
        if (m_Game != null)
        {
            m_Game.OnRoundReady -= ObjectsReady; m_Game.OnRoundTransitionStarted -= Transition;
            m_Game.OnSearchStarted -= SearchStarted; m_Game.OnCheckpoint -= Checkpoint;
            m_Game.OnGameCompleted -= Completed; m_Game.OnSessionStopped -= Stopped;
        }
        if (m_Voice != null) m_Voice.Telemetry -= AudioEvent;
    }
    void OnApplicationQuit() => Finish(false, "application_quit");
    void OnApplicationPause(bool paused) { if (paused && m_Writer != null) Write(Record("application_paused")); }
    void OnGUI()
    {
        if (m_Failure != null) GUI.Label(new Rect(10, 10, 900, 40), "Replay recording stopped: " + m_Failure);
    }
}
