using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class TrialReviewApp
{
    [Serializable] sealed class FinalSurveyHandoff
    {
        public string participantCode, desktopSessionId, runFolder, status, surveyUrl, surveyVersion, handoffId;
        public bool studyData, responsesCollected;
    }
    FinalSurveyHandoff m_FinalSurvey;
    string m_FinalSurveyMessage;
    void DrawFinalSurveyHandoff()
    {
        if (Button("Find this participant's completed-run survey handoff"))
        {
            m_FinalSurvey=null;m_FinalSurveyMessage="No completed-run handoff received yet. Keep Wi-Fi receiving after the last trial.";
            try
            {
                string archive=m_LiveStatus?.archive;
                if (!string.IsNullOrEmpty(archive) && Directory.Exists(archive))
                {
                    var files=Directory.GetFiles(archive,"final-survey-handoff-*.json",SearchOption.AllDirectories);
                    Array.Sort(files,(a,b)=>File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                    foreach(string file in files)
                    {
                        var handoff=JsonUtility.FromJson<FinalSurveyHandoff>(File.ReadAllText(file));
                        if(handoff.desktopSessionId==m_Intake.id && handoff.participantCode==m_Intake.participantCode)
                        {m_FinalSurvey=handoff;m_FinalSurveyMessage=null;break;}
                    }
                }
            }
            catch(Exception e) {m_FinalSurveyMessage="Survey handoff unavailable: "+e.Message;}
        }
        if(m_FinalSurvey?.desktopSessionId==m_Intake.id)
        {
            GUILayout.Label($"Final survey · {m_FinalSurvey.participantCode} · {m_FinalSurvey.runFolder}\n{m_FinalSurvey.status} · {m_FinalSurvey.surveyVersion}\nSurvey draft must be published by the study team before use. This viewer does not record survey completion.",m_Label);
            if(!m_FinalSurvey.studyData) GUILayout.Label("Development rehearsal: do not submit participant responses.",m_Label);
            GUILayout.TextArea(m_FinalSurvey.surveyUrl ?? "",m_Field);
            if(Button("Copy participant survey link")) GUIUtility.systemCopyBuffer=m_FinalSurvey.surveyUrl ?? "";
        }
        else if(!string.IsNullOrEmpty(m_FinalSurveyMessage)) GUILayout.Label(m_FinalSurveyMessage,m_MutedLabel);
    }
    bool Consented => m_Intake != null && m_Intake.protocol == "audio-intake-v2-20261028" &&
        m_Intake.withdrawnUtc == 0 && m_Intake.consent != null && m_Intake.consent.study && m_Intake.consent.voiceCloud &&
        m_Intake.consent.sessionId == m_Intake.id && m_Intake.consent.participantCode == m_Intake.participantCode;
    bool AudioIntakeComplete => m_Intake != null && (m_Intake.phase == "eligibility_review" ||
        m_Intake.phase == "screening_stopped" || m_Intake.phase == "interview_complete");
    bool ReviewedIntake => Consented && m_Intake.phase == "interview_complete" && m_Intake.eligible &&
        !string.IsNullOrEmpty(m_Intake.eligibility?.id) && m_Intake.answers != null && m_Intake.answers.Length >= 9;
    bool CanCloneIntake => ReviewedIntake && m_Intake.speechSeconds >= 30 && m_Intake.sampleWords >= 50;
    bool CanCacheIntake => CanCloneIntake && m_Intake.cloneState == "ready";

    void DrawIntakeConsentAndReview()
    {
        bool idle = m_IntakeJob == null && m_IntakeAudioJob == null && !m_IntakeStarting && m_AnswerClip == null && !m_PromptPlaying;
        if (m_Intake.protocol != "audio-intake-v2-20261028")
        { GUILayout.Label("Historical intake: retained read-only. Start a new consented session with an unused code.", m_Label); return; }
        if (m_Intake.phase == "awaiting_consent")
        {
            GUILayout.Label("Researcher initials", m_Label); m_ConsentOperator = GUILayout.TextField(m_ConsentOperator, m_Field);
            GUILayout.Label("Signed consent document/version or record reference (no participant name)", m_Label);
            m_ConsentReference = GUILayout.TextField(m_ConsentReference, m_Field);
            m_StudyConsent = IntakeCheck(m_StudyConsent,"Signed study consent is recorded for this participant");
            m_IntakeConsent = IntakeCheck(m_IntakeConsent,"Separate voice authorization covers recording, ElevenLabs transcription/cloning and synthesis");
            GUI.enabled = idle && m_StudyConsent && m_IntakeConsent && !string.IsNullOrWhiteSpace(m_ConsentOperator) && !string.IsNullOrWhiteSpace(m_ConsentReference);
            if (PrimaryButton("Record consent receipt")) m_IntakeJob = StartIntakeJob("consent");
            GUI.enabled = true;
        }
        if (m_Intake.phase == "select_voice")
        {
            GUILayout.Label("Participant chooses the Selected Voice", m_Heading);
            m_IntakeGender = GUILayout.SelectionGrid(m_IntakeGender,new[] {"Male voices","Female voices"},2,m_Button);
            m_IntakeOption = GUILayout.SelectionGrid(m_IntakeOption,m_IntakeGender == 0 ?
                new[] {"Eric","Roger","Chris","Will","Brian","Liam"} : new[] {"Janet","Sarah","Jessica","Matilda","Bella","Laura"},3,m_Button);
            GUI.enabled = idle && Consented;
            if (Button("Listen to this voice")) StartParticipantAudio("preview");
            GUI.enabled = idle && Consented && !string.IsNullOrEmpty(m_Intake.previewAudio) &&
                m_PreviewPlayed == m_Intake.previewRevision && m_Intake.neutralOption == m_IntakeOption &&
                m_Intake.neutralGender == (m_IntakeGender == 0 ? "Male" : "Female");
            if (PrimaryButton("Participant confirms Selected Voice / begin intake")) m_IntakeJob = StartIntakeJob("select_voice");
            GUI.enabled = true;
        }
        if (m_Intake.phase == "eligibility_review")
        {
            GUILayout.Label("Researcher: review each answer. No / unclear / skipped prevents enrollment.",m_Heading);
            if (m_Intake.answers != null)
                for (int i=0; i<Math.Min(4,m_Intake.answers.Length); i++)
                {
                    if(i%2==0)GUILayout.BeginHorizontal();
                    if(i%2==1)GUILayout.Space(16);
                    GUILayout.BeginVertical(new GUIStyle(m_Card){normal={background=m_ButtonTexture}},GUILayout.Width(448),GUILayout.MinHeight(168));
                    var answer = m_Intake.answers[i];
                    GUILayout.Label(answer.question+"\n"+(answer.skipped ? "SKIPPED" : answer.transcript),m_Label,GUILayout.MinHeight(60));
                    GUI.enabled = idle;
                    if (!answer.skipped && Button("Listen to saved answer "+(i+1))) StartCoroutine(PlayIntakeFile(answer.audio,AudioType.WAV));
                    m_Eligibility[i] = GUILayout.SelectionGrid(m_Eligibility[i],new[] {"Unclear","Yes","No"},3,m_Button);
                    GUILayout.EndVertical();
                    if(i%2==1||i==Math.Min(4,m_Intake.answers.Length)-1)GUILayout.EndHorizontal();
                }
            GUILayout.Label("Reviewing researcher initials",m_Label); m_ConsentOperator=GUILayout.TextField(m_ConsentOperator,m_Field);
            GUI.enabled = idle && Consented && !string.IsNullOrWhiteSpace(m_ConsentOperator);
            if (PrimaryButton("Save eligibility review")) m_IntakeJob=StartIntakeJob("review");
            GUI.enabled = true;
        }
        if (m_Intake.phase == "screening_stopped") GUILayout.Label("Screening stopped. Do not proceed to cloning or trials. Retain the answers and follow the approved study procedure.",m_Label);
        if (m_Intake.phase == "withdrawn") GUILayout.Label("Consent withdrawn: capture and further preparation are disabled. Existing records are retained for the approved handling procedure.",m_Label);
    }
    void DrawIntakeWithdrawal()
    {
        GUI.enabled = m_IntakeJob == null && !m_IntakeStarting && Consented;
        if (WithdrawalButton("Stop intake / record withdrawal (no further answer upload)"))
        {
            m_InterviewSpeaker?.Stop();
            if (m_AnswerClip != null) StopAnswer(false);
            m_IntakeJob = StartIntakeJob("withdraw");
        }
        GUI.enabled = true;
    }

    IEnumerator PlayIntakeFile(string name, AudioType type)
    {
        if(m_CaptureMode) yield break;
        m_PromptPlaying = true;
        string session = m_Intake.id;
        using (var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(m_IntakeFolder,name)).AbsoluteUri,type))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success && m_IntakeOpen && m_Intake?.id == session && m_AnswerClip == null)
            {
                if (m_InterviewSpeaker == null) { m_InterviewSpeaker=gameObject.AddComponent<AudioSource>(); m_InterviewSpeaker.ignoreListenerVolume=true; }
                m_InterviewSpeaker.Stop(); if (m_InterviewSpeaker.clip != null) Destroy(m_InterviewSpeaker.clip);
                m_InterviewSpeaker.clip=DownloadHandlerAudioClip.GetContent(request); m_InterviewSpeaker.Play();
                while (m_InterviewSpeaker != null && m_InterviewSpeaker.isPlaying) yield return null;
            }
            else m_IntakeError="Could not play saved intake audio.";
        }
        m_PromptPlaying=false;
    }

    void StartParticipantAudio(string action)
    {
        if(m_CaptureMode) return;
        if (m_IntakeAudioJob != null || !Consented) return;
        try
        {
            m_IntakeAudioAction=action;
            string arguments=Argument(Path.Combine(Application.streamingAssetsPath,"participant_audio.py"))+
                " --folder "+Argument(m_IntakeFolder)+" --endpoint "+Argument(m_Endpoint.Trim());
            if (action=="transfer") arguments+=" --transfer";
            if (action=="preview") arguments+=" --preview --gender "+(m_IntakeGender==0 ? "Male" : "Female")+" --option "+m_IntakeOption;
            m_IntakeAudioJob=Process.Start(new ProcessStartInfo("/usr/bin/python3",arguments) {UseShellExecute=false,CreateNoWindow=true});
        }
        catch (Exception e) { m_IntakeError="Could not start participant audio worker: "+e.Message; }
    }

    void PollParticipantAudio()
    {
        if(m_CaptureMode) return;
        if (m_IntakeAudioJob == null || !m_IntakeAudioJob.HasExited) return;
        int code=m_IntakeAudioJob.ExitCode; m_IntakeAudioJob.Dispose(); m_IntakeAudioJob=null;
        ReloadIntake();
        if (code!=0) { m_IntakeError="Participant audio stopped: "+m_Intake.cacheError+". Recordings and completed clips are preserved."; return; }
        m_IntakeError=null;
        if (m_IntakeAudioAction=="preview" && m_IntakeOpen && !string.IsNullOrEmpty(m_Intake.previewAudio))
            StartCoroutine(PlaySelectedPreview());
    }
    IEnumerator PlaySelectedPreview()
    {
        double revision=m_Intake.previewRevision;
        yield return PlayIntakeFile(m_Intake.previewAudio,AudioType.MPEG);
        if (string.IsNullOrEmpty(m_IntakeError) && m_IntakeOpen) m_PreviewPlayed=revision;
    }

    void DrawParticipantAudio()
    {
        GUILayout.Label($"Study audio: {m_Intake.cacheState} · {m_Intake.cacheCompleted}/{m_Intake.cacheTotal}",m_Label);
        if (!string.IsNullOrEmpty(m_Intake.cacheError)) IntakeErrorCard(m_Intake.cacheError);
        bool idle=m_IntakeAudioJob==null && m_IntakeJob==null && m_AnswerClip==null && !m_IntakeStarting;
        GUI.enabled=idle && CanCacheIntake;
        if (Button("Prepare / resume participant audio cache")) StartParticipantAudio("prepare");
        GUI.enabled=idle && Consented && (m_Intake.cacheState=="ready" || m_Intake.cacheState=="transferred") && m_Intake.phase=="interview_complete";
        if (m_Intake.cacheState=="ready" ? PrimaryButton("Transfer and verify study audio on headset") : Button("Transfer and verify study audio on headset")) StartParticipantAudio("transfer");
        GUI.enabled=true;
        if (m_IntakeAudioJob != null) GUILayout.Label("Audio worker: "+m_IntakeAudioAction+"…",m_MutedLabel);
    }
}
