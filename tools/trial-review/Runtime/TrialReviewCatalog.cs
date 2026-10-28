using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Local labels and file links. Source recordings are never rewritten.</summary>
public sealed class TrialReviewCatalog
{
    [Serializable] public sealed class Participant { public string id, name; }
    [Serializable] public sealed class Attachment { public string path; }
    [Serializable] public sealed class Assignment { public string utc, previousId, participantId, reason; }
    [Serializable] public sealed class Dataset
    {
        public string id, recordingId, sourceParticipantId, participantId, name, recordingPath, studyDataPath;
        public List<Attachment> files = new List<Attachment>();
        public List<Assignment> assignments = new List<Assignment>();
    }
    [Serializable] public sealed class Document
    {
        public int schema = 1;
        public List<Participant> participants = new List<Participant>();
        public List<Dataset> datasets = new List<Dataset>();
    }
    public Document Data { get; private set; }
    public string FilePath { get; }
    string m_Saved;
    public static TrialReviewCatalog DevelopmentFixture() => new TrialReviewCatalog();
    TrialReviewCatalog() { Data = new Document(); }
    public TrialReviewCatalog(string path)
    {
        FilePath = Path.GetFullPath(path);
        m_Saved = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        Data = m_Saved == null ? new Document() : JsonUtility.FromJson<Document>(m_Saved);
        if (Data == null || Data.schema != 1 || Data.participants == null || Data.datasets == null)
            throw new InvalidDataException("Unsupported participant catalog; existing file was preserved.");
    }
    public Participant FindParticipant(string id) => Data.participants.FirstOrDefault(p => p.id == id);
    static string Label(string value)
    {
        value = (value ?? "").Trim();
        if (value.Length > 120 || value.Any(char.IsControl)) throw new ArgumentException("Use at most 120 characters without line breaks.");
        return value;
    }
    public Participant SaveParticipant(string id, string name)
    {
        id = (id ?? "").Trim().ToUpperInvariant(); name = Label(name);
        if (!Regex.IsMatch(id, @"^[A-Z0-9][A-Z0-9_-]{0,63}$"))
            throw new ArgumentException("Participant ID: use 1–64 letters, digits, underscores or hyphens, such as P020.");
        var participant = FindParticipant(id);
        if (participant == null) { participant = new Participant { id = id }; Data.participants.Add(participant); }
        participant.name = name;
        return participant;
    }
    public Dataset Register(string recordingPath, string recordingId, string sourceId, string legacyName)
    {
        recordingPath = Path.GetFullPath(recordingPath);
        var dataset = Data.datasets.FirstOrDefault(d => d.sourceParticipantId == sourceId &&
            (!string.IsNullOrEmpty(recordingId) ? d.recordingId == recordingId :
                string.IsNullOrEmpty(d.recordingId) && d.recordingPath == recordingPath));
        if (dataset == null)
        {
            string id = string.IsNullOrEmpty(sourceId) ? "UNASSIGNED" : sourceId;
            // Preserve historical source identifiers exactly; editable IDs use validation above.
            if (FindParticipant(id) == null) Data.participants.Add(new Participant { id = id, name = legacyName ?? "" });
            dataset = new Dataset { id = Guid.NewGuid().ToString("N"), recordingId = recordingId,
                sourceParticipantId = sourceId, participantId = id, name = "", recordingPath = recordingPath };
            Data.datasets.Add(dataset);
        }
        dataset.recordingPath = recordingPath;
        Attach(dataset, recordingPath);
        return dataset;
    }
    public void Edit(Dataset dataset, string participantId, string name, string reason)
    {
        name = Label(name); reason = Label(reason);
        if (FindParticipant(participantId) == null) throw new ArgumentException("Create or select a participant first.");
        if (dataset.participantId != participantId)
        {
            if (reason.Length == 0) throw new ArgumentException("Add a short reason for changing this dataset's participant ID.");
            dataset.assignments.Add(new Assignment { utc = DateTime.UtcNow.ToString("O"), previousId = dataset.participantId,
                participantId = participantId, reason = reason });
            dataset.participantId = participantId;
        }
        dataset.name = name;
    }
    public void Attach(Dataset dataset, string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Choose an existing file to attach.", path);
        if (!dataset.files.Any(f => f.path == path)) dataset.files.Add(new Attachment { path = path });
    }
    public void AttachFolderFiles(Dataset dataset, string folder)
    {
        // Only explicitly linked run/package folders, never the surrounding archive.
        foreach (string file in Directory.GetFiles(folder).OrderBy(p => p)) Attach(dataset, file);
        string audio = Path.Combine(folder, "audio");
        if (Directory.Exists(audio)) foreach (string file in Directory.GetFiles(audio).OrderBy(p => p)) Attach(dataset, file);
    }
    public void Save()
    {
        if (FilePath == null) throw new InvalidOperationException("Development fixture cannot be saved.");
        string json = JsonUtility.ToJson(Data, true);
        if (json == m_Saved) return;
        // Multiple viewer versions may be open: never overwrite another window's edits.
        string disk = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        if (disk != m_Saved) throw new IOException("Participant catalog changed in another viewer. Close and reopen Dataset to reload it.");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
            else File.Move(temporary, FilePath);
            m_Saved = json;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
