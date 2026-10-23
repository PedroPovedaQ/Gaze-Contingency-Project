using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Compatibility per-frame CSV, sourced exclusively from SharedGazeStream.
/// Raw input deliveries and gaps are retained separately in EyeMeasurements JSONL.
/// Object intersections are interaction proxies, never validated fixations or blinks.
/// </summary>
public class GazeDataLogger : MonoBehaviour
{
    const string k_Tag = "[GazeLog]";
    const int k_FlushInterval = 300;
    internal const string Header = "timestamp,frame,gaze_origin_x,gaze_origin_y,gaze_origin_z," +
        "gaze_dir_x,gaze_dir_y,gaze_dir_z,gaze_rot_x,gaze_rot_y,gaze_rot_z,gaze_rot_w," +
        "left_eye_x,left_eye_y,left_eye_z,right_eye_x,right_eye_y,right_eye_z,left_eye_open,right_eye_open," +
        "fixation_x,fixation_y,fixation_z,hovered_object,hovered_shape,hovered_color,hovered_shelf_level," +
        "is_target,dwell_progress,objective_shape,objective_color,objective_index,game_state,ray_visible," +
        "blink,blink_count,participant_id,run_number,trial_id,voice_condition,search_active,hovered_object_id,is_practice," +
        "gaze_valid,invalid_reason,observation_sequence,input_event_time,receipt_time,delivery_age_seconds," +
        "new_input_update,eye_measurement_file,eye_event_measurement,left_eye_valid,right_eye_valid," +
        "left_openness_valid,right_openness_valid,htc_query_receipt_time";

    StreamWriter m_Writer;
    FindObjectGameManager m_GameManager;
    XRInteractorLineVisual m_Line;
    string m_LogFolder, m_LogParticipant;
    readonly string m_PreparationId = Guid.NewGuid().ToString("N");
    int m_FramesSinceFlush;
    long m_LastWrittenSequence = -1;
    public string RecordingError { get; private set; }

    // Retained for old callers. A signal is not a validated event detector.
    public int BlinkCount => -1;
    public bool HasBlinkSignal => false;

    void OnEnable()
    {
        m_Line = GetComponent<XRInteractorLineVisual>();
        OpenLog();
    }

    void OpenLog()
    {
        CloseLog();
        RecordingError = null;
        m_LogFolder = SessionConfig.CurrentRunFolder;
        m_LogParticipant = SessionConfig.ParticipantId;
        m_FramesSinceFlush = 0;
        try
        {
            string path;
            if (string.IsNullOrEmpty(m_LogFolder))
            {
                string parent = string.IsNullOrEmpty(m_LogParticipant)
                    ? Path.Combine(Application.persistentDataPath, "PreparationLogs") : SessionConfig.ParticipantPath;
                path = Path.Combine(parent, "preparation_" + m_PreparationId, "gaze_log.csv");
            }
            else path = SessionConfig.GetFilePath("gaze_log.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool hasRows = File.Exists(path) && new FileInfo(path).Length > 0;
            if (hasRows)
            {
                using var reader = new StreamReader(path);
                if (reader.ReadLine() != Header)
                    throw new IOException("Existing gaze CSV has a different schema; start a new run. Existing data preserved.");
            }
            // Re-enabling the component must not truncate an earlier recording.
            m_Writer = new StreamWriter(path, true, new UTF8Encoding(false));
            if (!hasRows) m_Writer.WriteLine(Header);
            m_Writer.Flush();
            Debug.Log($"{k_Tag} Logging to {path}");
        }
        catch (Exception e) { Fail(e); }
    }

    void OnDisable() => CloseLog();
    void OnApplicationPause(bool paused)
    {
        if (!paused || m_Writer == null) return;
        try { m_Writer.Flush(); } catch (Exception e) { Fail(e); }
    }
    void CloseLog()
    {
        var writer = m_Writer;
        m_Writer = null;
        if (writer == null) return;
        try { writer.Dispose(); }
        catch (Exception e) { RecordingError = e.Message; Debug.LogError($"{k_Tag} {e.Message}"); }
    }
    void Fail(Exception e)
    {
        RecordingError = e.Message;
        Debug.LogError($"{k_Tag} CSV recording stopped: {e.Message}");
        CloseLog();
    }

    void Update()
    {
        if (m_LogFolder != SessionConfig.CurrentRunFolder || m_LogParticipant != SessionConfig.ParticipantId) OpenLog();
        if (m_Writer == null) return;
        if (m_GameManager == null) m_GameManager = FindFirstObjectByType<FindObjectGameManager>();
        var stream = SharedGazeStream.Instance;
        var sample = stream != null ? stream.Latest : null;
        if (sample == null || sample.sequence == m_LastWrittenSequence) return;
        m_LastWrittenSequence = sample.sequence;
        bool valid = stream.TryGetValid(out _);
        SpawnableObjectInfo hit = null;
        if (valid) stream.TryHit(out hit);
        bool playing = m_GameManager != null && m_GameManager.CurrentState == FindObjectGameManager.GameState.Playing;
        string shape = playing ? m_GameManager.CurrentTarget.shape : "";
        string color = playing ? m_GameManager.CurrentTarget.color : "";
        var rotation = Quaternion.LookRotation(valid ? sample.worldDirection : Vector3.forward);
        bool nativeFresh = valid && GazeQualityPolicy.IsFresh(true, sample.htcQueryReceiptTime,
            Time.realtimeSinceStartupAsDouble, sample.maxDeliveryAgeSeconds);
        bool leftValid = nativeFresh && sample.htcGazeAvailable && sample.leftValid;
        bool rightValid = nativeFresh && sample.htcGazeAvailable && sample.rightValid;
        bool leftOpenValid = nativeFresh && sample.opennessAvailable && sample.leftOpennessValid;
        bool rightOpenValid = nativeFresh && sample.opennessAvailable && sample.rightOpennessValid;
        var cells = new[] {
            F(Time.timeAsDouble), Time.frameCount.ToString(CultureInfo.InvariantCulture),
            F(sample.worldOrigin.x,valid), F(sample.worldOrigin.y,valid), F(sample.worldOrigin.z,valid),
            F(sample.worldDirection.x,valid), F(sample.worldDirection.y,valid), F(sample.worldDirection.z,valid),
            F(rotation.x,valid), F(rotation.y,valid), F(rotation.z,valid), F(rotation.w,valid),
            F(sample.leftWorldOrigin.x,leftValid), F(sample.leftWorldOrigin.y,leftValid), F(sample.leftWorldOrigin.z,leftValid),
            F(sample.rightWorldOrigin.x,rightValid), F(sample.rightWorldOrigin.y,rightValid), F(sample.rightWorldOrigin.z,rightValid),
            F(sample.leftOpenness,leftOpenValid), F(sample.rightOpenness,rightOpenValid),
            "", "", "", // No validated fixation point or vendor binocular intersection.
            hit != null ? hit.name : "", hit != null ? hit.shapeName : "", hit != null ? hit.colorName : "",
            hit != null ? hit.shelfLevel.ToString(CultureInfo.InvariantCulture) : "",
            B(hit != null && playing && hit.shapeName == shape && hit.colorName == color), "0",
            shape, color, sample.round >= 0 ? sample.round.ToString(CultureInfo.InvariantCulture) : "",
            m_GameManager != null ? m_GameManager.CurrentState.ToString().ToLowerInvariant() : "idle",
            B(m_Line != null && m_Line.enabled), "", "", // Blink detection is deferred to validated analysis.
            sample.participant ?? "", sample.run.ToString(CultureInfo.InvariantCulture),
            sample.sourceTrialId ?? "", sample.voice ?? "", B(m_GameManager != null && m_GameManager.SearchActive),
            hit != null ? hit.objectId : "", B(sample.practice),
            B(valid), valid ? "" : sample.invalidReason == "" ? "stale_at_csv_read" : sample.invalidReason,
            sample.sequence.ToString(CultureInfo.InvariantCulture), F(sample.inputEventTime,sample.deviceAvailable),
            F(sample.receiptTime), F(sample.ageSeconds,sample.deviceAvailable), B(sample.newInputUpdate),
            stream.RecordingPath ?? "", "ray_intersection_proxy_not_validated_fixation", B(leftValid), B(rightValid),
            B(leftOpenValid), B(rightOpenValid), F(sample.htcQueryReceiptTime,sample.htcQueryReceiptTime > 0)
        };
        for (int i = 0; i < cells.Length; i++) cells[i] = Escape(cells[i]);
        try
        {
            m_Writer.WriteLine(string.Join(",", cells));
            if (++m_FramesSinceFlush >= k_FlushInterval) { m_Writer.Flush(); m_FramesSinceFlush = 0; }
        }
        catch (Exception e) { Fail(e); }
    }

    static string B(bool value) => value ? "1" : "0";
    static string F(double value, bool valid = true) => valid && !double.IsNaN(value) && !double.IsInfinity(value)
        ? value.ToString("F6", CultureInfo.InvariantCulture) : "";
    static string Escape(string value)
    {
        value = value ?? "";
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
