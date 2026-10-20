using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

/// <summary>Observes the study. Recording failures never change the live trial or its existing CSVs.</summary>
[DefaultExecutionOrder(300)]
public sealed class TrialReplayRecorder : MonoBehaviour
{
    FindObjectGameManager m_Game;
    ControllerRaySelector m_Controllers;
    TrialReplayRecord m_Context;
    bool m_TrialReady;
    XROrigin m_XrOrigin;
    VoiceSynthesizer m_Voice;
    TrialReplayWriter m_Writer;
    readonly Dictionary<int, string> m_Meshes = new Dictionary<int, string>();
    readonly Dictionary<int, string> m_Clips = new Dictionary<int, string>();
    readonly Dictionary<string, TrialReplayTransform> m_Transforms = new Dictionary<string, TrialReplayTransform>();
    Vector3 m_Origin;
    Quaternion m_OriginRotation;
    double m_Started, m_NextResolve;
    long m_Sequence;
    int m_TrialNumber;
    string m_TrialId, m_Directory, m_Failure;
    bool m_Finished, m_HeaderWritten;
    string m_RecordingId;
    public string RecordingDirectory => m_Directory;

    void Start() => Resolve();

    void OnEnable()
    {
        m_Game = GetComponent<FindObjectGameManager>();
        if (m_Game == null) { enabled = false; return; }
        m_Game.OnRoundReady += ObjectsReady;
        m_Game.OnRoundTransitionStarted += Transition;
        m_Game.OnSearchStarted += SearchStarted;
        m_Game.OnCheckpoint += Checkpoint;
        m_Game.OnReadinessEvent += ReadinessEvent;
        m_Game.OnGameCompleted += Completed;
        m_Game.OnSessionStopped += Stopped;
    }

    void Resolve()
    {
        if (m_Controllers == null) m_Controllers = GetComponent<ControllerRaySelector>();
        if (m_XrOrigin == null) m_XrOrigin = FindFirstObjectByType<XROrigin>();
        var voice = GetComponent<VoiceSynthesizer>();
        if (m_Voice != voice)
        {
            if (m_Voice != null) m_Voice.Telemetry -= AudioEvent;
            m_Voice = voice;
            if (m_Voice != null) m_Voice.Telemetry += AudioEvent;
        }
    }

    // Audio can be transferred during Loading. Defer the header/origin/clock until
    // the first real transition, so preparation does not change trial coordinates.
    void EnsureStorage()
    {
        if (m_Writer != null) return;
        m_RecordingId = Guid.NewGuid().ToString("N");
        m_Directory = Path.Combine(Application.persistentDataPath, "GazeReplays", DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "_" + m_RecordingId);
        Directory.CreateDirectory(Path.Combine(m_Directory, "audio"));
        m_Writer = new TrialReplayWriter(Path.Combine(m_Directory, "recording.jsonl"));
    }

    bool EnsureStarted()
    {
        if (m_Failure != null || m_Finished) return false;
        if (m_HeaderWritten) return true;
        try
        {
            Resolve();
            m_Started = Time.realtimeSinceStartupAsDouble;
            m_Origin = m_Game.SeatedOrigin;
            m_OriginRotation = Quaternion.Euler(0, m_Game.SeatedForwardYaw, 0);
            EnsureStorage();
            var header = Record("header");
            header.recordingId = m_RecordingId; header.sourceType = Application.isEditor ? "editor_simulation" : "live_headset";
            header.utc = DateTime.UtcNow.ToString("O"); header.appVersion = Application.version;
            header.buildGuid = Application.buildGUID; header.unityVersion = Application.unityVersion;
            header.interactionPolicy = SessionConfig.InteractionPolicy; header.repeatEventVersion = SessionConfig.RepeatEventVersion;
            header.guidanceAuditVersion = SessionConfig.GuidanceAuditVersion;
            header.gazeLinkVersion = SessionConfig.GazeReplayLinkVersion;
            header.gazeRayVersion = TrialReplayGaze.Version;
            header.neutralCatalogVersion = SessionConfig.NeutralCatalogVersion;
            header.origin = m_Origin; header.originRotation = m_OriginRotation;
            header.gazeRecordingPath = SharedGazeStream.Instance != null ? SharedGazeStream.Instance.RecordingPath : "";
            header.detail = "Unity metres, +Y up, +Z forward; poses relative to fixed recording origin; software audio onset; gaze interaction proxy.";
            m_HeaderWritten = true;
            Write(header);
            Debug.Log("[TrialReplay] Recording to " + m_Directory);
            return true;
        }
        catch (Exception e) { Fail(e.Message); return false; }
    }

    // Freeze trial linkage before the manager advances or changes voice at a block boundary.
    void BeginTrial()
    {
        m_TrialId = (m_Game.IsPractice ? "practice_" : "trial_") + (++m_TrialNumber).ToString("D3");
        m_TrialReady = false;
        m_Context = new TrialReplayRecord {
            round = m_Game.CurrentObjectiveIndex, practice = m_Game.IsPractice,
            voice = SessionConfig.VoiceBlocksEnabled ? SessionConfig.VoiceLabelForRound(m_Game.CurrentObjectiveIndex) : SessionConfig.VoiceTag,
            participantCode = SessionConfig.ParticipantId,
            runNumber = m_Game.IsPractice ? 0 : SessionConfig.RunNumber,
            sourceRunFolder = m_Game.IsPractice ? "" : Path.GetFileName(SessionConfig.CurrentRunFolder),
            sourceTrialId = m_Game.IsPractice || SessionConfig.RunNumber == 0 ? "" :
                $"{SessionConfig.ParticipantId}_run{SessionConfig.RunNumber:D3}_r{m_Game.CurrentObjectiveIndex:D2}"
        };
        m_Context.block = m_Game.IsPractice ? -1 : m_Context.round / ChallengeSet.RoundsPerBlock;
    }
    TrialReplayRecord Record(string kind)
    {
        var r = new TrialReplayRecord {
            kind = kind, sequence = m_Sequence++, time = Math.Max(0, Time.realtimeSinceStartupAsDouble - m_Started),
            frame = Time.frameCount, trialId = m_TrialId,
            searchSeconds = m_Game.CurrentSearchSeconds,
            selectionMethod = "controller_ray_trigger_press_v1",
            sampling = "Unity LateUpdate once per rendered frame; not native eye sensor samples"
        };
        if (m_Context != null)
        {
            r.round = m_Context.round; r.practice = m_Context.practice; r.voice = m_Context.voice;
            r.participantCode = m_Context.participantCode; r.runNumber = m_Context.runNumber;
            r.sourceRunFolder = m_Context.sourceRunFolder; r.sourceTrialId = m_Context.sourceTrialId;
            r.block = m_Context.block;
        }
        var gate = m_Game.Readiness;
        if (gate != null)
        {
            r.readinessActive = gate.Active; r.readinessWall = gate.Wall; r.readinessProgress = gate.Progress;
            r.readinessPaused = m_Game.ResearcherPaused;
            r.readinessValidInside = gate.ValidInside; r.announcementReady = m_Game.AnnouncementReady;
            r.readinessPosition = Position(gate.Position); r.readinessRotation = Rotation(gate.Rotation);
        }
        return r;
    }
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
        m_Writer?.StopWithoutWaiting(); m_Writer = null;
    }
    Vector3 Position(Vector3 world) => Quaternion.Inverse(m_OriginRotation) * (world - m_Origin);
    Quaternion Rotation(Quaternion world) => Quaternion.Inverse(m_OriginRotation) * world;

    void ObjectsReady(int round, string color, string shape)
    {
        if (!EnsureStarted()) return;
        try
        {
            if (m_TrialId == null) BeginTrial();
            m_TrialReady = true;
            var r = Record("trial");
            r.layout = m_Game.RotationalBetaEnabled ? RotationalSearchLayout.LayoutTag : "shelves_v1";
            r.layoutSeed = m_Game.LayoutSeed;
            if (!m_Game.IsPractice && m_Game.RotationalBetaEnabled)
            {
                var planned = ChallengeSet.Rounds[round];
                r.scheduleVersion = ChallengeSet.ScheduleVersion; r.scheduleSeed = ChallengeSet.ScheduleSeed;
                r.targetIdentityPolicy = ChallengeSet.TargetIdentityPolicy;
                r.heightAssignmentPolicy = ChallengeSet.HeightAssignmentPolicy;
                r.block = planned.blockIndex; r.anglePair = planned.anglePair; r.targetPlane = planned.targetPlane;
                r.startPlane = planned.startPlane;
                r.signedTheta = planned.SignedTheta; r.absoluteTheta = planned.AbsoluteTheta;
            }
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
                    rotation = Rotation(obj.transform.rotation), scale = obj.transform.lossyScale,
                    planeId = info.planeId, planeSlot = info.planeSlot, planeAzimuth = info.planeAzimuth };
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
        if (EnsureStarted()) { BeginTrial(); var r = Record("transition"); r.detail = color + " " + shape; Write(r); }
    }
    void SearchStarted(int round) { if (m_HeaderWritten) Write(Record("search_start")); }
    void Checkpoint(string name) { if (m_HeaderWritten) Write(Record(name)); }
    void ReadinessEvent(string name) { if (m_HeaderWritten) Write(Record(name)); }
    void Completed(float elapsed) => StartCoroutine(FinishAfterSuccess());
    System.Collections.IEnumerator FinishAfterSuccess()
    {
        yield return null;
        while (GetComponent<VoiceAssistantController>()?.SuccessFeedbackBusy == true) yield return null;
        Finish(true, "completed");
    }
    void Stopped(string reason) => Finish(false, reason);

    // Called before advancing the objective or resetting controller selection. Gaze never selects.
    public void Selection(GameObject obj, bool correct)
    {
        if (m_Writer == null || m_TrialId == null) return;
        var info = obj.GetComponent<SpawnableObjectInfo>();
        if (info == null) return;
        var r = Record("selection"); r.objectId = info.objectId; r.correct = correct;
        try
        {
            Resolve(); CapturePoses(r); CaptureObjectChanges(r);
            r.selectingHand = m_Controllers != null ? m_Controllers.SelectingHand : null;
            Write(r);
            if (correct) m_TrialReady = false;
        }
        catch (Exception e) { Fail(e.Message); }
    }

    void LateUpdate()
    {
        if (m_Writer == null || !m_TrialReady) return;
        // Optional recording must never throw through the live study loop.
        try
        {
            if (m_Writer.Error != null) { Fail(m_Writer.Error); return; }
            if (Time.realtimeSinceStartupAsDouble >= m_NextResolve)
            { Resolve(); m_NextResolve = Time.realtimeSinceStartupAsDouble + 2; }
            var r = Record("sample");
            CapturePoses(r);
            CaptureObjectChanges(r);
            Write(r);
        }
        catch (Exception e) { Fail(e.Message); }
    }

    void CapturePoses(TrialReplayRecord r)
    {
        r.searchActive = m_Game.SearchActive; r.visible = m_Game.SearchActive;
        var head = Camera.main;
        r.headAvailable = head != null;
        if (head != null) { r.headPosition = Position(head.transform.position); r.headRotation = Rotation(head.transform.rotation); }
        var stream = SharedGazeStream.Instance;
        var gaze = stream != null ? stream.Latest : null;
        TrialReplayGaze.Capture(r, gaze, stream != null && stream.TryGetValid(out _), m_Origin, m_OriginRotation);
        r.xrOriginAvailable = m_XrOrigin != null;
        if (m_XrOrigin != null)
        { r.xrOriginPosition = Position(m_XrOrigin.transform.position); r.xrOriginRotation = Rotation(m_XrOrigin.transform.rotation); }
        var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (hmd.isValid && hmd.TryGetFeatureValue(CommonUsages.isTracked, out bool headTracked)) r.headTracked = headTracked ? 1 : 0;
        // Same valid ray as guidance; this is an intersection proxy, not a fixation.
        if (m_Game.SearchActive && stream != null && stream.TryHit(out var hitInfo) && m_Transforms.ContainsKey(hitInfo.objectId))
            r.hoveredId = hitInfo.objectId;
        if (m_Controllers != null && m_Controllers.TelemetryFrame == Time.frameCount)
        {
            r.leftController = Controller(m_Controllers.LeftTelemetry);
            r.rightController = Controller(m_Controllers.RightTelemetry);
        }
    }

    TrialReplayController Controller(ControllerRaySelector.TelemetrySample sample)
    {
        var info = sample.target != null ? sample.target.GetComponent<SpawnableObjectInfo>() : null;
        return new TrialReplayController {
            available = sample.available, tracked = sample.tracked,
            triggerHeld = sample.triggerHeld, triggerValue = sample.triggerValue,
            position = Position(sample.position), rotation = Rotation(sample.rotation),
            rayAvailable = sample.rayAvailable, rayOrigin = Position(sample.rayOrigin),
            rayDirection = Quaternion.Inverse(m_OriginRotation) * sample.rayDirection,
            rayEnd = Position(sample.rayEnd),
            hoveredId = info != null && m_Transforms.ContainsKey(info.objectId) ? info.objectId : null
        };
    }

    void CaptureObjectChanges(TrialReplayRecord r)
    {
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
        r.changes = changes?.ToArray();
    }

    // Called only after the final voice treatment and level matching. No network
    // acknowledgement is needed: optional replay must never hold up guidance.
    public void PrepareVoiceClip(AudioClip clip)
    {
        if (clip == null || m_Finished || m_Failure != null) return;
        try { EnsureStorage(); SaveClip(clip); }
        catch (Exception e) { Fail(e.Message); }
    }

    string SaveClip(AudioClip clip)
    {
        if (m_Clips.TryGetValue(clip.GetInstanceID(), out string relative)) return relative;
        relative = "audio/clip_" + m_Clips.Count.ToString("D4") + ".wav";
        var samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0)) throw new IOException("Cannot read replay clip.");
        if (!m_Writer.Asset(Path.Combine(m_Directory, relative), TrialReplayAudio.Encode(samples, clip.channels, clip.frequency)))
            throw new IOException(m_Writer.Error);
        m_Clips.Add(clip.GetInstanceID(), relative);
        return relative;
    }

    public void RecordEffect(AudioClip clip, string name, float volume)
    {
        if (!m_HeaderWritten || m_Writer == null || clip == null) return;
        var r = Record("audio_playback_start"); r.text = name; r.audioVolume = volume;
        try
        {
            r.clip = SaveClip(clip);
        }
        catch (Exception e) { r.detail="audio_asset_missing="+e.Message+";"; }
        r.detail += "success_effect;software_one_shot_start;duration=" + clip.length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Write(r);
    }
    void AudioEvent(string kind, string context, string clipId, string detail)
    {
        if (!m_HeaderWritten || m_Writer == null) return;
        var r = Record(kind); r.detail = context + ";" + detail;
        if (kind == "guidance_decision")
        {
            r.guidanceDecision = JsonUtility.FromJson<GuidanceDecision>(detail);
            r.guidanceDecisionId = r.guidanceDecision?.decisionId;
        }
        else
        {
            // The ID belongs to the original request, not the gaze at playback.
            const string key = "decision_id=";
            foreach (var field in (detail ?? "").Split(';'))
                if (field.StartsWith(key, StringComparison.Ordinal))
                { r.guidanceDecisionId = field.Substring(key.Length); break; }
        }
        int textAt = detail == null ? -1 : detail.IndexOf("text=", StringComparison.Ordinal);
        if (textAt >= 0) r.text = detail.Substring(textAt + 5);
        if (kind == "audio_playback_start" && m_Voice.ReplayClip != null)
        {
            try
            {
                r.clip = SaveClip(m_Voice.ReplayClip);
            }
            catch (Exception e) { r.detail += ";audio_asset_missing=" + e.Message; }
        }
        Write(r);
    }

    void Finish(bool complete, string reason)
    {
        if (m_Writer == null) return;
        if (m_HeaderWritten)
        { var r = Record("end"); r.complete = complete; r.detail = reason; Write(r); }
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
            m_Game.OnReadinessEvent -= ReadinessEvent;
            m_Game.OnGameCompleted -= Completed; m_Game.OnSessionStopped -= Stopped;
        }
        if (m_Voice != null) m_Voice.Telemetry -= AudioEvent;
    }
    void OnApplicationQuit() => Finish(false, "application_quit");
    void OnApplicationPause(bool paused)
    {
        if (!paused || m_Writer == null) return;
        if (m_HeaderWritten) Write(Record("application_paused"));
        if (m_Writer != null && !m_Writer.RequestFlush())
            Fail(m_Writer.Error ?? "Replay writer could not accept the pause flush.");
    }
    void OnGUI()
    {
        if (m_Failure != null) GUI.Label(new Rect(10, 10, 900, 40), "Replay recording stopped: " + m_Failure);
    }
}
