using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Logs comprehensive per-frame eye tracking and gaze data to CSV.
/// Captures: gaze origin/direction/rotation, per-eye positions and openness,
/// fixation point, hovered object info, dwell progress, current objective,
/// target match status, and blink detection.
/// </summary>
public class GazeDataLogger : MonoBehaviour
{
    const string k_Tag = "[GazeLog]";
    const int k_FlushInterval = 300;

    // Blink detection thresholds
    const float k_BlinkClosedThreshold = 0.2f;  // eye openness below this = closed
    const float k_BlinkMaxDuration = 0.5f;       // max blink duration in seconds
    const float k_BlinkMinDuration = 0.04f;      // min blink duration (debounce noise)

    XRBaseInputInteractor m_Interactor;
    StreamWriter m_Writer;
    int m_FrameCount;

    // Cached references
    GazeHighlightManager m_DwellSelector;
    FindObjectGameManager m_GameManager;
    InputDevice m_EyeDevice;
    bool m_EyeDeviceSearched;

    // Hover state
    string m_HoveredObject = "";
    string m_HoveredShape = "";
    string m_HoveredColor = "";
    int m_HoveredShelfLevel = -1;

    // Blink detection state
    bool m_EyesClosed;
    float m_EyesClosedStart;
    int m_BlinkCount;
    bool m_BlinkThisFrame;

    /// <summary>Total blinks detected since logging started.</summary>
    public int BlinkCount => m_BlinkCount;

    void OnEnable()
    {
        m_Interactor = GetComponent<XRBaseInputInteractor>();
        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.AddListener(OnHoverEntered);
            m_Interactor.hoverExited.AddListener(OnHoverExited);
        }

        // Write to the current run folder if available, else fallback
        var path = SessionConfig.GetFilePath("gaze_log.csv");

        m_Writer = new StreamWriter(path, false, Encoding.UTF8);
        m_Writer.WriteLine(string.Join(",",
            "timestamp", "frame",
            "gaze_origin_x", "gaze_origin_y", "gaze_origin_z",
            "gaze_dir_x", "gaze_dir_y", "gaze_dir_z",
            "gaze_rot_x", "gaze_rot_y", "gaze_rot_z", "gaze_rot_w",
            "left_eye_x", "left_eye_y", "left_eye_z",
            "right_eye_x", "right_eye_y", "right_eye_z",
            "left_eye_open", "right_eye_open",
            "fixation_x", "fixation_y", "fixation_z",
            "hovered_object", "hovered_shape", "hovered_color", "hovered_shelf_level",
            "is_target", "dwell_progress",
            "objective_shape", "objective_color", "objective_index",
            "game_state", "ray_visible",
            "blink", "blink_count"
        ));
        m_Writer.Flush();

        Debug.Log($"{k_Tag} Logging to {path}");
    }

    void OnDisable()
    {
        if (m_Interactor != null)
        {
            m_Interactor.hoverEntered.RemoveListener(OnHoverEntered);
            m_Interactor.hoverExited.RemoveListener(OnHoverExited);
        }

        if (m_Writer != null)
        {
            m_Writer.Flush();
            m_Writer.Close();
            m_Writer = null;
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        m_HoveredObject = args.interactableObject.transform.name;
        var info = args.interactableObject.transform.GetComponent<SpawnableObjectInfo>();
        if (info != null)
        {
            m_HoveredShape = info.shapeName;
            m_HoveredColor = info.colorName;
            m_HoveredShelfLevel = info.shelfLevel;
        }
        else
        {
            m_HoveredShape = "";
            m_HoveredColor = "";
            m_HoveredShelfLevel = -1;
        }
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (m_HoveredObject == args.interactableObject.transform.name)
        {
            m_HoveredObject = "";
            m_HoveredShape = "";
            m_HoveredColor = "";
            m_HoveredShelfLevel = -1;
        }
    }

    void Update()
    {
        if (m_Writer == null) return;

        // Lazy-resolve references
        if (m_DwellSelector == null)
            m_DwellSelector = FindObjectOfType<GazeHighlightManager>();
        if (m_GameManager == null)
            m_GameManager = FindObjectOfType<FindObjectGameManager>();

        // Don't log during round transitions (fixation cross period)
        if (m_GameManager != null &&
            m_GameManager.CurrentState == FindObjectGameManager.GameState.Transitioning)
            return;

        // Try to find eye tracking device
        if (!m_EyeDeviceSearched)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, devices);
            if (devices.Count > 0)
                m_EyeDevice = devices[0];
            m_EyeDeviceSearched = true;
        }

        // Gaze interactor transform
        var t = transform;
        var origin = t.position;
        var dir = t.forward;
        var rot = t.rotation;

        // Per-eye data (default NaN if unavailable)
        float lx = float.NaN, ly = float.NaN, lz = float.NaN;
        float rx = float.NaN, ry = float.NaN, rz = float.NaN;
        float leftOpen = float.NaN, rightOpen = float.NaN;
        float fx = float.NaN, fy = float.NaN, fz = float.NaN;

        if (m_EyeDevice.isValid)
        {
            if (m_EyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
            {
                if (eyes.TryGetLeftEyePosition(out Vector3 leftPos))
                { lx = leftPos.x; ly = leftPos.y; lz = leftPos.z; }
                if (eyes.TryGetRightEyePosition(out Vector3 rightPos))
                { rx = rightPos.x; ry = rightPos.y; rz = rightPos.z; }
                if (eyes.TryGetLeftEyeOpenAmount(out float lo))
                    leftOpen = lo;
                if (eyes.TryGetRightEyeOpenAmount(out float ro))
                    rightOpen = ro;
                if (eyes.TryGetFixationPoint(out Vector3 fixPt))
                { fx = fixPt.x; fy = fixPt.y; fz = fixPt.z; }
            }
        }

        // Blink detection
        m_BlinkThisFrame = false;
        if (!float.IsNaN(leftOpen) && !float.IsNaN(rightOpen))
        {
            bool bothClosed = leftOpen < k_BlinkClosedThreshold && rightOpen < k_BlinkClosedThreshold;

            if (bothClosed && !m_EyesClosed)
            {
                // Eyes just closed
                m_EyesClosed = true;
                m_EyesClosedStart = Time.time;
            }
            else if (!bothClosed && m_EyesClosed)
            {
                // Eyes just opened — check if it was a valid blink duration
                float closedDuration = Time.time - m_EyesClosedStart;
                if (closedDuration >= k_BlinkMinDuration && closedDuration <= k_BlinkMaxDuration)
                {
                    m_BlinkCount++;
                    m_BlinkThisFrame = true;
                }
                m_EyesClosed = false;
            }
        }

        // Objective and target state
        string objShape = "", objColor = "";
        int objIndex = -1;
        bool isTarget = false;
        string gameState = "idle";

        if (m_GameManager != null)
        {
            gameState = m_GameManager.CurrentState.ToString().ToLower();
            if (m_GameManager.CurrentState == FindObjectGameManager.GameState.Playing)
            {
                var objectives = m_GameManager.Objectives;
                int idx = m_GameManager.CurrentObjectiveIndex;
                if (idx < objectives.Count)
                {
                    objShape = objectives[idx].shape;
                    objColor = objectives[idx].color;
                    objIndex = idx;
                    isTarget = !string.IsNullOrEmpty(m_HoveredShape) &&
                               m_HoveredShape == objShape &&
                               m_HoveredColor == objColor;
                }
            }
        }

        // Dwell progress
        float dwellProgress = m_DwellSelector != null ? m_DwellSelector.DwellProgress : 0f;

        // Ray visibility
        var lineVisual = GetComponent<XRInteractorLineVisual>();
        bool rayVisible = lineVisual != null && lineVisual.enabled;

        m_Writer.WriteLine(string.Format(
            "{0:F4},{1}," +
            "{2:F5},{3:F5},{4:F5}," +
            "{5:F5},{6:F5},{7:F5}," +
            "{8:F5},{9:F5},{10:F5},{11:F5}," +
            "{12},{13},{14}," +
            "{15},{16},{17}," +
            "{18},{19}," +
            "{20},{21},{22}," +
            "{23},{24},{25},{26}," +
            "{27},{28:F4}," +
            "{29},{30},{31}," +
            "{32},{33}," +
            "{34},{35}",
            Time.time, Time.frameCount,
            origin.x, origin.y, origin.z,
            dir.x, dir.y, dir.z,
            rot.x, rot.y, rot.z, rot.w,
            F(lx), F(ly), F(lz),
            F(rx), F(ry), F(rz),
            F(leftOpen), F(rightOpen),
            F(fx), F(fy), F(fz),
            m_HoveredObject, m_HoveredShape, m_HoveredColor, m_HoveredShelfLevel,
            isTarget ? 1 : 0, dwellProgress,
            objShape, objColor, objIndex,
            gameState, rayVisible ? 1 : 0,
            m_BlinkThisFrame ? 1 : 0, m_BlinkCount
        ));

        m_FrameCount++;
        if (m_FrameCount >= k_FlushInterval)
        {
            m_Writer.Flush();
            m_FrameCount = 0;
        }
    }

    static string F(float v) => float.IsNaN(v) ? "" : v.ToString("F5");
}
