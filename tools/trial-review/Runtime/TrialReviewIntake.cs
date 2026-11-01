using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class TrialReviewApp
{
    [Serializable] sealed class IntakeAnswer
    {
        public string id, question, transcript, machineTranscript, audio, topic, transcriptionStatus, transcriptionError, structuredValue;
        public float seconds; public bool skipped, researcherCorrected, possibleIdentifiers;
    }
    [Serializable] sealed class IntakeConsent { public string id, participantCode, sessionId; public bool study, voiceCloud; }
    [Serializable] sealed class IntakeEligibility { public string id; }
    [Serializable] sealed class IntakeCapture { public string prompt, consentId; }
    [Serializable] sealed class IntakeState
    {
        public string id, participantCode, configuration, neutralGender, phase, prompt, promptAudio, cloneState, cloneVoiceId, cloneError;
        public string cacheState, cacheError, cacheManifestSha256, previewAudio, protocol, cleanupState, cleanupError;
        public int cacheCompleted, cacheTotal;
        public bool eligible, selectedVoiceConfirmed, development;
        public double withdrawnUtc, previewRevision;
        public IntakeConsent consent;
        public IntakeEligibility eligibility;
        public int neutralOption, promptRevision, questionIndex;
        public float recordedSeconds, speechSeconds;
        public int sampleWords;
        public IntakeAnswer[] answers;
        public ParticipantFitMeasurements fit;
    }
    [Serializable] sealed class IntakeRequest
    {
        public string action, folder, keysPath, participantCode, configuration, neutralGender, audio;
        public int neutralOption;
        public bool skip, saveCorrection;
        public bool study, voiceCloud, development;
        public string @operator, reference;
        public string answerId, transcript, structuredValue;
        public string[] eligibilityValues;
        public ParticipantFitMeasurements fit;
        public string catalog, headsetSuggestion, participantSessionId;
        public string[] archives, knownCodes;
    }
    [Serializable] sealed class IntakeResult { public bool ok, duplicate; public string error, suggested; }
    sealed class IntakeJob { public Process process; public string result, action; }
    bool m_IntakeOpen, m_IntakeConsent, m_IntakeStarting;
    bool m_StudyConsent, m_IntakeDevelopment, m_PromptPlaying;
    string m_ConsentOperator = "", m_ConsentReference = "", m_CaptureName;
    readonly int[] m_Eligibility = { 0, 0, 0, 0 };
    static readonly string[] EligibilityValues = { "unclear", "yes", "no" };
    Process m_IntakeAudioJob;
    string m_IntakeAudioAction;
    double m_PreviewPlayed;
    string m_IntakeFolder, m_IntakeCode = "P001", m_IntakeError, m_IntakeMic, m_PendingAnswer;
    int m_IntakeConfiguration, m_IntakeGender, m_IntakeOption, m_IntakePlayedRevision, m_QuestionHeardRevision;
    IntakeState m_Intake;
    IntakeJob m_IntakeJob, m_CloneJob;
    AudioClip m_AnswerClip;
    AudioSource m_InterviewSpeaker;
    Vector2 m_IntakeScroll;
    double m_IntakePoll;
    string m_IntakeIpd = "", m_IntakeHeadsetIpd = "";
    int m_IntakeIpdSource;
    static readonly string[] IpdSources = { "not_recorded", "pupillometer", "ruler", "participant_report" };
    ParticipantFitMeasurements ReadIntakeFit()
    {
        var fit = new ParticipantFitMeasurements { ipdRecorded = !string.IsNullOrWhiteSpace(m_IntakeIpd),
            headsetIpdRecorded = !string.IsNullOrWhiteSpace(m_IntakeHeadsetIpd), ipdSource = IpdSources[m_IntakeIpdSource] };
        if (fit.ipdRecorded && !float.TryParse(m_IntakeIpd, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out fit.participantIpdMm))
            throw new ArgumentException("Enter participant IPD in millimetres, using a decimal point.");
        if (fit.headsetIpdRecorded && !float.TryParse(m_IntakeHeadsetIpd, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out fit.headsetIpdMm))
            throw new ArgumentException("Enter headset lens spacing in millimetres, using a decimal point.");
        fit.Validate(); return fit;
    }
    string IntakeKeys => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/follow-my-voice/api_keys.json");
    void OpenIntake()
    {
        if(m_CaptureMode) { m_IntakeOpen=true; return; }
        m_IntakeOpen = true;
        if(!m_DetachedReview && !ReceiverRunning)ConnectLive();
        m_IntakePaused = true;
        if (m_IntakeFolder == null)
        {
            string saved = PlayerPrefs.GetString("ParticipantIntakeFolder", "");
            if (File.Exists(Path.Combine(saved, "session.json"))) m_IntakeFolder = saved;
            RefreshParticipantCode(true);
        }
        ReloadIntake();
        if (Consented && m_PendingAnswer == null && (m_Intake.phase == "interview" || m_Intake.phase == "fallback"))
        {
            foreach (string path in Directory.GetFiles(m_IntakeFolder,"answer-*.wav"))
            {
                if (!File.Exists(path+".capture.json")) continue;
                if (File.Exists(path+".end.json") && File.ReadAllText(path+".end.json").Contains("no_speech")) continue;
                var capture=JsonUtility.FromJson<IntakeCapture>(File.ReadAllText(path+".capture.json"));
                string name=Path.GetFileName(path);
                if (capture.prompt==m_Intake.prompt && capture.consentId==m_Intake.consent.id &&
                    (m_Intake.answers==null || !Array.Exists(m_Intake.answers,x=>x.audio==name))) m_PendingAnswer=name;
            }
        }
    }
    void ReloadIntake()
    {
        if(m_CaptureMode) return;
        if (m_IntakeFolder == null || !File.Exists(Path.Combine(m_IntakeFolder, "session.json"))) return;
        try { m_Intake = JsonUtility.FromJson<IntakeState>(File.ReadAllText(Path.Combine(m_IntakeFolder, "session.json"))); }
        catch (Exception) { m_IntakeError = "Could not read the local interview session."; }
    }
    IntakeJob StartIntakeJob(string action, string audio = null, bool skip = false)
    {
        if(m_CaptureMode) return null;
        try
        {
            Directory.CreateDirectory(m_IntakeFolder);
            string request = Path.Combine(m_IntakeFolder, "request-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(request, JsonUtility.ToJson(new IntakeRequest { action = action, folder = m_IntakeFolder,
                keysPath = IntakeKeys, participantCode = action == "sync_fit" ? m_Intake.participantCode : m_IntakeCode, configuration = m_IntakeConfiguration == 0 ? "A" : "B",
                neutralGender = m_IntakeGender == 0 ? "Male" : "Female", neutralOption = m_IntakeOption, audio = audio, skip = skip,
                study = m_StudyConsent, voiceCloud = m_IntakeConsent, development = m_IntakeDevelopment,
                @operator = m_ConsentOperator, reference = m_ConsentReference,
                answerId=m_EditAnswerId, transcript=m_EditTranscript, structuredValue=m_EditCategory,
                saveCorrection=action=="review" && TranscriptEditPending,
                eligibilityValues = Array.ConvertAll(m_Eligibility, x => EligibilityValues[x]),
                fit = action == "sync_fit" ? m_LiveStatus?.headsetState?.fit : null,
                participantSessionId = m_Intake?.id,
                catalog = CatalogPath, archives = NumberingArchives, knownCodes = new[]{m_LiveStatus?.headsetState?.participantCode} }));
            var process = Process.Start(new ProcessStartInfo("/usr/bin/python3", Argument(Path.Combine(Application.streamingAssetsPath, "participant_intake.py")) + " --request " + Argument(request)) { UseShellExecute = false, CreateNoWindow = true });
            return new IntakeJob { process = process, action = action, result = Path.ChangeExtension(request, ".result.json") };
        }
        catch (Exception e) { m_IntakePaused=true; m_IntakeError = "Could not start interview worker: " + e.Message; return null; }
    }
    void PollIntake()
    {
        if(m_CaptureMode) return;
        PollParticipantCodes();
        PollIntakeCapture();
        if (m_IntakeStarting || m_AnswerClip != null) return;
        if (Time.realtimeSinceStartupAsDouble < m_IntakePoll) return;
        m_IntakePoll = Time.realtimeSinceStartupAsDouble + .3;
        FinishIntakeJob(ref m_IntakeJob); FinishIntakeJob(ref m_CloneJob);
        if (m_IntakeFolder == null) return;
        ReloadIntake();
        if (m_Intake == null) return;
        SyncHeadsetFit();
        PollParticipantAudio();
        if((m_Intake.phase=="screening_stopped" || m_Intake.withdrawnUtc>0) &&
            !string.IsNullOrEmpty(m_Intake.cloneVoiceId) && m_Intake.cleanupState!="deleted" &&
            string.IsNullOrEmpty(m_Intake.cleanupError) && m_IntakeJob==null && m_CloneJob==null && m_WithdrawalJob==null)
            m_IntakeJob=StartIntakeJob("cleanup_clone");
        if (CanCloneIntake && m_Intake.cloneState == "not_started" && m_CloneJob == null)
            m_CloneJob = StartIntakeJob("clone");
        if (CanCacheIntake && m_Intake.cacheState == "not_started" && string.IsNullOrEmpty(m_Intake.cacheError) && m_IntakeAudioJob == null)
            StartParticipantAudio("prepare");
        AdvanceAutomaticIntake();
    }
    void FinishIntakeJob(ref IntakeJob job)
    {
        if (job == null || !job.process.HasExited) return;
        var done = job; job = null; done.process.Dispose(); ReloadIntake();
        try
        {
            var result = JsonUtility.FromJson<IntakeResult>(File.ReadAllText(done.result));
            if (!result.ok) { m_IntakePaused=true; m_IntakeError = "Paused: "+result.error; return; }
            m_IntakeError = null;
            if (done.action == "answer") m_PendingAnswer = null;
            if (done.action == "create")
            { Array.Clear(m_Eligibility,0,m_Eligibility.Length); PlayerPrefs.SetString("ParticipantIntakeFolder", m_IntakeFolder); PlayerPrefs.Save(); }
            if (done.action == "authorize_recording" && AutomaticCaptureAllowed) StartCoroutine(StartAnswer());
            if (done.action == "select_voice" || done.action == "fallback")
            { m_IntakeAutomatic=true; m_IntakePaused=false; m_IntakePlayedRevision=0; }
            if(done.action=="correct_transcript"||done.action=="retry_transcript")m_EditAnswerId=null;
            if (done.action == "ticket") SendIntake();
        }
        catch (Exception) { m_IntakePaused=true; m_IntakeError = "Paused: interview worker stopped without a result. Recordings remain saved locally."; }
    }
    IEnumerator PlayInterviewPrompt()
    {
        if (m_AnswerClip != null || m_IntakeStarting || m_Intake == null || string.IsNullOrEmpty(m_Intake.promptAudio)) yield break;
        int revision = m_Intake.promptRevision, epoch=m_IntakeEpoch; string sessionId = m_Intake.id;
        m_IntakeError = null;
        yield return PlayIntakeFile(m_Intake.promptAudio,AudioType.MPEG);
        if (AutomaticCaptureAllowed && epoch==m_IntakeEpoch && m_Intake?.id == sessionId && m_Intake.promptRevision == revision && string.IsNullOrEmpty(m_IntakeError))
        {
            m_QuestionHeardRevision = revision;
            m_CaptureName="answer-"+Guid.NewGuid().ToString("N")+".wav";
            m_IntakeJob=StartIntakeJob("authorize_recording",m_CaptureName);
        }
        else if(!string.IsNullOrEmpty(m_IntakeError))m_IntakePaused=true;
    }
    IEnumerator StartAnswer()
    {
        if(m_CaptureMode) yield break;
        if (!AutomaticCaptureAllowed || m_PromptPlaying || m_QuestionHeardRevision != m_Intake.promptRevision || string.IsNullOrEmpty(m_CaptureName)) yield break;
        int epoch=m_IntakeEpoch;
        m_IntakeStarting = true; m_InterviewSpeaker?.Stop(); m_Audio?.Stop(); m_Playing = false;
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone) || Microphone.devices.Length == 0)
            m_IntakeError = "Enable microphone access for Trial Review in macOS Privacy settings.";
        else if (AutomaticCaptureAllowed && epoch==m_IntakeEpoch)
        {
            if (m_InterviewSpeaker == null) { m_InterviewSpeaker = gameObject.AddComponent<AudioSource>(); m_InterviewSpeaker.ignoreListenerVolume = true; }
            var tone = AudioClip.Create("Intake speak cue", 4320, 1, 24000, false);
            var cue = new float[4320];
            for (int i=0; i<cue.Length; i++) cue[i] = .18f * Mathf.Sin(2*Mathf.PI*880*i/24000) * Mathf.Sin(Mathf.PI*i/(cue.Length-1));
            tone.SetData(cue, 0); m_InterviewSpeaker.PlayOneShot(tone);
            yield return new WaitForSecondsRealtime(.25f); Destroy(tone);
            // Cue and prompt are excluded from the participant-only sample.
            if (!AutomaticCaptureAllowed || epoch!=m_IntakeEpoch) { m_IntakeStarting = false; yield break; }
            if (string.IsNullOrEmpty(m_IntakeMic)) m_IntakeMic = Microphone.devices[0];
            m_AnswerClip = Microphone.Start(m_IntakeMic, false, 120, 24000);
            m_VoiceActivity=new TrialReviewVoiceActivity(m_Intake.questionIndex>=4);m_ActivityOffset=0;
            if(m_NoSpeechIndex!=m_Intake.questionIndex){m_NoSpeechIndex=m_Intake.questionIndex;m_NoSpeechRepeats=0;}
            if (m_AnswerClip == null) m_IntakeError = "The microphone could not start.";
        }
        m_IntakeStarting = false;
        if(!string.IsNullOrEmpty(m_IntakeError))m_IntakePaused=true;
    }
    void StopAnswer(bool submit = true)
    {
        if (m_AnswerClip == null) return;
        int samples = Microphone.GetPosition(m_IntakeMic);
        if (!Microphone.IsRecording(m_IntakeMic)) samples = m_AnswerClip.samples;
        Microphone.End(m_IntakeMic);
        try
        {
            if (samples == 0) throw new InvalidOperationException("No microphone samples were received. Intake paused.");
            var pcm = new float[samples * m_AnswerClip.channels]; m_AnswerClip.GetData(pcm, 0);
            string name = m_CaptureName;
            if (m_AnswerClip.channels > 1)
            {
                using (var raw = new FileStream(Path.Combine(m_IntakeFolder,name+".source.wav"),FileMode.CreateNew))
                { var bytes=TrialReplayAudio.Encode(pcm,m_AnswerClip.channels,m_AnswerClip.frequency); raw.Write(bytes,0,bytes.Length); }
                var mono=new float[samples];
                for (int i=0; i<samples; i++)
                    for (int channel=0; channel<m_AnswerClip.channels; channel++) mono[i]+=pcm[i*m_AnswerClip.channels+channel]/m_AnswerClip.channels;
                pcm=mono;
            }
            using (var output = new FileStream(Path.Combine(m_IntakeFolder, name), FileMode.CreateNew))
            { var data = TrialReplayAudio.Encode(pcm, 1, m_AnswerClip.frequency); output.Write(data,0,data.Length); }
            m_PendingAnswer = samples>=2400?name:null;
            if (submit && m_PendingAnswer!=null) m_IntakeJob = StartIntakeJob("answer", name);
        }
        catch (Exception e) { m_IntakePaused=true; m_IntakeError = e.Message; }
        Destroy(m_AnswerClip); m_AnswerClip = null;
    }
    void StopIntake()
    {
        PauseAutomaticIntake();
        // Provider jobs finish independently and persist results; do not kill an in-flight clone.
        m_IntakeJob?.process.Dispose(); m_CloneJob?.process.Dispose();
    }
    void SendIntake()
    {
        if(m_CaptureMode) return;
        try
        {
            var state = m_LiveStatus.headsetState;
            if (!Consented) throw new InvalidOperationException("Consent is not recorded for this session.");
            var assignment = JsonUtility.FromJson<ParticipantSessionAssignment>(File.ReadAllText(Path.Combine(m_IntakeFolder,"assignment.json")));
            if (assignment.id != m_Intake.id) throw new InvalidOperationException("Session changed before handoff.");
            assignment.Validate();
            var command = new HeadsetCommand { id = Guid.NewGuid().ToString("N"), sessionId = state.sessionId, action = "assign_session",
                observedRevision = state.revision, observedUtc = state.updatedUtc, createdUtc = UtcSeconds, assignment = assignment };
            string path = Path.Combine(m_LiveOutput, "headset-command.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(command));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
            m_HeadsetCommand = command;
            m_FollowLive=true;m_JoinLive=true;m_LiveSignalActive=false;
        }
        catch (Exception e) { m_IntakeError = "Could not send session: " + e.Message; }
    }
    bool m_MicMenu;
    string m_IntakeRenderedPhase;
    int IntakeStep
    {
        get
        {
            if(m_Intake==null)return 0;
            switch(m_Intake.phase)
            {
                case "awaiting_consent":return 1;
                case "select_voice":return 2;
                case "interview":case "fallback":case "fallback_available":return 3;
                case "eligibility_review":case "screening_stopped":return 4;
                case "interview_complete":return 5;
                default:return -1;
            }
        }
    }
    bool PrimaryButton(string label,params GUILayoutOption[] options)=>GUILayout.Button(HelpContent(label),m_ActiveChip,options);
    void IntakeStepper()
    {
        string[] steps={"Reserve","Consent","Selected Voice","Audio intake","Eligibility review","Send to headset"};
        bool[] complete={m_Intake!=null,Consented,m_Intake?.selectedVoiceConfirmed==true,AudioIntakeComplete,
            m_Intake?.eligible==true,false};
        GUILayout.BeginHorizontal();
        for(int i=0;i<steps.Length;i++)
        {
            var style=new GUIStyle(i==IntakeStep?m_ActiveChip:m_Chip){wordWrap=true,fontSize=12,alignment=TextAnchor.MiddleCenter};
            if(i!=IntakeStep)style.normal.textColor=complete[i]?m_Ok:m_MutedLabel.normal.textColor;
            GUILayout.Label((complete[i]&&i!=IntakeStep?"✓":(i+1).ToString())+"\n"+steps[i],style,GUILayout.Height(52),GUILayout.ExpandWidth(true));
        }
        GUILayout.EndHorizontal();GUILayout.Space(16);
    }
    void DrawIntake(float width, float height)
    {
        if (!m_IntakeOpen) return;
        Fill(new Rect(0,0,width,height),new Color(0,0,0,.70f));
        string phase=m_Intake?.phase??"reserve";
        if(m_IntakeRenderedPhase!=phase){m_IntakeRenderedPhase=phase;m_IntakeScroll=Vector2.zero;m_MicMenu=false;}
        float monitorSpace=SessionMonitorVisible?178:0;
        var rect = new Rect(width / 2 - 520, 40+monitorSpace, 1040, height - 80-monitorSpace);
        Fill(rect,m_Border);Panel(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2));
        GUILayout.BeginArea(new Rect(rect.x + 24, rect.y + 18, rect.width - 48, rect.height - 36));
        GUILayout.Label("Audio intake · River",m_Heading);
        GUILayout.BeginHorizontal();
        GUILayout.Label(m_Intake==null?"New participant session":m_Intake.participantCode+" · Configuration "+m_Intake.configuration+(m_Intake.configuration=="A"?" (Self-Similar first)":" (Selected Voice first)"),m_Title);
        if(m_Intake?.development==true||m_Intake==null&&m_IntakeDevelopment)
        {var ribbon=GUILayoutUtility.GetRect(144,28,GUILayout.Width(144));StatusChip(ribbon,"DEVELOPMENT",m_Warn,true);}
        GUILayout.EndHorizontal();GUILayout.Space(16);IntakeStepper();
        m_IntakeScroll=GUILayout.BeginScrollView(m_IntakeScroll);
        if(phase=="interview"||phase=="fallback")
        {
            GUILayout.Label("Automatic interview: listen, then speak after the tone. Capture ends after 3 seconds of silence for eligibility or 5 seconds for open answers. Use headphones to prevent speaker spill.",m_MutedLabel);
            GUILayout.Space(12);
        }
        if(m_Intake==null)DrawIntakeReserve();
        else
        {
            bool busy=m_IntakeJob!=null||m_IntakeStarting||m_CaptureMode&&m_CaptureIntakeActivity=="Submitting";
            if(phase!="interview"&&phase!="fallback"&&phase!="fallback_available"&&phase!="interview_complete"||m_Intake.protocol!=IntakeProtocol)
            {GUILayout.BeginVertical(m_Card);DrawIntakeConsentAndReview();GUILayout.EndVertical();}
            if((phase=="interview"||phase=="fallback") && m_Intake.protocol==IntakeProtocol)DrawIntakeQuestion(busy);
            if(phase=="fallback_available" && m_Intake.protocol==IntakeProtocol)
            {
                GUILayout.Label("All nine questions are complete. Speech is below the cloning threshold. Read the optional fallback, or continue to researcher review; cloning stays disabled below the threshold.",m_Label);
                GUI.enabled=!busy&&Consented;
                if(PrimaryButton("Read fallback passage / add more speech"))m_IntakeJob=StartIntakeJob("fallback");
                if(Button("Continue to eligibility review"))m_IntakeJob=StartIntakeJob("finish_audio");
                GUI.enabled=true;
            }
            if(IntakeStep>=3 || phase=="withdrawn")DrawIntakeReadiness();
            else if(!string.IsNullOrEmpty(m_Intake.cloneError))IntakeErrorCard(m_Intake.cloneError);
            if(phase=="interview_complete")
            {
                GUILayout.BeginVertical(m_Card);GUILayout.Label(IntakeStep==5?"SEND TO HEADSET":"BACKGROUND AUDIO PREPARATION",m_Heading);
                if(IntakeStep==5)
                    GUILayout.Label($"{m_Intake.participantCode} · Configuration {m_Intake.configuration} · Selected Voice + Self-Similar Voice",m_Label);
                DrawIntakeCloneControls(busy);
                GUI.enabled=true;DrawParticipantAudio();
                if(IntakeStep==5)DrawIntakeSend(busy);
                GUILayout.EndVertical();
            }
            if(phase=="interview_complete")DrawFinalSurveyHandoff();
            GUILayout.Space(12);
            DrawParticipantFit();
            if(m_Intake.answers!=null&&m_Intake.answers.Length>0)
            {
                GUILayout.Label("Last answer: "+m_Intake.answers[m_Intake.answers.Length-1].transcript,m_MutedLabel);
                if(Array.Exists(m_Intake.answers,x=>x.possibleIdentifiers))GUILayout.Label("Possible identifying name in an answer · review transcripts before eligibility approval.",new GUIStyle(m_Label){normal={textColor=m_Warn}});
                if(Array.Exists(m_Intake.answers,x=>x.transcriptionStatus=="failed"))GUILayout.Label("A transcription failed · audio is saved. Retry or type the answer during review.",new GUIStyle(m_Label){normal={textColor=m_Warn}});
            }
        }
        if(!m_CaptureMode&&!File.Exists(IntakeKeys))GUILayout.Label("API configuration missing: "+IntakeKeys,m_Label);
        if(!string.IsNullOrEmpty(m_IntakeError))IntakeErrorCard(m_IntakeError);
        GUILayout.EndScrollView();GUILayout.Space(8);
        if(m_Intake!=null)
        {
            if((phase=="interview"||phase=="fallback") && m_Intake.protocol==IntakeProtocol)DrawAutomaticIntakeControls();
            GUILayout.BeginHorizontal();DrawIntakeWithdrawal();GUILayout.FlexibleSpace();
            DrawIntakeFinish(m_IntakeJob!=null||m_IntakeStarting||m_CaptureMode&&(m_CaptureIntakeActivity=="Submitting"||m_CaptureIntakeActivity?.StartsWith("Listening")==true));GUILayout.EndHorizontal();
        }
        GUI.enabled=m_AnswerClip==null&&!m_IntakeStarting&&!(m_CaptureMode&&m_CaptureIntakeActivity?.StartsWith("Listening")==true);
        if(GUILayout.Button(HelpContent("Close interview panel"),new GUIStyle(m_MutedLabel){alignment=TextAnchor.MiddleCenter},GUILayout.Height(32))){PauseAutomaticIntake();m_IntakeOpen=false;}
        GUI.enabled=true;GUILayout.EndArea();
    }
    bool IntakeCheck(bool value,string label,bool warning=false)
    {
        var style=new GUIStyle(m_Row){normal={textColor=warning?m_Warn:m_Label.normal.textColor}};
        return GUILayout.Button((value?"☑  ":"☐  ")+label,style,GUILayout.MinHeight(44))?!value:value;
    }
    void DrawIntakeReserve()
    {
        GUILayout.BeginHorizontal();GUILayout.BeginVertical(m_Card,GUILayout.Width(472));
        GUILayout.Label(m_IntakeDevelopment ? "DEVELOPMENT REHEARSAL" : "STUDY PARTICIPANT",m_Heading);
        GUILayout.Label("Participant code",m_Label);m_IntakeCode=GUILayout.TextField(m_IntakeCode,m_Field);
        GUILayout.Label(m_IntakeDevelopment ? "Separate rehearsal sequence · excluded from study data" : "Next unused study code · stopped and withdrawn codes remain reserved",m_MutedLabel);
        if(m_CodeDuplicate)GUILayout.Label("Duplicate code — choose an unused code.",new GUIStyle(m_Label){normal={textColor=m_Warn}});
        if(!string.IsNullOrEmpty(m_CodeError))GUILayout.Label(m_CodeError,m_MutedLabel);
        GUILayout.Space(20);
        GUILayout.Label("AUTOMATIC HEADSET FIT",m_Heading);
        GUILayout.Label("After the headset is worn, setup records view separation and an eye-origin separation estimate. Values and warnings appear here before calibration; no IPD entry is needed to reserve.",m_Label);
        GUILayout.Label("These are unvalidated estimates. Manual fallback is available after headset setup if an automatic value is missing.",m_MutedLabel);
        GUILayout.EndVertical();GUILayout.Space(16);GUILayout.BeginVertical(m_Card);
        GUILayout.Label("COUNTERBALANCE",m_Heading);
        string[] orders={"A · Self-Similar first","B · Selected Voice first"};
        for(int i=0;i<2;i++)if(SelectionCard(orders[i],m_IntakeConfiguration==i,56))m_IntakeConfiguration=i;
        GUILayout.Label("Use the assigned counterbalance order. Do not choose based on participant preferences.",m_MutedLabel);GUILayout.Space(24);
        bool development=IntakeCheck(m_IntakeDevelopment,"Development rehearsal — exclude from study data",true);
        if(development!=m_IntakeDevelopment){m_IntakeDevelopment=development;RefreshParticipantCode(true);}
        GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.Space(16);
        GUI.enabled=m_IntakeJob==null&&!m_CodeDuplicate&&m_CodeJob==null&&string.IsNullOrEmpty(m_CodeError)&&m_CheckedCode==m_IntakeCode;
        GUILayout.BeginHorizontal();GUILayout.FlexibleSpace();
        if(PrimaryButton("Reserve participant session"))
        {
            m_IntakeFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents","Trial Review Intake",Guid.NewGuid().ToString("N"));
            m_IntakeJob=StartIntakeJob("create");
        }
        GUILayout.EndHorizontal();GUI.enabled=true;
    }
    void DrawIntakeQuestion(bool busy)
    {
        GUILayout.BeginVertical(m_Card);
        GUILayout.Label(m_Intake.prompt??"",new GUIStyle(m_Label){fontSize=18},GUILayout.MinHeight(64));
        GUILayout.Label(m_Intake.phase=="fallback"?"Optional reading fallback":$"Question {m_Intake.questionIndex+1} of {IntakeQuestionCount} · Listen → tone → speak",m_MutedLabel);
        string activity=m_IntakePaused?"Paused · microphone off":m_PromptPlaying?"River is speaking":m_IntakeJob!=null?"Saving / preparing":m_IntakeStarting?"Tone / preparing microphone":m_AnswerClip!=null?
            (m_VoiceActivity?.SpeechDetected==true?$"Listening · speech detected · silence {m_VoiceActivity.SilentSeconds:0.0}s / {m_VoiceActivity.SilenceSeconds:0}s":"Listening · waiting for speech (10 s)"):"Ready for automatic intake";
        if(m_CaptureMode&&!string.IsNullOrEmpty(m_CaptureIntakeActivity))activity=m_CaptureIntakeActivity;
        var status=GUILayoutUtility.GetRect(10,32,GUILayout.ExpandWidth(true));StatusChip(status,activity,activity.StartsWith("Listening")?m_ErrorColor:m_Info);
        if(m_AnswerClip==null&&!busy&&!(m_CaptureMode&&m_CaptureIntakeActivity?.StartsWith("Listening")==true))
        {
            string[] devices=m_CaptureMode?new[]{"Development microphone (inactive)"}:Microphone.devices;
            string selected=m_IntakeMic??(devices.Length>0?devices[0]:"No microphone available");
            if(Button("Microphone: "+selected+" ▾"))m_MicMenu=!m_MicMenu;
            if(m_MicMenu)foreach(string device in devices)if(Button(device)){m_IntakeMic=device;m_MicMenu=false;}
        }
        if(busy)GUILayout.Label("Preparing the next question…",m_MutedLabel);
        GUILayout.EndVertical();
    }
    void DrawIntakeCloneControls(bool busy)
    {
        GUI.enabled = CanCloneIntake && m_CloneJob == null && m_Intake.cloneState == "uncertain";
        if (m_Intake.cloneState=="uncertain" && Button("Retry failed cloning (may create a duplicate provider voice)")) m_CloneJob = StartIntakeJob("retry_clone");
        GUI.enabled = Consented && !busy && m_CloneJob == null && m_Intake.cloneState == "cloning";
        if (m_Intake.cloneState=="cloning" && Button("Check interrupted clone worker")) m_IntakeJob = StartIntakeJob("recover_clone");

        GUI.enabled=true;
    }
    void DrawIntakeSend(bool busy)
    {
        var headset = m_LiveStatus?.headsetState;
        bool fresh = TrialReviewHeadsetState.Fresh(headset, ReceiverConnected, UtcSeconds, m_LiveStatus?.headsetStateObservedUtc ?? 0);
        GUI.enabled = true;
        GUI.enabled = !busy && Consented && m_Intake.eligible && m_Intake.cacheState == "transferred" && m_FollowLive && !m_DetachedReview && fresh && (headset.intakeAvailable || headset.participantSessionId == m_Intake.id) && m_Intake.phase == "interview_complete" && m_Intake.cloneState == "ready";
        if (m_Intake.cacheState=="transferred" ? PrimaryButton("Send session to headset for confirmation") : Button("Send session to headset for confirmation")) m_IntakeJob = StartIntakeJob("ticket");
        GUI.enabled = true;
        GUILayout.Label(fresh && headset.participantSessionId == m_Intake.id ? "Headset: " + headset.assignmentState : "Headset must be connected and waiting for a participant.", m_MutedLabel);

    }
    void DrawIntakeFinish(bool busy)
    {
        GUI.enabled = !busy && m_CloneJob == null && m_IntakeAudioJob == null && m_WithdrawalJob == null && m_AnswerClip == null && !m_PromptPlaying;
        if (Button("Finish here / create another session")) { PauseAutomaticIntake();m_IntakeAutomatic=false;m_WithdrawRequested=false;m_EditAnswerId=null; m_Intake = null; m_IntakeFolder = null; m_IntakePlayedRevision = 0; m_IntakeConsent = false; m_PendingAnswer = null; m_IntakeIpd = m_IntakeHeadsetIpd = ""; m_IntakeIpdSource = 0; PlayerPrefs.DeleteKey("ParticipantIntakeFolder"); RefreshParticipantCode(true); }
        GUI.enabled = true;

    }
    void DrawIntakeReadiness()
    {
        GUILayout.BeginVertical(m_Card);GUILayout.Label("VOICE READINESS",m_Heading);
        GUILayout.Label($"Answer audio: {m_Intake.recordedSeconds:0}s · speech estimate: {m_Intake.speechSeconds:0}s · words: {m_Intake.sampleWords} · Clone: {m_Intake.cloneState}",m_MutedLabel);
        GUILayout.BeginHorizontal();IntakeMeter($"Speech {m_Intake.speechSeconds:0} / 30 s",m_Intake.speechSeconds/30f);
        GUILayout.Space(16);IntakeMeter($"Words {m_Intake.sampleWords} / 50",m_Intake.sampleWords/50f);GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        var clone=GUILayoutUtility.GetRect(10,28,GUILayout.ExpandWidth(true));StatusChip(clone,"Clone: "+m_Intake.cloneState,!string.IsNullOrEmpty(m_Intake.cloneError)?m_ErrorColor:m_Intake.cloneState=="ready"?m_Ok:m_Info);
        var cache=GUILayoutUtility.GetRect(10,28,GUILayout.ExpandWidth(true));StatusChip(cache,$"Cache: {m_Intake.cacheState} · {(m_Intake.cacheTotal>0?100f*m_Intake.cacheCompleted/m_Intake.cacheTotal:0):0}%",m_Intake.cacheState=="ready"||m_Intake.cacheState=="transferred"?m_Ok:m_Info);
        GUILayout.EndHorizontal();
        if(!string.IsNullOrEmpty(m_Intake.cloneError))IntakeErrorCard(m_Intake.cloneError);
        if(m_Intake.phase=="eligibility_review")
        {
            DrawIntakeCloneControls(m_IntakeJob!=null);
            if(!string.IsNullOrEmpty(m_Intake.cacheError) || m_Intake.cacheState=="preparing" && m_IntakeAudioJob==null)
            {
                if(!string.IsNullOrEmpty(m_Intake.cacheError))IntakeErrorCard(m_Intake.cacheError);
                GUI.enabled=m_IntakeAudioJob==null && CanCacheIntake;
                if(Button("Retry / resume audio cache"))StartParticipantAudio("prepare");
                GUI.enabled=true;
            }
        }
        if(!string.IsNullOrEmpty(m_Intake.cleanupState))GUILayout.Label("Clone cleanup: "+m_Intake.cleanupState,m_MutedLabel);
        if(!string.IsNullOrEmpty(m_Intake.cleanupError))
        {
            IntakeErrorCard(m_Intake.cleanupError);
            GUI.enabled=m_IntakeJob==null;
            if(Button("Retry clone deletion"))m_IntakeJob=StartIntakeJob("cleanup_clone");
            GUI.enabled=true;
        }
        GUILayout.EndVertical();
    }
    void IntakeMeter(string label,float fraction)
    {
        GUILayout.BeginVertical();GUILayout.Label(label,m_Mono);var bar=GUILayoutUtility.GetRect(10,8,GUILayout.ExpandWidth(true));
        Fill(bar,m_Border);Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(fraction),bar.height),fraction>=1?m_Ok:m_Info);GUILayout.Space(12);GUILayout.EndVertical();
    }
    void IntakeErrorCard(string message)
    {
        GUILayout.BeginVertical(m_Card);GUILayout.Label(message,new GUIStyle(m_Label){normal={textColor=m_ErrorColor}});GUILayout.EndVertical();
    }
}
