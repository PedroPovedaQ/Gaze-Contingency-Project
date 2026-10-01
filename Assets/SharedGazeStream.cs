using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VIVE.OpenXR.EyeTracker;

/// <summary>
/// One source for measurement and guidance. Input delivery times establish software freshness,
/// not sensor capture time. Native HTC poses stay in their original OpenXR app-space coordinates.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class SharedGazeStream : MonoBehaviour
{
    public static SharedGazeStream Instance { get; private set; }
    [SerializeField] float m_MaxDeliveryAgeSeconds = .1f; // Pilot setting; log and validate on hardware.
    [SerializeField] bool m_MirrorLocalOriginX; // Researcher diagnostic only; default OFF.
    public GazeObservation Latest { get; private set; }
    public string RecordingPath { get; private set; }
    public string RecordingError { get; private set; }
    public string ValidationId { get; private set; }
    public string ValidationSet { get; private set; }
    public string ValidationTargetId { get; private set; }
    public Transform ValidationTarget { get; private set; }
    public bool MirrorLocalOriginX { get => m_MirrorLocalOriginX; set => m_MirrorLocalOriginX = value; }
    public event Action<GazeObservation> Observed;
    GazeObservation m_LastNative;
    EyeGazeInteraction.EyeGazeDevice m_Device;
    XRGazeInteractor m_Interactor;
    FindObjectGameManager m_Game;
    TrialReplayWriter m_Writer;
    string m_RecordingId;
    long m_Sequence, m_AcceptedRows;
    double m_LastInput = -1, m_NextResolve;
    int m_LastDevice = -1;
    bool m_Focused = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Attach()
    {
        if (FindFirstObjectByType<XRGazeInteractor>() != null && Instance == null)
            new GameObject("Shared gaze measurement").AddComponent<SharedGazeStream>();
    }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.AddComponent<GazeDiagnosticView>();
    }
    void OnEnable() => InputSystem.onAfterUpdate += AfterInput;
    void OnDisable()
    {
        InputSystem.onAfterUpdate -= AfterInput;
        if (m_Writer == null) return;
        m_Writer.Dispose();
        RecordingError = RecordingError ?? m_Writer.Error;
        try
        {
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(RecordingPath),"end_status.json"),
                JsonUtility.ToJson(new EndStatus { complete=RecordingError==null, acceptedRows=m_AcceptedRows,
                    error=RecordingError ?? "", stoppedAt=Time.realtimeSinceStartupAsDouble },true));
        }
        catch (Exception e) { RecordingError=e.Message;Debug.LogError("[SharedGaze] " + e.Message); }
        m_Writer=null;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnApplicationFocus(bool focus) { m_Focused = focus; if (!focus) Capture(); }
    void OnApplicationPause(bool paused)
    {
        m_Focused = !paused; Capture();
        if (paused && m_Writer != null && RecordingError == null && !m_Writer.RequestFlush())
        {
            RecordingError = m_Writer.Error ?? "Eye writer could not accept the pause flush.";
            Debug.LogError("[SharedGaze] " + RecordingError);
            m_Writer.StopWithoutWaiting();
        }
    }
    void Resolve()
    {
        if (m_Interactor == null) m_Interactor = FindFirstObjectByType<XRGazeInteractor>();
        if (m_Game == null) m_Game = FindFirstObjectByType<FindObjectGameManager>();
        m_Device = null;
        foreach (var device in InputSystem.devices)
            if (device is EyeGazeInteraction.EyeGazeDevice eye) { m_Device = eye; break; }
    }
    void AfterInput()
    {
        if (m_Device != null && m_Device.added && m_Device.lastUpdateTime != m_LastInput) Capture();
    }
    void Update()
    {
        if (Time.realtimeSinceStartupAsDouble >= m_NextResolve)
        { Resolve(); m_NextResolve = Time.realtimeSinceStartupAsDouble + 1; }
        // Heartbeats preserve invalid/stale gaps even when input stops delivering events.
        Capture();
    }
    public bool TryGetValid(out GazeObservation sample)
    {
        sample = Latest;
        return isActiveAndEnabled && sample != null && sample.valid && m_Focused &&
            GazeQualityPolicy.IsFresh(true, sample.inputEventTime, Time.realtimeSinceStartupAsDouble, m_MaxDeliveryAgeSeconds);
    }
    public bool TryHit(out SpawnableObjectInfo info)
    {
        info = null;
        if (!TryGetValid(out var sample)) return false;
        if (Physics.Raycast(sample.worldOrigin, sample.worldDirection, out var hit, 30, 1 << 8, QueryTriggerInteraction.Ignore))
            info = hit.collider.GetComponentInParent<SpawnableObjectInfo>();
        return info != null;
    }
    public void SetValidationTarget(Transform target, string id, string set, string targetId)
    { ValidationTarget = target; ValidationId = id; ValidationSet = set; ValidationTargetId = targetId; }

    void Capture()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        var s = new GazeObservation { sequence = m_Sequence++, frame = Time.frameCount, receiptTime = now,
            focused = m_Focused && Application.isFocused, participant = SessionConfig.ParticipantId,
            run = SessionConfig.RunNumber, voice = SessionConfig.VoiceTag,
            recordingId = m_RecordingId, maxDeliveryAgeSeconds=m_MaxDeliveryAgeSeconds, validationId = ValidationId, validationSet = ValidationSet,
            targetId = ValidationTargetId, correction = m_MirrorLocalOriginX ? "mirror_local_origin_x_diagnostic" : "none" };
        if (m_Game != null)
        {
            FillTrialContext(s, m_Game);
        }
        if (m_Device != null && m_Device.added)
        {
            s.deviceAvailable = true; s.deviceId = m_Device.deviceId;
            s.inputEventTime = m_Device.lastUpdateTime;
            s.newInputUpdate = s.inputEventTime != m_LastInput || s.deviceId != m_LastDevice;
            m_LastInput = s.inputEventTime; m_LastDevice = s.deviceId;
            s.ageSeconds = Math.Max(0, now - s.inputEventTime);
            var pose = m_Device.pose.ReadValue();
            s.tracked = pose.isTracked;
            // Non-finite values are unavailable, not numeric JSON measurements.
            // Retain the invalid row and flag while emitting finite placeholders.
            s.rawLocalOrigin = Finite(pose.position) ? pose.position : Vector3.zero;
            s.rawLocalRotation = Finite(pose.rotation) ? pose.rotation : Quaternion.identity;
            s.poseValid = (pose.trackingState & (InputTrackingState.Position | InputTrackingState.Rotation)) ==
                (InputTrackingState.Position | InputTrackingState.Rotation) && Finite(pose.position) &&
                Finite(pose.rotation) && Quaternion.Dot(pose.rotation, pose.rotation) > .5f;
            // EXT EyeGaze input is already converted by Unity. Do not apply HTC's native conversion again.
            var parent = m_Interactor != null ? m_Interactor.transform.parent : null;
            if (parent != null && s.poseValid)
            {
                s.trackingOriginPosition = parent.position; s.trackingOriginRotation = parent.rotation;
                Ray raw=GazeCoordinates.InputPoseToWorld(pose.position,pose.rotation,parent,false);
                Ray used=GazeCoordinates.InputPoseToWorld(pose.position,pose.rotation,parent,m_MirrorLocalOriginX);
                s.rawWorldOrigin=raw.origin; s.rawWorldDirection=raw.direction;
                s.worldOrigin=used.origin; s.worldDirection=used.direction;
            }
            s.valid = parent != null && s.focused && s.poseValid &&
                GazeQualityPolicy.IsFresh(s.tracked, s.inputEventTime, now, m_MaxDeliveryAgeSeconds);
            s.invalidReason = s.valid ? "" : !s.focused ? "unfocused" : parent == null ? "no_tracking_parent" :
                !s.tracked ? "untracked" : !s.poseValid ? "invalid_pose" : "stale_or_unknown_delivery_time";
        }
        else s.invalidReason = "no_eye_device";
        var head = Camera.main;
        var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        s.headValid = head != null && hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked;
        if (head != null) { s.headPosition = head.transform.position; s.headRotation = head.transform.rotation; }
        if (s.newInputUpdate) { CaptureHtc(s); m_LastNative=s; }
        else if (m_LastNative != null)
        {
            s.htcQueryReceiptTime=m_LastNative.htcQueryReceiptTime;
            s.htcGazeAvailable=m_LastNative.htcGazeAvailable;s.leftValid=m_LastNative.leftValid;s.rightValid=m_LastNative.rightValid;
            s.leftNativeOrigin=m_LastNative.leftNativeOrigin;s.rightNativeOrigin=m_LastNative.rightNativeOrigin;
            s.leftNativeRotation=m_LastNative.leftNativeRotation;s.rightNativeRotation=m_LastNative.rightNativeRotation;
            s.leftWorldOrigin=m_LastNative.leftWorldOrigin;s.rightWorldOrigin=m_LastNative.rightWorldOrigin;
            s.leftWorldDirection=m_LastNative.leftWorldDirection;s.rightWorldDirection=m_LastNative.rightWorldDirection;
            s.pupilAvailable=m_LastNative.pupilAvailable;s.leftPupilValid=m_LastNative.leftPupilValid;s.rightPupilValid=m_LastNative.rightPupilValid;
            s.leftPupilMm=m_LastNative.leftPupilMm;s.rightPupilMm=m_LastNative.rightPupilMm;
            s.opennessAvailable=m_LastNative.opennessAvailable;s.leftOpennessValid=m_LastNative.leftOpennessValid;s.rightOpennessValid=m_LastNative.rightOpennessValid;
            s.leftOpenness=m_LastNative.leftOpenness;s.rightOpenness=m_LastNative.rightOpenness;
        }
        if(s.htcGazeAvailable && !s.leftValid && !s.rightValid)
        { s.valid=false; s.invalidReason="both_native_eyes_invalid"; }
        if (ValidationTarget != null)
        {
            s.validationTargetPresent = true; s.validationTarget = ValidationTarget.position;
            s.targetErrorDegrees = Vector3.Angle(s.rawWorldDirection, s.validationTarget - s.rawWorldOrigin);
            if (m_Interactor != null && m_Interactor.transform.parent != null)
            {
                var mirrored = s.rawLocalOrigin; mirrored.x = -mirrored.x;
                s.mirroredOriginErrorDegrees = Vector3.Angle(s.rawWorldDirection,
                    s.validationTarget - m_Interactor.transform.parent.TransformPoint(mirrored));
            }
        }
        Latest = s;
        Record(s); Observed?.Invoke(s);
    }

    // Keep eye/replay linkage on the completed trial during success speech. The
    // manager's next-objective counter can already point at another block or 80.
    public static void FillTrialContext(GazeObservation sample, FindObjectGameManager game)
    {
        sample.round = game.PresentedRoundIndex;
        sample.practice = game.PresentedRoundIsPractice;
        if (sample.round >= 0) sample.voice = game.PresentedVoiceTag;
        sample.block = sample.practice || sample.round < 0 ? -1 : sample.round / ChallengeSet.RoundsPerBlock;
        sample.phase = game.SearchActive ? "search" : game.ResearcherPaused ? "paused" :
            game.CurrentState == FindObjectGameManager.GameState.Completed ? "post_run" : "setup_or_transition";
        sample.sourceTrialId = sample.run > 0 && !sample.practice && sample.round >= 0
            ? $"{sample.participant}_run{sample.run:D3}_r{sample.round:D2}" : "";
    }
    void CaptureHtc(GazeObservation s)
    {
        var feature = OpenXRSettings.Instance != null ? OpenXRSettings.Instance.GetFeature<ViveEyeTracker>() : null;
        if (feature == null || !feature.enabled) return;
        s.htcQueryReceiptTime = Time.realtimeSinceStartupAsDouble;
        s.htcGazeAvailable = feature.GetEyeGazeData(out var eyes) && eyes != null && eyes.Length >= 2;
        if (s.htcGazeAvailable)
        {
            s.leftValid = eyes[0].isValid; s.rightValid = eyes[1].isValid;
            s.leftNativeOrigin = NativePosition(eyes[0].gazePose.position);
            s.rightNativeOrigin = NativePosition(eyes[1].gazePose.position);
            s.leftNativeRotation = NativeRotation(eyes[0].gazePose.orientation);
            s.rightNativeRotation = NativeRotation(eyes[1].gazePose.orientation);
            s.leftValid &= Finite(s.leftNativeOrigin) && Finite(s.leftNativeRotation) && Quaternion.Dot(s.leftNativeRotation, s.leftNativeRotation) > .5f;
            s.rightValid &= Finite(s.rightNativeOrigin) && Finite(s.rightNativeRotation) && Quaternion.Dot(s.rightNativeRotation, s.rightNativeRotation) > .5f;
            if (!Finite(s.leftNativeOrigin)) s.leftNativeOrigin = Vector3.zero;
            if (!Finite(s.rightNativeOrigin)) s.rightNativeOrigin = Vector3.zero;
            if (!Finite(s.leftNativeRotation)) s.leftNativeRotation = Quaternion.identity;
            if (!Finite(s.rightNativeRotation)) s.rightNativeRotation = Quaternion.identity;
            if (m_Interactor != null && m_Interactor.transform.parent != null)
            {
                Ray left=GazeCoordinates.NativePoseToWorld(eyes[0].gazePose,m_Interactor.transform.parent);
                Ray right=GazeCoordinates.NativePoseToWorld(eyes[1].gazePose,m_Interactor.transform.parent);
                if (s.leftValid) { s.leftWorldOrigin=left.origin; s.leftWorldDirection=left.direction; }
                if (s.rightValid) { s.rightWorldOrigin=right.origin; s.rightWorldDirection=right.direction; }
            }
        }
        s.pupilAvailable = feature.GetEyePupilData(out var pupil) && pupil != null && pupil.Length >= 2;
        if (s.pupilAvailable)
        {
            s.leftPupilValid = pupil[0].isDiameterValid && Finite(pupil[0].pupilDiameter) && pupil[0].pupilDiameter > 0;
            s.rightPupilValid = pupil[1].isDiameterValid && Finite(pupil[1].pupilDiameter) && pupil[1].pupilDiameter > 0;
            s.leftPupilMm = s.leftPupilValid ? pupil[0].pupilDiameter : 0;
            s.rightPupilMm = s.rightPupilValid ? pupil[1].pupilDiameter : 0;
        }
        s.opennessAvailable = feature.GetEyeGeometricData(out var geometry) && geometry != null && geometry.Length >= 2;
        if (s.opennessAvailable)
        {
            s.leftOpennessValid = geometry[0].isValid && Finite(geometry[0].eyeOpenness) && geometry[0].eyeOpenness >= 0 && geometry[0].eyeOpenness <= 1;
            s.rightOpennessValid = geometry[1].isValid && Finite(geometry[1].eyeOpenness) && geometry[1].eyeOpenness >= 0 && geometry[1].eyeOpenness <= 1;
            s.leftOpenness = s.leftOpennessValid ? geometry[0].eyeOpenness : 0;
            s.rightOpenness = s.rightOpennessValid ? geometry[1].eyeOpenness : 0;
        }
    }
    static Vector3 NativePosition(VIVE.OpenXR.XrVector3f v) => new Vector3(v.x,v.y,v.z);
    static Quaternion NativeRotation(VIVE.OpenXR.XrQuaternionf q) => new Quaternion(q.x,q.y,q.z,q.w);
    static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w);
    void Record(GazeObservation sample)
    {
        if (RecordingError != null) return;
        try
        {
            if (m_Writer == null)
            {
                m_AcceptedRows = 0;
                m_RecordingId = Guid.NewGuid().ToString("N");
                var directory = Path.Combine(Application.persistentDataPath,"EyeMeasurements", DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+m_RecordingId);
                Directory.CreateDirectory(directory); RecordingPath = Path.Combine(directory,"gaze_observations.jsonl");
                File.WriteAllText(Path.Combine(directory,"capabilities.json"), JsonUtility.ToJson(new Metadata(), true));
                m_Writer = new TrialReplayWriter(RecordingPath);
            }
            sample.recordingId = m_RecordingId;
            if (!m_Writer.Append(sample)) throw new IOException(m_Writer.Error ?? "Eye writer stopped");
            m_AcceptedRows++;
        }
        catch (Exception e) { RecordingError = e.Message; Debug.LogError("[SharedGaze] " + e.Message); m_Writer?.StopWithoutWaiting(); }
    }
    [Serializable] class EndStatus
    { public bool complete; public long acceptedRows; public double stoppedAt; public string error; }
    [Serializable] class Metadata
    {
        public string build = Application.buildGUID, unity = Application.unityVersion, device = SystemInfo.deviceModel,
            os = SystemInfo.operatingSystem, source = "OpenXR EXT input state; optional HTC per-eye polls",
            timing = "receiptTime and inputEventTime: Unity realtime seconds; source capture time/sequence unavailable",
            nativeCoordinates = "HTC raw right-handed OpenXR app space; EXT pose already Unity converted",
            rateConclusion = "120 Hz sensor acquisition NOT ESTABLISHED; repeated polls are not distinct source observations";
    }
}
