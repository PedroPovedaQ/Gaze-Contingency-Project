using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

public sealed partial class TrialReviewApp
{
    [Serializable] sealed class IntakeAnswer { public string question, transcript, audio, topic; public float seconds; public bool skipped; }
    [Serializable] sealed class IntakeConsent { public string id, participantCode, sessionId; public bool study, voiceCloud; }
    [Serializable] sealed class IntakeCapture { public string prompt, consentId; }
    [Serializable] sealed class IntakeState
    {
        public string id, participantCode, configuration, neutralGender, phase, prompt, promptAudio, cloneState, cloneVoiceId, cloneError;
        public string cacheState, cacheError, cacheManifestSha256, previewAudio, protocol;
        public int cacheCompleted, cacheTotal;
        public bool eligible, selectedVoiceConfirmed, development;
        public double withdrawnUtc, previewRevision;
        public IntakeConsent consent;
        public int neutralOption, promptRevision;
        public float recordedSeconds, speechSeconds;
        public int sampleWords;
        public IntakeAnswer[] answers;
        public ParticipantFitMeasurements fit;
    }
    [Serializable] sealed class IntakeRequest
    {
        public string action, folder, keysPath, participantCode, configuration, neutralGender, audio;
        public int neutralOption;
        public bool skip;
        public bool study, voiceCloud, development;
        public string @operator, reference;
        public string[] eligibilityValues;
        public ParticipantFitMeasurements fit;
    }
    [Serializable] sealed class IntakeResult { public bool ok; public string error; }
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
        if (m_IntakeFolder == null)
        {
            string saved = PlayerPrefs.GetString("ParticipantIntakeFolder", "");
            if (File.Exists(Path.Combine(saved, "session.json"))) m_IntakeFolder = saved;
            m_IntakeCode = m_LiveStatus?.headsetState?.nextParticipantCode ?? "P001";
        }
        ReloadIntake();
        if (Consented && m_PendingAnswer == null && (m_Intake.phase == "interview" || m_Intake.phase == "fallback"))
        {
            foreach (string path in Directory.GetFiles(m_IntakeFolder,"answer-*.wav"))
            {
                if (!File.Exists(path+".capture.json")) continue;
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
                keysPath = IntakeKeys, participantCode = m_IntakeCode, configuration = m_IntakeConfiguration == 0 ? "A" : "B",
                neutralGender = m_IntakeGender == 0 ? "Male" : "Female", neutralOption = m_IntakeOption, audio = audio, skip = skip,
                study = m_StudyConsent, voiceCloud = m_IntakeConsent, development = m_IntakeDevelopment,
                @operator = m_ConsentOperator, reference = m_ConsentReference,
                eligibilityValues = Array.ConvertAll(m_Eligibility, x => EligibilityValues[x]),
                fit = action == "create" ? ReadIntakeFit() : null }));
            var process = Process.Start(new ProcessStartInfo("/usr/bin/python3", Argument(Path.Combine(Application.streamingAssetsPath, "participant_intake.py")) + " --request " + Argument(request)) { UseShellExecute = false, CreateNoWindow = true });
            return new IntakeJob { process = process, action = action, result = Path.ChangeExtension(request, ".result.json") };
        }
        catch (Exception e) { m_IntakeError = "Could not start interview worker: " + e.Message; return null; }
    }
    void PollIntake()
    {
        if(m_CaptureMode) return;
        if (m_IntakeStarting || m_AnswerClip != null) return;
        if (Time.realtimeSinceStartupAsDouble < m_IntakePoll) return;
        m_IntakePoll = Time.realtimeSinceStartupAsDouble + .3;
        FinishIntakeJob(ref m_IntakeJob); FinishIntakeJob(ref m_CloneJob);
        if (m_IntakeFolder == null) return;
        ReloadIntake();
        if (m_Intake == null) return;
        PollParticipantAudio();
        if (Consented && m_Intake.eligible && m_Intake.speechSeconds >= 30 && m_Intake.sampleWords >= 50 && m_Intake.cloneState == "not_started" && m_CloneJob == null)
            m_CloneJob = StartIntakeJob("clone");
        if (Consented && m_Intake.cloneState == "ready" && m_Intake.cacheState == "not_started" && string.IsNullOrEmpty(m_Intake.cacheError) && m_IntakeAudioJob == null)
            StartParticipantAudio("prepare");
        if (m_IntakeOpen && m_IntakeJob == null && (m_Intake.phase == "interview" || m_Intake.phase == "fallback") && m_Intake.promptRevision > m_IntakePlayedRevision && !string.IsNullOrEmpty(m_Intake.promptAudio))
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
            { PlayerPrefs.SetString("ParticipantIntakeFolder", m_IntakeFolder); PlayerPrefs.Save(); }
            if (done.action == "authorize_recording") StartCoroutine(StartAnswer());
            if (done.action == "select_voice" || done.action == "fallback" || (done.action == "review" && m_Intake.eligible))
                m_IntakeJob = StartIntakeJob("prompt");
            if (done.action == "ticket") SendIntake();
        }
        catch (Exception) { m_IntakeError = "Interview worker stopped without a result. Recordings remain saved locally."; }
    }
    IEnumerator PlayInterviewPrompt()
    {
        if (m_AnswerClip != null || m_IntakeStarting || m_Intake == null || string.IsNullOrEmpty(m_Intake.promptAudio)) yield break;
        int revision = m_Intake.promptRevision; string sessionId = m_Intake.id;
        m_IntakeError = null;
        yield return PlayIntakeFile(m_Intake.promptAudio,AudioType.MPEG);
        if (m_IntakeOpen && m_Intake?.id == sessionId && m_Intake.promptRevision == revision && string.IsNullOrEmpty(m_IntakeError))
            m_QuestionHeardRevision = revision;
    }
    IEnumerator StartAnswer()
    {
        if(m_CaptureMode) yield break;
        if (!Consented || m_PromptPlaying || m_QuestionHeardRevision != m_Intake.promptRevision || string.IsNullOrEmpty(m_CaptureName)) yield break;
        m_IntakeStarting = true; m_InterviewSpeaker?.Stop(); m_Audio?.Stop(); m_Playing = false;
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone) || Microphone.devices.Length == 0)
            m_IntakeError = "Enable microphone access for Trial Review in macOS Privacy settings.";
        else if (m_IntakeOpen && Consented)
        {
            if (m_InterviewSpeaker == null) { m_InterviewSpeaker = gameObject.AddComponent<AudioSource>(); m_InterviewSpeaker.ignoreListenerVolume = true; }
            var tone = AudioClip.Create("Intake speak cue", 4320, 1, 24000, false);
            var cue = new float[4320];
            for (int i=0; i<cue.Length; i++) cue[i] = .18f * Mathf.Sin(2*Mathf.PI*880*i/24000) * Mathf.Sin(Mathf.PI*i/(cue.Length-1));
            tone.SetData(cue, 0); m_InterviewSpeaker.PlayOneShot(tone);
            yield return new WaitForSecondsRealtime(.25f); Destroy(tone);
            // Cue and prompt are excluded from the participant-only sample.
            if (!m_IntakeOpen || !Consented) { m_IntakeStarting = false; yield break; }
            if (string.IsNullOrEmpty(m_IntakeMic)) m_IntakeMic = Microphone.devices[0];
            m_AnswerClip = Microphone.Start(m_IntakeMic, false, 120, 24000);
            if (m_AnswerClip == null) m_IntakeError = "The microphone could not start.";
        }
        m_IntakeStarting = false;
    }
    void StopAnswer(bool submit = true)
    {
        if (m_AnswerClip == null) return;
        int samples = Microphone.GetPosition(m_IntakeMic);
        if (!Microphone.IsRecording(m_IntakeMic)) samples = m_AnswerClip.samples;
        Microphone.End(m_IntakeMic);
        try
        {
            if (samples < 2400) throw new InvalidOperationException("Record an answer before submitting.");
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
            m_PendingAnswer = name;
            if (submit) m_IntakeJob = StartIntakeJob("answer", name);
        }
        catch (Exception e) { m_IntakeError = e.Message; }
        Destroy(m_AnswerClip); m_AnswerClip = null;
    }
    void StopIntake()
    {
        m_InterviewSpeaker?.Stop();
        if (m_AnswerClip != null) StopAnswer(false);
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
                case "interview":case "fallback":return 3;
                case "eligibility_review":case "screening_stopped":return 4;
                case "interview_complete":return m_Intake.cacheState=="ready"||m_Intake.cacheState=="transferred"?6:5;
                default:return -1;
            }
        }
    }
    bool PrimaryButton(string label,params GUILayoutOption[] options)=>GUILayout.Button(HelpContent(label),m_ActiveChip,options);
    void IntakeStepper()
    {
        string[] steps={"Reserve","Consent","Selected Voice","Audio intake","Eligibility review","Clone & cache","Send to headset"};
        bool[] complete={m_Intake!=null,Consented,m_Intake?.selectedVoiceConfirmed==true,m_Intake?.phase=="interview_complete",
            m_Intake?.eligible==true,m_Intake?.cloneState=="ready"&&(m_Intake.cacheState=="ready"||m_Intake.cacheState=="transferred"),false};
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
        var rect = new Rect(width / 2 - 520, 40, 1040, height - 80);
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
            GUILayout.Label("Consent first. Hear each question, then select Record. Speak after the tone; select Stop when finished. Use headphones to prevent speaker spill.",m_MutedLabel);
            GUILayout.Space(12);
        }
        if(m_Intake==null)DrawIntakeReserve();
        else
        {
            bool busy=m_IntakeJob!=null||m_IntakeStarting||m_CaptureMode&&m_CaptureIntakeActivity=="Submitting";
            if(phase!="interview"&&phase!="fallback"&&phase!="interview_complete"||m_Intake.protocol!="audio-intake-v2-20261028")
            {GUILayout.BeginVertical(m_Card);DrawIntakeConsentAndReview();GUILayout.EndVertical();}
            if(phase=="interview"||phase=="fallback")DrawIntakeQuestion(busy);
            if(IntakeStep>=3)DrawIntakeReadiness();
            else if(!string.IsNullOrEmpty(m_Intake.cloneError))IntakeErrorCard(m_Intake.cloneError);
            if(phase=="interview_complete"||m_Intake.cloneState!="not_started"||Consented&&m_Intake.eligible&&m_Intake.speechSeconds>=30&&m_Intake.sampleWords>=50)
            {
                GUILayout.BeginVertical(m_Card);GUILayout.Label(IntakeStep==6?"SEND TO HEADSET":"CLONE & CACHE",m_Heading);
                if(IntakeStep==6)
                    GUILayout.Label($"{m_Intake.participantCode} · Configuration {m_Intake.configuration} · Selected Voice + Self-Similar Voice",m_Label);
                DrawIntakeCloneControls(busy);
                GUI.enabled=true;DrawParticipantAudio();
                if(IntakeStep==6)DrawIntakeSend(busy);
                GUILayout.EndVertical();
            }
            if(phase=="interview_complete")DrawFinalSurveyHandoff();
            GUILayout.Space(12);
            var fit=m_Intake.fit;
            GUILayout.Label(fit!=null&&fit.ipdRecorded?$"Participant IPD: {fit.participantIpdMm:0.##} mm · {fit.ipdSource}":"Participant IPD: not recorded",m_MutedLabel);
            GUILayout.Label(fit!=null&&fit.headsetIpdRecorded?$"Headset lens spacing: {fit.headsetIpdMm:0.##} mm (manual entry)":"Headset lens spacing: not recorded",m_MutedLabel);
            if(m_Intake.answers!=null&&m_Intake.answers.Length>0)GUILayout.Label("Last answer: "+m_Intake.answers[m_Intake.answers.Length-1].transcript,m_MutedLabel);
        }
        if(!m_CaptureMode&&!File.Exists(IntakeKeys))GUILayout.Label("API configuration missing: "+IntakeKeys,m_Label);
        if(!string.IsNullOrEmpty(m_IntakeError))IntakeErrorCard(m_IntakeError);
        GUILayout.EndScrollView();GUILayout.Space(8);
        if(m_Intake!=null)
        {
            GUILayout.BeginHorizontal();DrawIntakeWithdrawal();GUILayout.FlexibleSpace();
            DrawIntakeFinish(m_IntakeJob!=null||m_IntakeStarting||m_CaptureMode&&(m_CaptureIntakeActivity=="Submitting"||m_CaptureIntakeActivity?.StartsWith("Recording")==true));GUILayout.EndHorizontal();
        }
        GUI.enabled=m_AnswerClip==null&&!m_IntakeStarting&&!(m_CaptureMode&&m_CaptureIntakeActivity?.StartsWith("Recording")==true);
        if(GUILayout.Button(HelpContent("Close interview panel"),new GUIStyle(m_MutedLabel){alignment=TextAnchor.MiddleCenter},GUILayout.Height(32))){m_IntakeOpen=false;m_InterviewSpeaker?.Stop();}
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
        GUILayout.Label("PARTICIPANT & FIT",m_Heading);
        GUILayout.Label("Participant code",m_Label);m_IntakeCode=GUILayout.TextField(m_IntakeCode,m_Field);
        GUILayout.Label("Participant binocular distance IPD (mm) · blank = not recorded",m_Label);
        m_IntakeIpd=GUILayout.TextField(m_IntakeIpd,m_Field);
        string[] sources={"Not recorded","Pupillometer","Ruler","Participant report"};
        for(int row=0;row<2;row++)
        {
            GUILayout.BeginHorizontal();
            for(int col=0;col<2;col++){int i=row*2+col;if(SelectionCard(sources[i],m_IntakeIpdSource==i,40))m_IntakeIpdSource=i;}
            GUILayout.EndHorizontal();
        }
        GUILayout.Label("Headset lens spacing (mm) · optional, entered separately",m_Label);
        m_IntakeHeadsetIpd=GUILayout.TextField(m_IntakeHeadsetIpd,m_Field);
        GUILayout.Label("Confirm these measurements before beginning. They are saved with this session; eye tracking does not measure them automatically.",m_MutedLabel);
        GUILayout.EndVertical();GUILayout.Space(16);GUILayout.BeginVertical(m_Card);
        GUILayout.Label("COUNTERBALANCE",m_Heading);
        string[] orders={"A · Self-Similar first","B · Selected Voice first"};
        for(int i=0;i<2;i++)if(SelectionCard(orders[i],m_IntakeConfiguration==i,56))m_IntakeConfiguration=i;
        GUILayout.Label("Use the assigned counterbalance order. Do not choose based on participant preferences.",m_MutedLabel);GUILayout.Space(24);
        m_IntakeDevelopment=IntakeCheck(m_IntakeDevelopment,"Development rehearsal — exclude from study data",true);
        GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.Space(16);
        GUI.enabled=m_IntakeJob==null;
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
        GUILayout.Label("Listen → tone → speak",m_MutedLabel);
        string activity=m_PromptPlaying?"Prompt playing":m_IntakeJob!=null?"Submitting":m_IntakeStarting?"Preparing the next question…":m_AnswerClip!=null?
            "Recording ● "+Clock(Microphone.IsRecording(m_IntakeMic)?(double)Microphone.GetPosition(m_IntakeMic)/m_AnswerClip.frequency:m_AnswerClip.length):
            m_QuestionHeardRevision==m_Intake.promptRevision&&m_QuestionHeardRevision>0?"Ready to record":"Listen to the question before recording";
        if(m_CaptureMode&&!string.IsNullOrEmpty(m_CaptureIntakeActivity))activity=m_CaptureIntakeActivity;
        var status=GUILayoutUtility.GetRect(10,32,GUILayout.ExpandWidth(true));StatusChip(status,activity,activity.StartsWith("Recording")?m_ErrorColor:m_Info);
        if(m_AnswerClip==null&&!busy&&!(m_CaptureMode&&m_CaptureIntakeActivity?.StartsWith("Recording")==true))
        {
            string[] devices=m_CaptureMode?new[]{"Development microphone (inactive)"}:Microphone.devices;
            string selected=m_IntakeMic??(devices.Length>0?devices[0]:"No microphone available");
            if(Button("Microphone: "+selected+" ▾"))m_MicMenu=!m_MicMenu;
            if(m_MicMenu)foreach(string device in devices)if(Button(device)){m_IntakeMic=device;m_MicMenu=false;}
        }
        GUI.enabled = !busy && !m_PromptPlaying && Consented && m_QuestionHeardRevision == m_Intake.promptRevision && m_QuestionHeardRevision > 0;
        if(m_CaptureMode&&(activity=="Submitting"||activity=="Prompt playing"))GUI.enabled=false;
        GUILayout.BeginHorizontal();GUILayout.FlexibleSpace();
        if (PrimaryButton(m_AnswerClip != null || m_CaptureMode&&activity.StartsWith("Recording") ? "Stop and submit answer" : "Record participant answer"))
        { if (m_AnswerClip != null) StopAnswer(); else { m_CaptureName = "answer-"+Guid.NewGuid().ToString("N")+".wav"; m_IntakeJob = StartIntakeJob("authorize_recording",m_CaptureName); } }
        GUILayout.EndHorizontal();
        GUI.enabled = !busy && m_AnswerClip == null && !m_PromptPlaying && Consented;
        if(m_CaptureMode&&(activity=="Submitting"||activity=="Prompt playing"||activity.StartsWith("Recording")))GUI.enabled=false;
        GUILayout.BeginHorizontal();
        if (Button("Skip this question")) { m_InterviewSpeaker?.Stop(); m_IntakeJob = StartIntakeJob("answer", skip: true); }
        if (Button("Retry spoken prompt")) m_IntakeJob = StartIntakeJob("prompt");
        if (m_PendingAnswer != null && Button("Retry saved answer")) m_IntakeJob = StartIntakeJob("answer", m_PendingAnswer);
        GUILayout.EndHorizontal();GUI.enabled = true;

        if(busy)GUILayout.Label("Preparing the next question…",m_MutedLabel);
        GUILayout.EndVertical();
    }
    void DrawIntakeCloneControls(bool busy)
    {
        GUI.enabled = !busy && Consented && m_Intake.phase == "interview_complete" && m_Intake.cloneState == "not_started";
        if (m_Intake.phase=="interview_complete" && m_Intake.cloneState=="not_started" && Button("Read fallback passage / add more speech")) m_IntakeJob = StartIntakeJob("fallback");
        GUI.enabled = Consented && m_Intake.eligible && m_CloneJob == null && m_Intake.cloneState == "not_started" && m_Intake.speechSeconds >= 30 && m_Intake.sampleWords >= 50;
        if (m_Intake.cloneState=="not_started" && Button("Prepare voice from recorded answers")) m_CloneJob = StartIntakeJob("clone");
        GUI.enabled = Consented && m_CloneJob == null && m_Intake.cloneState == "uncertain";
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
        var headset=m_LiveStatus?.headsetState;
        GUI.enabled = !busy && m_CloneJob == null && m_IntakeAudioJob == null && m_AnswerClip == null;
        if (Button("Finish here / create another session")) { m_InterviewSpeaker?.Stop(); m_Intake = null; m_IntakeFolder = null; m_IntakePlayedRevision = 0; m_IntakeConsent = false; m_PendingAnswer = null; m_IntakeIpd = m_IntakeHeadsetIpd = ""; m_IntakeIpdSource = 0; PlayerPrefs.DeleteKey("ParticipantIntakeFolder"); m_IntakeCode = headset?.nextParticipantCode ?? "P001"; }
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
