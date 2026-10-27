using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class TrialReviewApp
{
    [Serializable] sealed class IntakeAnswer { public string question, transcript, audio; public float seconds; public bool skipped; }
    [Serializable] sealed class IntakeState
    {
        public string id, participantCode, configuration, neutralGender, phase, prompt, promptAudio, cloneState, cloneVoiceId, cloneError;
        public int neutralOption, promptRevision;
        public float recordedSeconds;
        public IntakeAnswer[] answers;
    }
    [Serializable] sealed class IntakeRequest
    {
        public string action, folder, keysPath, participantCode, configuration, neutralGender, audio;
        public int neutralOption;
        public bool skip;
    }
    [Serializable] sealed class IntakeResult { public bool ok; public string error; }
    sealed class IntakeJob { public Process process; public string result, action; }
    bool m_IntakeOpen, m_IntakeConsent, m_IntakeStarting;
    string m_IntakeFolder, m_IntakeCode = "P001", m_IntakeError, m_IntakeMic, m_PendingAnswer;
    int m_IntakeConfiguration, m_IntakeGender, m_IntakeOption, m_IntakePlayedRevision;
    IntakeState m_Intake;
    IntakeJob m_IntakeJob, m_CloneJob;
    AudioClip m_AnswerClip;
    AudioSource m_InterviewSpeaker;
    Vector2 m_IntakeScroll;
    double m_IntakePoll;
    string IntakeKeys => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/follow-my-voice/api_keys.json");
    void OpenIntake()
    {
        m_IntakeOpen = true;
        if (m_IntakeFolder == null)
        {
            string saved = PlayerPrefs.GetString("ParticipantIntakeFolder", "");
            if (File.Exists(Path.Combine(saved, "session.json"))) m_IntakeFolder = saved;
            m_IntakeCode = m_LiveStatus?.headsetState?.nextParticipantCode ?? "P001";
        }
        ReloadIntake();
    }
    void ReloadIntake()
    {
        if (m_IntakeFolder == null || !File.Exists(Path.Combine(m_IntakeFolder, "session.json"))) return;
        try { m_Intake = JsonUtility.FromJson<IntakeState>(File.ReadAllText(Path.Combine(m_IntakeFolder, "session.json"))); }
        catch (Exception) { m_IntakeError = "Could not read the local interview session."; }
    }
    IntakeJob StartIntakeJob(string action, string audio = null, bool skip = false)
    {
        try
        {
            Directory.CreateDirectory(m_IntakeFolder);
            string request = Path.Combine(m_IntakeFolder, "request-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(request, JsonUtility.ToJson(new IntakeRequest { action = action, folder = m_IntakeFolder,
                keysPath = IntakeKeys, participantCode = m_IntakeCode, configuration = m_IntakeConfiguration == 0 ? "A" : "B",
                neutralGender = m_IntakeGender == 0 ? "Male" : "Female", neutralOption = m_IntakeOption, audio = audio, skip = skip }));
            var process = Process.Start(new ProcessStartInfo("/usr/bin/python3", Argument(Path.Combine(Application.streamingAssetsPath, "participant_intake.py")) + " --request " + Argument(request)) { UseShellExecute = false, CreateNoWindow = true });
            return new IntakeJob { process = process, action = action, result = Path.ChangeExtension(request, ".result.json") };
        }
        catch (Exception e) { m_IntakeError = "Could not start interview worker: " + e.Message; return null; }
    }
    void PollIntake()
    {
        if (m_IntakeStarting || m_AnswerClip != null) return;
        if (Time.realtimeSinceStartupAsDouble < m_IntakePoll) return;
        m_IntakePoll = Time.realtimeSinceStartupAsDouble + .3;
        FinishIntakeJob(ref m_IntakeJob); FinishIntakeJob(ref m_CloneJob);
        if (m_IntakeFolder == null) return;
        ReloadIntake();
        if (m_Intake == null) return;
        if (m_Intake.recordedSeconds >= 30 && m_Intake.cloneState == "not_started" && m_CloneJob == null)
            m_CloneJob = StartIntakeJob("clone");
        if (m_IntakeOpen && m_IntakeJob == null && m_Intake.promptRevision > m_IntakePlayedRevision && !string.IsNullOrEmpty(m_Intake.promptAudio))
        { m_IntakePlayedRevision = m_Intake.promptRevision; StartCoroutine(PlayInterviewPrompt()); }
    }
    void FinishIntakeJob(ref IntakeJob job)
    {
        if (job == null || !job.process.HasExited) return;
        var done = job; job = null; done.process.Dispose(); ReloadIntake();
        try
        {
            var result = JsonUtility.FromJson<IntakeResult>(File.ReadAllText(done.result));
            if (!result.ok) { m_IntakeError = result.error; return; }
            m_IntakeError = null;
            if (done.action == "answer") m_PendingAnswer = null;
            if (done.action == "create")
            { PlayerPrefs.SetString("ParticipantIntakeFolder", m_IntakeFolder); PlayerPrefs.Save(); m_IntakeJob = StartIntakeJob("prompt"); }
        }
        catch (Exception) { m_IntakeError = "Interview worker stopped without a result. Recordings remain saved locally."; }
    }
    IEnumerator PlayInterviewPrompt()
    {
        if (m_AnswerClip != null || m_IntakeStarting || m_Intake == null || string.IsNullOrEmpty(m_Intake.promptAudio)) yield break;
        int revision = m_Intake.promptRevision; string sessionId = m_Intake.id;
        using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(m_IntakeFolder, m_Intake.promptAudio)).AbsoluteUri, AudioType.MPEG))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) { m_IntakeError = "Could not play prompt. Use Retry spoken prompt."; yield break; }
            if (!m_IntakeOpen || m_AnswerClip != null || m_IntakeStarting || m_Intake == null || sessionId != m_Intake.id || revision != m_Intake.promptRevision) yield break;
            if (m_InterviewSpeaker == null) { m_InterviewSpeaker = gameObject.AddComponent<AudioSource>(); m_InterviewSpeaker.ignoreListenerVolume = true; }
            m_InterviewSpeaker.Stop(); if (m_InterviewSpeaker.clip != null) Destroy(m_InterviewSpeaker.clip);
            m_InterviewSpeaker.clip = DownloadHandlerAudioClip.GetContent(request); m_InterviewSpeaker.Play();
        }
    }
    IEnumerator StartAnswer()
    {
        m_IntakeStarting = true; m_InterviewSpeaker?.Stop(); m_Audio?.Stop(); m_Playing = false;
        yield return new WaitForSecondsRealtime(.5f);
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone) || Microphone.devices.Length == 0)
            m_IntakeError = "Enable microphone access for Trial Review in macOS Privacy settings.";
        else if (m_IntakeOpen)
        {
            if (string.IsNullOrEmpty(m_IntakeMic)) m_IntakeMic = Microphone.devices[0];
            m_AnswerClip = Microphone.Start(m_IntakeMic, false, 120, 24000);
            if (m_AnswerClip == null) m_IntakeError = "The microphone could not start.";
        }
        m_IntakeStarting = false;
    }
    void StopAnswer()
    {
        if (m_AnswerClip == null) return;
        int samples = Microphone.GetPosition(m_IntakeMic);
        if (!Microphone.IsRecording(m_IntakeMic)) samples = m_AnswerClip.samples;
        Microphone.End(m_IntakeMic);
        try
        {
            if (samples < 2400) throw new InvalidOperationException("Record an answer before submitting.");
            var pcm = new float[samples * m_AnswerClip.channels]; m_AnswerClip.GetData(pcm, 0);
            string name = "answer-" + Guid.NewGuid().ToString("N") + ".wav";
            File.WriteAllBytes(Path.Combine(m_IntakeFolder, name), TrialReplayAudio.Encode(pcm, m_AnswerClip.channels, m_AnswerClip.frequency));
            m_PendingAnswer = name; m_IntakeJob = StartIntakeJob("answer", name);
        }
        catch (Exception e) { m_IntakeError = e.Message; }
        Destroy(m_AnswerClip); m_AnswerClip = null;
    }
    void StopIntake()
    {
        m_InterviewSpeaker?.Stop();
        if (m_AnswerClip != null) StopAnswer();
        // Provider jobs finish independently and persist results; do not kill an in-flight clone.
        m_IntakeJob?.process.Dispose(); m_CloneJob?.process.Dispose();
    }
    void SendIntake()
    {
        try
        {
            var state = m_LiveStatus.headsetState;
            var assignment = new ParticipantSessionAssignment { id = m_Intake.id, participantCode = m_Intake.participantCode,
                configuration = m_Intake.configuration, neutralGender = m_Intake.neutralGender, neutralOption = m_Intake.neutralOption, cloneVoiceId = m_Intake.cloneVoiceId };
            assignment.Validate();
            var command = new HeadsetCommand { id = Guid.NewGuid().ToString("N"), sessionId = state.sessionId, action = "assign_session",
                observedRevision = state.revision, observedUtc = state.updatedUtc, createdUtc = UtcSeconds, assignment = assignment };
            string path = Path.Combine(m_LiveOutput, "headset-command.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(command));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
            m_HeadsetCommand = command;
        }
        catch (Exception e) { m_IntakeError = "Could not send session: " + e.Message; }
    }
    void DrawIntake(float width, float height)
    {
        if (!m_IntakeOpen) return;
        var rect = new Rect(width / 2 - 420, 40, 840, height - 80); Panel(rect);
        GUILayout.BeginArea(new Rect(rect.x + 24, rect.y + 18, rect.width - 48, rect.height - 36));
        GUILayout.Label("Participant interview · River", m_Title);
        m_IntakeScroll = GUILayout.BeginScrollView(m_IntakeScroll);
        GUILayout.Label("Record participant answers only. Spoken prompts stop before recording begins. Use headphones to avoid speaker spill.", m_Label);
        if (m_Intake == null)
        {
            GUILayout.Label("Participant code", m_Label); m_IntakeCode = GUILayout.TextField(m_IntakeCode, m_Field);
            m_IntakeConfiguration = GUILayout.SelectionGrid(m_IntakeConfiguration, new[] { "A · Self-similar first", "B · Neutral first" }, 2, m_Button);
            m_IntakeGender = GUILayout.SelectionGrid(m_IntakeGender, new[] { "Male neutral voice", "Female neutral voice" }, 2, m_Button);
            m_IntakeOption = GUILayout.SelectionGrid(m_IntakeOption, m_IntakeGender == 0 ? new[] { "Eric", "Roger", "Chris", "Will", "Brian", "Liam" } : new[] { "Janet", "Sarah", "Jessica", "Matilda", "Bella", "Laura" }, 3, m_Button);
            GUI.enabled = m_IntakeJob == null;
            if (Button("Begin interview"))
            {
                m_IntakeFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Trial Review Intake", Guid.NewGuid().ToString("N"));
                m_IntakeJob = StartIntakeJob("create");
            }
            GUI.enabled = true;
        }
        else
        {
            GUILayout.Label(m_Intake.participantCode + " · Configuration " + m_Intake.configuration, m_Heading);
            GUILayout.Label(m_Intake.prompt ?? "", m_Label, GUILayout.MinHeight(80));
            GUILayout.Label($"Answer audio: {m_Intake.recordedSeconds:0} seconds · Clone: {m_Intake.cloneState}", m_Label);
            if (!string.IsNullOrEmpty(m_Intake.cloneError)) GUILayout.Label(m_Intake.cloneError, m_Label);
            m_IntakeConsent = GUILayout.Toggle(m_IntakeConsent, "Participant agreed to recording and voice cloning");
            bool busy = m_IntakeJob != null || m_IntakeStarting;
            if (m_Intake.phase == "interview")
            {
                if (m_AnswerClip == null && !busy)
                {
                    foreach (string device in Microphone.devices)
                        if (GUILayout.Toggle((m_IntakeMic ?? (Microphone.devices.Length > 0 ? Microphone.devices[0] : "")) == device, device)) m_IntakeMic = device;
                }
                GUI.enabled = !busy && (m_AnswerClip != null || m_IntakeConsent);
                if (Button(m_AnswerClip != null ? "Stop and submit answer" : "Record participant answer"))
                { if (m_AnswerClip != null) StopAnswer(); else StartCoroutine(StartAnswer()); }
                GUI.enabled = !busy && m_AnswerClip == null;
                if (Button("Skip this question")) { m_InterviewSpeaker?.Stop(); m_IntakeJob = StartIntakeJob("answer", skip: true); }
                if (Button("Retry spoken prompt")) m_IntakeJob = StartIntakeJob("prompt");
                if (m_PendingAnswer != null && Button("Retry saved answer")) m_IntakeJob = StartIntakeJob("answer", m_PendingAnswer);
                GUI.enabled = true;
            }
            if (busy) GUILayout.Label("Preparing the next question…", m_MutedLabel);
            if (m_Intake.answers != null && m_Intake.answers.Length > 0)
                GUILayout.Label("Last answer: " + m_Intake.answers[m_Intake.answers.Length - 1].transcript, m_Label);
            GUI.enabled = m_CloneJob == null && m_Intake.cloneState == "not_started" && m_Intake.recordedSeconds > 0;
            if (Button("Prepare voice from recorded answers")) m_CloneJob = StartIntakeJob("clone");
            GUI.enabled = m_CloneJob == null && m_Intake.cloneState == "uncertain";
            if (Button("Retry failed cloning (may create a duplicate provider voice)")) m_CloneJob = StartIntakeJob("retry_clone");
            var headset = m_LiveStatus?.headsetState;
            bool fresh = TrialReviewHeadsetState.Fresh(headset, ReceiverConnected, UtcSeconds, m_LiveStatus?.headsetStateObservedUtc ?? 0);
            GUI.enabled = m_FollowLive && !m_DetachedReview && fresh && (headset.intakeAvailable || headset.participantSessionId == m_Intake.id) && m_Intake.phase == "interview_complete" && m_Intake.cloneState == "ready";
            if (Button("Send session to headset for confirmation")) SendIntake();
            GUI.enabled = true;
            GUILayout.Label(fresh && headset.participantSessionId == m_Intake.id ? "Headset: " + headset.assignmentState : "Headset must be connected and waiting for a participant.", m_MutedLabel);
            GUI.enabled = !busy && m_CloneJob == null && m_AnswerClip == null;
            if (Button("Finish here / create another session")) { m_InterviewSpeaker?.Stop(); m_Intake = null; m_IntakeFolder = null; m_IntakePlayedRevision = 0; m_IntakeConsent = false; m_PendingAnswer = null; PlayerPrefs.DeleteKey("ParticipantIntakeFolder"); m_IntakeCode = headset?.nextParticipantCode ?? "P001"; }
            GUI.enabled = true;
        }
        if (!File.Exists(IntakeKeys)) GUILayout.Label("API configuration missing: " + IntakeKeys, m_Label);
        if (!string.IsNullOrEmpty(m_IntakeError)) GUILayout.Label(m_IntakeError, m_Label);
        GUILayout.EndScrollView();
        GUI.enabled = m_AnswerClip == null && !m_IntakeStarting;
        if (Button("Close interview panel")) { m_IntakeOpen = false; m_InterviewSpeaker?.Stop(); }
        GUI.enabled = true; GUILayout.EndArea();
    }
}
