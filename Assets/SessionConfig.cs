using System.IO;
using UnityEngine;

/// <summary>
/// Shared session configuration for data logging.
/// Manages participant IDs, run numbering, and folder structure.
///
/// Output structure:
///   {persistentDataPath}/GazeData/
///     P001/
///       run_001_gaze_aware_2026-04-05_14-30-22/
///         gaze_log.csv
///         trial_events.csv
///         trial_summary.json
///       run_002_gaze_unaware_2026-04-05_14-45-10/
///         ...
///     P002/
///       ...
///
/// Set ParticipantId before starting the game. If not set, auto-assigns
/// the next available ID (P001, P002, ...).
/// </summary>
public static class SessionConfig
{
    const string k_RootFolder = "GazeData";
    const string k_Tag = "[Session]";

    /// <summary>Current participant ID (e.g., "P001"). Set before game start.</summary>
    public static string ParticipantId { get; set; } = "";

    /// <summary>Current run number within this participant (auto-incremented).</summary>
    public static int RunNumber { get; private set; }

    /// <summary>Full path to the current run's output folder.</summary>
    public static string CurrentRunFolder { get; private set; } = "";

    /// <summary>The condition label for the current run.</summary>
    public static string ConditionLabel { get; private set; } = "";

    /// <summary>Root data folder path.</summary>
    public static string RootPath => Path.Combine(Application.persistentDataPath, k_RootFolder);

    /// <summary>
    /// Begins a new run. Creates the output folder and increments the run counter.
    /// Call this at game start (before any loggers open files).
    /// </summary>
    /// <param name="gazeAware">True if the gaze-aware condition is active.</param>
    public static string BeginRun(bool gazeAware)
    {
        string conditionLabel = gazeAware ? "gaze_aware" : "gaze_unaware";
        return BeginRun(conditionLabel);
    }

    /// <summary>
    /// Begins a new run with an explicit condition label.
    /// Use this when a run contains multiple blocks/conditions.
    /// </summary>
    public static string BeginRun(string conditionLabel)
    {
        // Auto-assign participant ID if not set
        if (string.IsNullOrEmpty(ParticipantId))
            ParticipantId = FindNextParticipantId();

        ConditionLabel = string.IsNullOrWhiteSpace(conditionLabel)
            ? "unspecified"
            : conditionLabel;

        // Find next run number for this participant
        string participantDir = Path.Combine(RootPath, ParticipantId);
        if (!Directory.Exists(participantDir))
            Directory.CreateDirectory(participantDir);

        RunNumber = CountExistingRuns(participantDir) + 1;

        // Create run folder
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string folderName = $"run_{RunNumber:D3}_{ConditionLabel}_{timestamp}";
        CurrentRunFolder = Path.Combine(participantDir, folderName);
        Directory.CreateDirectory(CurrentRunFolder);

        Debug.Log($"{k_Tag} Run started: {ParticipantId} / run {RunNumber} / {ConditionLabel}");
        Debug.Log($"{k_Tag} Output folder: {CurrentRunFolder}");

        return CurrentRunFolder;
    }

    /// <summary>
    /// Returns the full path for a file in the current run folder.
    /// </summary>
    public static string GetFilePath(string filename)
    {
        if (string.IsNullOrEmpty(CurrentRunFolder))
        {
            Debug.LogWarning($"{k_Tag} No active run — falling back to persistentDataPath");
            return Path.Combine(Application.persistentDataPath, filename);
        }
        return Path.Combine(CurrentRunFolder, filename);
    }

    /// <summary>
    /// Clears the active run/session identity so the next started run is logged
    /// as a fresh participant with a fresh run number.
    /// </summary>
    public static void ResetForNewParticipant()
    {
        Debug.Log($"{k_Tag} Resetting session state for a new participant");
        ParticipantId = "";
        RunNumber = 0;
        CurrentRunFolder = "";
        ConditionLabel = "";
    }

    /// <summary>
    /// Scans the root folder for the next available participant ID.
    /// </summary>
    static string FindNextParticipantId()
    {
        string root = RootPath;
        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
            return "P001";
        }

        int maxId = 0;
        foreach (var dir in Directory.GetDirectories(root))
        {
            string name = Path.GetFileName(dir);
            if (name.StartsWith("P") && int.TryParse(name.Substring(1), out int id))
            {
                if (id > maxId) maxId = id;
            }
        }
        return $"P{maxId + 1:D3}";
    }

    /// <summary>
    /// Counts existing run folders for a participant.
    /// </summary>
    static int CountExistingRuns(string participantDir)
    {
        int count = 0;
        foreach (var dir in Directory.GetDirectories(participantDir))
        {
            if (Path.GetFileName(dir).StartsWith("run_"))
                count++;
        }
        return count;
    }
}
