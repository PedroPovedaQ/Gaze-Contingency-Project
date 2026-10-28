using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    TrialReviewCatalog m_Catalog;
    TrialReviewCatalog.Dataset m_Dataset;
    bool m_DatasetPanel, m_ChooseAttachment;
    string m_DatasetDraft = "", m_IdDraft = "", m_LabelDraft = "", m_AssignmentReason = "", m_CatalogError;
    int m_CatalogTab;
    Vector2 m_CatalogScroll;
    string AssignedParticipantId => m_Dataset?.participantId ?? ParticipantId;
    string DatasetName => m_Dataset?.name ?? "";
    string CatalogPath => Path.Combine(Application.persistentDataPath, "participant-datasets.json");
    [Serializable] sealed class RunExportReceipt { public string study_data_path; }

    void InitializeCatalog()
    {
        if(m_CaptureMode) return;
        try
        {
            m_Catalog = new TrialReviewCatalog(CatalogPath);
            m_Dataset = m_Timeline == null ? null : m_Catalog.Register(m_Timeline.FilePath,
                m_Timeline.Header.recordingId, ParticipantId, PlayerPrefs.GetString(NameKey, ""));
            if (m_Dataset != null)
            {
                m_Catalog.AttachFolderFiles(m_Dataset, Path.GetDirectoryName(m_Timeline.FilePath));
                m_StudyDataFolder = m_Dataset.studyDataPath ?? "";
                m_ParticipantName = m_Catalog.FindParticipant(m_Dataset.participantId)?.name ?? "";
            }
            m_Catalog.Save(); m_CatalogError = null;
        }
        catch (Exception error) { m_CatalogError = error.Message; m_Dataset = null; }
    }
    void OpenDatasetPanel(int tab = 0)
    {
        m_Playing = false; m_FollowLive = false; Sync(true);
        m_TimelineDrag = false; GUIUtility.hotControl = GUIUtility.keyboardControl = 0;
        InitializeCatalog();
        m_DatasetDraft = DatasetName; m_IdDraft = AssignedParticipantId; m_LabelDraft = m_ParticipantName;
        m_AssignmentReason = ""; m_CatalogTab = tab; m_CatalogScroll = Vector2.zero; m_DatasetPanel = true;
    }
    void CatalogAction(Action action)
    {
        if(m_CaptureMode) return;
        string datasetId = m_Dataset?.id;
        try { action(); m_Catalog.Save(); m_CatalogError = null; }
        catch (Exception error)
        {
            m_CatalogError = error.Message;
            // A failed save must not leak an unpersisted reassignment into CSVs.
            try
            {
                m_Catalog = new TrialReviewCatalog(CatalogPath);
                m_Dataset = m_Catalog.Data.datasets.FirstOrDefault(d => d.id == datasetId);
                m_ParticipantName = m_Catalog.FindParticipant(AssignedParticipantId)?.name ?? "";
            }
            catch { m_Catalog = null; m_Dataset = null; m_RunCsvRequested = false; }
        }
    }
    void RevealFile(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("File is no longer at its attached location.", path);
            // A structured quoted argument: filenames are never interpreted by a shell.
            Process.Start(new ProcessStartInfo("/usr/bin/open", "-R " + Argument(path)) { UseShellExecute = false });
        }
        catch (Exception error) { m_CatalogError = error.Message; }
    }
    void AttachStudyFolder(string path)
    {
        m_StudyDataFolder = path;
        if (m_Catalog != null && m_Dataset != null) CatalogAction(() => {
            m_Dataset.studyDataPath = path; m_Catalog.AttachFolderFiles(m_Dataset, path);
        });
        m_RunCsvRequested = true;
    }
    void AttachGeneratedFiles(string folder)
    {
        if (m_Catalog == null || m_Dataset == null) return;
        CatalogAction(() => {
            m_Catalog.AttachFolderFiles(m_Dataset, folder);
            var receipt = JsonUtility.FromJson<RunExportReceipt>(File.ReadAllText(Path.Combine(folder,"desktop-export.json")));
            if (!string.IsNullOrEmpty(receipt?.study_data_path) && Directory.Exists(receipt.study_data_path))
            {
                m_Dataset.studyDataPath = m_StudyDataFolder = receipt.study_data_path;
                m_Catalog.AttachFolderFiles(m_Dataset, receipt.study_data_path);
            }
        });
    }
    void DatasetDialog(float width, float height)
    {
        var rect = new Rect(width / 2 - 460, 74, 920, height - 148); Panel(rect);
        GUILayout.BeginArea(new Rect(rect.x + 24, rect.y + 20, rect.width - 48, rect.height - 40));
        GUILayout.BeginHorizontal(); GUILayout.Label("Participants & datasets", m_Title);
        if (GUILayout.Button("Close", m_Chip, GUILayout.Width(90))) m_DatasetPanel = false;
        GUILayout.EndHorizontal(); GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        string[] tabs = { "Current dataset", "Participants", "Saved datasets" };
        for (int i = 0; i < tabs.Length; i++) if (GUILayout.Button(tabs[i], m_CatalogTab == i ? m_ActiveChip : m_Chip))
        { m_CatalogTab = i; m_CatalogScroll = Vector2.zero; }
        GUILayout.EndHorizontal(); GUILayout.Space(12);
        if (!string.IsNullOrEmpty(m_CatalogError)) GUILayout.Label(m_CatalogError, m_Label);
        m_CatalogScroll = GUILayout.BeginScrollView(m_CatalogScroll);
        if (m_Catalog != null)
        {
            if (m_CatalogTab == 0) CurrentDatasetControls();
            else if (m_CatalogTab == 1) ParticipantControls();
            else foreach (var dataset in m_Catalog.Data.datasets)
            {
                GUILayout.Label((string.IsNullOrEmpty(dataset.name) ? Path.GetFileName(Path.GetDirectoryName(dataset.recordingPath)) : dataset.name) +
                    " · " + dataset.participantId, m_Heading);
                GUILayout.Label(dataset.recordingPath, m_MutedLabel); GUILayout.BeginHorizontal();
                if (Button("Open dataset")) { m_DatasetPanel = false; StartCoroutine(Open(dataset.recordingPath)); }
                if (Button("Show in Finder")) RevealFile(dataset.recordingPath);
                GUILayout.EndHorizontal(); GUILayout.Space(12);
            }
        }
        GUILayout.EndScrollView(); GUILayout.Space(8);
        GUILayout.Label("Local catalog · source recordings and source IDs remain unchanged", m_MutedLabel);
        GUILayout.EndArea();
    }
    void CurrentDatasetControls()
    {
        if (m_Dataset == null)
        {
            GUILayout.Label("Open a recording to add its dataset.", m_Label);
            if (Button("Open run…")) { m_DatasetPanel = false; Browse(false); }
            return;
        }
        GUILayout.Label("Dataset / run name", m_Heading);
        m_DatasetDraft = GUILayout.TextField(m_DatasetDraft, 120, m_Field, GUILayout.Height(36));
        GUILayout.Label("Participant ID — choose below or enter a new coded ID", m_Heading);
        m_IdDraft = GUILayout.TextField(m_IdDraft, 64, m_Field, GUILayout.Height(36));
        GUILayout.BeginHorizontal();
        int col = 0;
        foreach (var participant in m_Catalog.Data.participants)
        {
            if (col++ == 5) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); col = 1; }
            if (GUILayout.Button(participant.id, m_IdDraft == participant.id ? m_ActiveChip : m_Chip))
            { m_IdDraft = participant.id; m_LabelDraft = participant.name ?? ""; }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Participant name (optional, shared by this ID's datasets)", m_Heading);
        m_LabelDraft = GUILayout.TextField(m_LabelDraft, 120, m_Field, GUILayout.Height(36));
        if (m_IdDraft.Trim().ToUpperInvariant() != m_Dataset.participantId)
        {
            GUILayout.Label("Reason for reassignment", m_Heading);
            m_AssignmentReason = GUILayout.TextField(m_AssignmentReason, 120, m_Field, GUILayout.Height(36));
        }
        GUILayout.Label("Recorded source ID: " + m_Dataset.sourceParticipantId, m_MutedLabel);
        if (Button("Save participant & dataset")) CatalogAction(() => {
            if (m_IdDraft.Trim().ToUpperInvariant() != m_Dataset.participantId && string.IsNullOrWhiteSpace(m_AssignmentReason))
                throw new ArgumentException("Add a short reason for reassigning this dataset.");
            var participant = m_Catalog.SaveParticipant(m_IdDraft, m_LabelDraft);
            m_Catalog.Edit(m_Dataset, participant.id, m_DatasetDraft, m_AssignmentReason);
            m_IdDraft = participant.id; m_ParticipantName = participant.name;
            m_RunCsvFolder = ""; m_RunCsvRequested = true;
        });
        GUILayout.Space(14); GUILayout.Label("FILES — one attachment per file", m_Heading);
        GUILayout.BeginHorizontal();
        if (Button("Attach file…")) { m_DatasetPanel = false; Browse(false); m_ChooseAttachment = true; RefreshDirectory(Path.GetDirectoryName(m_Dataset.recordingPath)); }
        if (Button("Open dataset folder")) Application.OpenURL(new Uri(Path.GetDirectoryName(m_Dataset.recordingPath)).AbsoluteUri);
        if (Button("Refresh files")) CatalogAction(() => m_Catalog.AttachFolderFiles(m_Dataset, Path.GetDirectoryName(m_Dataset.recordingPath)));
        GUILayout.EndHorizontal();
        foreach (var file in m_Dataset.files)
        {
            string label=Path.GetFileName(file.path)=="trials_condensed.csv"?"Condensed trial CSV · trials_condensed.csv":Path.GetFileName(file.path);
            GUILayout.BeginHorizontal(); GUILayout.Label(label + (File.Exists(file.path) ? "" : " · missing"), m_Label);
            if (GUILayout.Button("Finder", m_Chip, GUILayout.Width(85))) RevealFile(file.path);
            GUILayout.EndHorizontal(); GUILayout.Label(file.path, m_MutedLabel);
        }
        if (m_Dataset.assignments.Count > 0)
        {
            GUILayout.Space(14); GUILayout.Label("ASSIGNMENT HISTORY", m_Heading);
            foreach (var change in m_Dataset.assignments)
                GUILayout.Label($"{change.utc} · {change.previousId} → {change.participantId}\n{change.reason}", m_MutedLabel);
        }
    }
    void ParticipantControls()
    {
        GUILayout.Label("Create or update a participant", m_Heading);
        GUILayout.Label("Coded ID", m_MutedLabel); m_IdDraft = GUILayout.TextField(m_IdDraft, 64, m_Field, GUILayout.Height(36));
        GUILayout.Label("Optional name", m_MutedLabel); m_LabelDraft = GUILayout.TextField(m_LabelDraft, 120, m_Field, GUILayout.Height(36));
        if (Button("Save participant")) CatalogAction(() => {
            var person = m_Catalog.SaveParticipant(m_IdDraft, m_LabelDraft);
            if (person.id == AssignedParticipantId) { m_ParticipantName = person.name; m_RunCsvRequested = true; m_RunCsvFolder = ""; }
        });
        GUILayout.Label("Creating a participant here does not change headset enrollment. Assign a recorded run on Current dataset.", m_MutedLabel);
        GUILayout.Space(12);
        foreach (var person in m_Catalog.Data.participants)
            if (Button(person.id + " · " + person.name + " · " + m_Catalog.Data.datasets.Count(d => d.participantId == person.id) + " datasets"))
            { m_IdDraft = person.id; m_LabelDraft = person.name ?? ""; }
    }
}
