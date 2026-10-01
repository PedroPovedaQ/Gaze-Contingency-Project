using System.IO;
using UnityEngine;

/// <summary>
/// Which voice the assistant speaks in for this run.
///   Generic    = neutral ElevenLabs voice (legacy enum name retained).
///   SelfSimilar = a voice cloned from the participant via ElevenLabs.
/// </summary>
public enum VoiceCondition
{
    Generic,
    SelfSimilar
}

public enum NeutralVoiceProfile { Female, Male }

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
///       run_002_gaze_aware_voice-selfsimilar_2026-04-05_14-45-10/
///         ...
///     P002/
///       ...
///
/// Set ParticipantId before starting the game. If not set, auto-assigns
/// the next available ID (P001, P002, ...).
/// </summary>
public static class SessionConfig
{
    public const string InteractionPolicy = "right_controller_ray_trigger_v2";
    public const string RepeatEventVersion = "guidance-repeat-v1";
    public const string GuidanceAuditVersion = "guidance-decision-v1";
    public const string GazeReplayLinkVersion = "gaze-observation-link-v1";
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

    /// <summary>Voice condition for this run. Set before game start (default Generic).</summary>
    public static VoiceCondition Voice { get; set; } = VoiceCondition.Generic;

    /// <summary>
    /// Legacy script-pilot preference. Runtime wording now follows voice condition
    /// through VoicePromptText.PerspectiveForVoice.
    /// </summary>
    public static VoicePerspective Perspective { get; private set; } = VoicePerspective.Collaborative;
    /// <summary>Canonical wording version owned by the prompt formatter.</summary>
    public static string PerspectiveVersion => VoicePromptText.Version;
    public static bool PerspectiveLocked { get; private set; }

    /// <summary>Attempts to select the wording perspective before setup is locked.</summary>
    public static bool TrySetPerspective(VoicePerspective value)
    {
        if (PerspectiveLocked || !System.Enum.IsDefined(typeof(VoicePerspective), value))
            return false;
        Perspective = value;
        return true;
    }

    /// <summary>Locks the participant's perspective for enrollment and the full run.</summary>
    public static void LockPerspective()
    {
        PerspectiveLocked = true;
    }

    /// <summary>Explicit neutral voice selection; never inferred from gaze or recordings.</summary>
    public static NeutralVoiceProfile NeutralProfile { get; set; } = NeutralVoiceProfile.Female;
    public static int NeutralVoiceOption { get; private set; }
    public static string NeutralVoiceName => NeutralName(NeutralProfile, NeutralVoiceOption);
    public static string NeutralVoiceId => NeutralId(NeutralProfile, NeutralVoiceOption);

    public const int NeutralVoiceCount = 6;
    public const string NeutralCatalogVersion = "elevenlabs-six-per-gender-v1";
    // Preserve every existing saved option. Liam/Laura are appended, not inserted.
    // Provider metadata checked Oct 1: both additions are American English; Janet
    // keeps its legacy request alias. These labels do not infer participant identity.
    static readonly string[] MaleNames = { "Eric", "Roger", "Chris", "Will", "Brian", "Liam" };
    static readonly string[] FemaleNames = { "Janet", "Sarah", "Jessica", "Matilda", "Bella", "Laura" };
    static readonly string[] MaleIds = { "cjVigY5qzO86Huf0OWal", "CwhRBWXzGAHq8TQ4Fs17", "iP95p4xoKVk53GoZ742B", "bIHbv24MWmeRgasZH58o", "nPczCjzI2devNBz1zQrb", "TX3LPaxmHKxFdv7VOQHJ" };
    static readonly string[] FemaleIds = { "21m00Tcm4TlvDq8ikWAM", "EXAVITQu4vr4xnSDxMaL", "cgSgspJ2msm6clMCkdW9", "XrExE9yKIg1WjnnlVkGX", "hpp4J3VqNfWAUOO0d1Us", "FGY2WhTYpPnrIDTdsKH5" };

    public static string NeutralName(NeutralVoiceProfile gender, int option)
    {
        ValidateNeutral(gender, option);
        return (gender == NeutralVoiceProfile.Male ? MaleNames : FemaleNames)[option];
    }
    public static string NeutralId(NeutralVoiceProfile gender, int option)
    {
        ValidateNeutral(gender, option);
        return (gender == NeutralVoiceProfile.Male ? MaleIds : FemaleIds)[option];
    }
    static void ValidateNeutral(NeutralVoiceProfile gender, int option)
    {
        if (!System.Enum.IsDefined(typeof(NeutralVoiceProfile), gender) || option < 0 || option >= NeutralVoiceCount)
            throw new System.ArgumentOutOfRangeException(nameof(option), "Choose one of the six voices for this gender.");
    }
    public static void SelectNeutralVoice(NeutralVoiceProfile gender, int option)
    {
        ValidateNeutral(gender, option);
        NeutralProfile = gender; NeutralVoiceOption = option;
    }
    public static string VoiceTag => Voice == VoiceCondition.SelfSimilar
        ? "selfsimilar" : "neutral-" + NeutralProfile.ToString().ToLowerInvariant();

    /// <summary>
    /// ElevenLabs voice id for the self-similar clone (set at enrollment).
    /// Empty until a voice has been cloned this session.
    /// </summary>
    public static string SelfSimilarVoiceId { get; set; } = "";

    /// <summary>
    /// True while a self-similar voice is being recorded/cloned. While pending,
    /// the synthesizer stays silent rather than leaking the generic voice into a
    /// run labeled self-similar.
    /// </summary>
    public static bool SelfSimilarEnrollmentPending { get; set; } = false;

    public const string ScheduleVersion = "voice-blocks-v1";
    public static bool VoiceBlocksEnabled { get; private set; }
    public static bool NeutralFirst { get; private set; }
    public static string VoiceOrder => NeutralFirst ? "neutral_then_selfsimilar" : "selfsimilar_then_neutral";
    public static int CurrentBlock { get; private set; }
    public static string ParticipantPath => Path.Combine(RootPath, ParticipantId);

    public static void EnsureParticipantId()
    {
        if (string.IsNullOrEmpty(ParticipantId)) ParticipantId = FindNextParticipantId();
    }

    public static void ConfigureVoiceBlocks(bool replaceNeutralSelection = false)
    {
        EnsureParticipantId();
        if (!System.Text.RegularExpressions.Regex.IsMatch(ParticipantId, @"^P[0-9]{3,}$"))
            throw new System.InvalidOperationException("Use a coded numeric participant ID, such as P001.");
        Directory.CreateDirectory(ParticipantPath);
        string assignment = Path.Combine(ParticipantPath, "voice-order-v1.txt");
        if (File.Exists(assignment))
        {
            string value = File.ReadAllText(assignment).Trim();
            if (value != "neutral_then_selfsimilar" && value != "selfsimilar_then_neutral")
                throw new System.InvalidOperationException("Invalid saved voice assignment.");
            NeutralFirst = value == "neutral_then_selfsimilar";
        }
        else
        {
            if (!int.TryParse(ParticipantId.TrimStart('P', 'p'), out int number))
                throw new System.InvalidOperationException("Use a coded numeric participant ID, such as P001.");
            NeutralFirst = number % 2 == 1;
            File.WriteAllText(assignment, VoiceOrder);
        }
        string profilePath = Path.Combine(ParticipantPath, "neutral-profile-v1.txt");
        string choicePath = Path.Combine(ParticipantPath, "neutral-choice-v2.txt");
        if (!replaceNeutralSelection && File.Exists(choicePath))
        {
            var saved = File.ReadAllText(choicePath).Trim().Split('|');
            if (saved.Length != 2 || !System.Enum.TryParse(saved[0], out NeutralVoiceProfile gender) ||
                !int.TryParse(saved[1], out int option))
                throw new System.InvalidOperationException("Invalid saved neutral voice choice.");
            SelectNeutralVoice(gender, option);
        }
        else if (!replaceNeutralSelection && File.Exists(profilePath))
        {
            string saved = File.ReadAllText(profilePath).Trim();
            if (!System.Enum.TryParse(saved, out NeutralVoiceProfile profile) || !System.Enum.IsDefined(typeof(NeutralVoiceProfile), profile))
                throw new System.InvalidOperationException("Invalid saved neutral profile.");
            SelectNeutralVoice(profile, 0); // Existing participants used the first voice.
        }
        File.WriteAllText(choicePath, $"{NeutralProfile}|{NeutralVoiceOption}");
        File.WriteAllText(profilePath, NeutralProfile.ToString());
        using (var catalog = new StreamWriter(Path.Combine(ParticipantPath, "neutral-catalog-v1.json")))
            WriteNeutralCatalog(catalog);
        VoiceBlocksEnabled = true;
        ApplyRoundVoice(0);
    }

    /// <summary>
    /// Save available choices and stable request IDs, not an assertion that every
    /// candidate was heard or rated. All strings here come from the fixed catalog.
    /// </summary>
    public static void WriteNeutralCatalog(TextWriter writer)
    {
        writer.WriteLine($"{{\"schema\":1,\"catalog_version\":\"{NeutralCatalogVersion}\",\"provider\":\"elevenlabs\",\"candidates\":[");
        for (int genderIndex = 0; genderIndex < 2; genderIndex++)
        {
            var gender = genderIndex == 0 ? NeutralVoiceProfile.Male : NeutralVoiceProfile.Female;
            for (int option = 0; option < NeutralVoiceCount; option++)
            {
                if (genderIndex != 0 || option != 0) writer.WriteLine(",");
                writer.Write($"{{\"gender_presentation\":\"{gender}\",\"option\":{option},\"name\":\"{NeutralName(gender, option)}\",\"request_voice_id\":\"{NeutralId(gender, option)}\"}}");
            }
        }
        writer.WriteLine("]}");
    }

    public static VoiceCondition VoiceForRound(int round)
    {
        bool firstBlock = round < ChallengeSet.RoundsPerBlock;
        return firstBlock == NeutralFirst ? VoiceCondition.Generic : VoiceCondition.SelfSimilar;
    }

    public static string VoiceLabelForRound(int round) => VoiceForRound(round) == VoiceCondition.SelfSimilar
        ? "selfsimilar" : "neutral-" + NeutralProfile.ToString().ToLowerInvariant();

    public static void ApplyRoundVoice(int round)
    {
        if (!VoiceBlocksEnabled) return;
        CurrentBlock = round / ChallengeSet.RoundsPerBlock;
        Voice = VoiceForRound(round);
    }

    /// <summary>Root data folder path.</summary>
    public static string RootPath => Path.Combine(Application.persistentDataPath, k_RootFolder);

    /// <summary>
    /// Begins a new run. Creates the output folder and increments the run counter.
    /// Call this at game start (before any loggers open files).
    /// </summary>
    public static string BeginRun()
    {
        // Auto-assign participant ID if not set
        EnsureParticipantId();

        // A run owns one wording perspective even when a caller starts it
        // without the optional voice setup panel.
        LockPerspective();

        ConditionLabel = "gaze_aware";

        // Record the voice condition in the folder name so runs are self-describing.
        string voiceTag = VoiceBlocksEnabled ? "blocks-" + VoiceOrder : VoiceTag;
        ConditionLabel = $"{ConditionLabel}_voice-{voiceTag}";

        // Reset any stale run state that also resets the session should clear the
        // cloned voice id (handled by ResetForNewParticipant).

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
        using (var catalog = new StreamWriter(Path.Combine(CurrentRunFolder, "neutral-catalog-v1.json")))
            WriteNeutralCatalog(catalog);

        // Keep the selected wording perspective alongside the run so analysis
        // never has to infer it from cached audio or the voice block order.
        File.WriteAllText(
            Path.Combine(CurrentRunFolder, "session-perspective-v1.txt"),
            $"perspective_policy=shared_participant_directed_imperative\nversion={PerspectiveVersion}\n");

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
        VoiceBlocksEnabled = false;
        CurrentBlock = 0;
        ParticipantId = "";
        RunNumber = 0;
        CurrentRunFolder = "";
        ConditionLabel = "";
        // A cloned voice belongs to one participant — force re-enrollment for the next.
        SelfSimilarVoiceId = "";
        SelfSimilarEnrollmentPending = false;
        NeutralProfile = NeutralVoiceProfile.Female;
        NeutralVoiceOption = 0;
        Perspective = VoicePerspective.Collaborative;
        PerspectiveLocked = false;
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
