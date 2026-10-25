using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Diagnostic side stream. Native eye-pose times never replace SharedGazeStream observations,
/// set sourceCaptureTimeKnown, drive calibration, or grant guidance validity.
/// </summary>
[DefaultExecutionOrder(-90)]
public sealed class NativeGazeRecorder : MonoBehaviour
{
    public static NativeGazeRecorder Instance { get; private set; }
    public string Status { get; private set; } = "Waiting for native eye-pose diagnostics.";
    public string RecordingError { get; private set; }
    public string RecordingPath { get; private set; }
    readonly NativeGazeRecord[] m_Buffer = new NativeGazeRecord[1024];
    TrialReplayWriter m_Writer;
    SharedGazeStream m_Shared;
    double m_NextStatus;
    ulong m_AcceptedRecords;
    NativeGazeStatus m_LastStatus;
    bool m_Focused = true, m_Stopped, m_EpochChanged;
    ulong m_FirstEpoch, m_LastSequence;
    int m_LastDrainCount;

    [Serializable] sealed class Metadata
    {
        public int schema = 1;
        public string kind = "native_gaze_capabilities", build = Application.buildGUID,
            runtime = UnityEngine.XR.OpenXR.OpenXRRuntime.name,
            runtimeVersion = UnityEngine.XR.OpenXR.OpenXRRuntime.version,
            timestampMeaning = "XrEyeGazeSampleTimeEXT: clamped, predicted or interpolated pose-expression time; zero means unavailable",
            sourceIdentification = "Action space with a suggested EXT gaze binding; actual bound sensor source not independently established",
            clocks = "requestedTime/runtimePoseTime: XrTime nanoseconds; receiptMonotonicNs: native steady clock; Unity drain clock bracket is not capture time",
            coordinates = "Right-handed native OpenXR pose in recorded baseSpace, including recorded action-space offset; not Unity world space",
            rateConclusion = "120 Hz sensor acquisition NOT ESTABLISHED. Distinct reported pose times can be predicted/interpolated.",
            acquisition = "Passive interception of existing xrLocateSpace calls; does not increase sensor acquisition cadence";
        public bool sensorCaptureSemanticsVerified = false;
    }
    [Serializable] sealed class Batch
    {
        public int schema = 1, unityFrameAtDrain;
        public string kind = "native_gaze_batch", featureStatus;
        public bool focusedAtDrain, featureAvailable;
        public double unityBeforeClockRead, unityAfterClockRead;
        public long nativeClockReadNs;
        public NativeGazeStatus status;
        public NativeGazeRecord[] records;
    }
    [Serializable] sealed class EndStatus
    {
        public int schema = 1;
        public bool complete;
        public string completenessScope = "Writer drained and observed native sequence covered through shutdown snapshot; not sensor coverage";
        public ulong acceptedRecords;
        public string error;
        public double stoppedAt;
        public NativeGazeStatus nativeStatus;
    }
    void Awake() { Instance = this; m_Shared = GetComponent<SharedGazeStream>(); }
    void Update() { Drain(false); }
    void Drain(bool forceStatus)
    {
        if (m_Stopped || RecordingError != null || m_Shared == null || string.IsNullOrEmpty(m_Shared.RecordingPath)) return;
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(m_Shared.RecordingPath), "native_gaze_observations.jsonl");
            if (m_Writer == null)
            {
                RecordingPath = path;
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"native_gaze_capabilities.json"), JsonUtility.ToJson(new Metadata(),true));
                m_Writer = new TrialReplayWriter(path); m_AcceptedRecords = 0;
            }
            if (m_Writer.Error != null) throw new IOException(m_Writer.Error);
            var batch = new Batch { featureStatus = NativeGazeTimestampFeature.Status,
                featureAvailable = NativeGazeTimestampFeature.Available, unityFrameAtDrain = Time.frameCount,
                focusedAtDrain = m_Focused && Application.isFocused, unityBeforeClockRead = Time.realtimeSinceStartupAsDouble };
            int count = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (NativeGazeTimestampFeature.NativeLoaded)
            {
                NativeGazeInterop.ng_status(out batch.status);
                if (batch.status.recordBytes != Marshal.SizeOf<NativeGazeRecord>()) throw new InvalidOperationException("Native gaze ABI size mismatch.");
                batch.nativeClockReadNs = NativeGazeInterop.ng_monotonic_ns();
                count = NativeGazeInterop.ng_drain(m_Buffer,m_Buffer.Length);
            }
#endif
            batch.unityAfterClockRead = Time.realtimeSinceStartupAsDouble;
            m_LastStatus = batch.status;
            m_LastDrainCount = count;
            if (batch.status.epoch != 0)
            {
                if (m_FirstEpoch == 0) m_FirstEpoch = batch.status.epoch;
                m_EpochChanged |= batch.status.epoch != m_FirstEpoch;
            }
            if (count > 0) m_LastSequence = m_Buffer[count - 1].sequence;
            Status = !batch.featureAvailable ? batch.featureStatus :
                $"Runtime pose queries: {batch.status.records}; drops: {batch.status.dropped}. Sensor 120 Hz unverified.";
            if (count == 0 && !forceStatus && batch.unityAfterClockRead < m_NextStatus) return;
            batch.records = new NativeGazeRecord[count]; Array.Copy(m_Buffer,batch.records,count);
            if (!m_Writer.AppendJson(JsonUtility.ToJson(batch))) throw new IOException(m_Writer.Error ?? "Native eye writer stopped.");
            m_AcceptedRecords += (ulong)count; m_NextStatus = batch.unityAfterClockRead + 1;
        }
        catch(Exception e) { RecordingError=e.Message;Status="Native timestamp recording error: "+e.Message; m_Writer?.StopWithoutWaiting();Debug.LogError("[NativeGaze] "+Status); }
    }
    void OnApplicationPause(bool paused)
    {
        m_Focused = !paused; Drain(true);
        if (paused && m_Writer != null && !m_Writer.RequestFlush()) RecordingError=m_Writer.Error ?? "Native timestamp flush unavailable.";
    }
    void OnApplicationFocus(bool focused) { m_Focused=focused; }
    void OnDisable()
    {
        // Drain at most the entire native ring plus one status snapshot, without blocking acquisition.
        for (int i = 0; i < 9; ++i) { Drain(true); if (m_LastDrainCount == 0 || RecordingError != null) break; }
        m_Stopped = true; // A re-enabled component must not overwrite its previous segment.
        if (m_Writer == null) return;
        m_Writer.Dispose(); RecordingError=RecordingError ?? m_Writer.Error;
        try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(RecordingPath),"native_gaze_end_status.json"),
            JsonUtility.ToJson(new EndStatus {complete=RecordingError==null && m_FirstEpoch != 0 && !m_EpochChanged &&
                    m_LastStatus.dropped == 0 && m_LastSequence == m_LastStatus.records,
                acceptedRecords=m_AcceptedRecords,
                error=RecordingError ?? "", stoppedAt=Time.realtimeSinceStartupAsDouble,nativeStatus=m_LastStatus},true)); }
        catch(Exception e) {RecordingError=e.Message;Debug.LogError("[NativeGaze] "+e.Message);}
        m_Writer=null;
    }
    void OnDestroy() { if(Instance==this)Instance=null; }
}
