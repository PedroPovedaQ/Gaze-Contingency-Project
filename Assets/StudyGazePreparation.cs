using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Participant-scoped preparation before activation practice or any scored trial.</summary>
public sealed class StudyGazePreparation : MonoBehaviour
{
    public bool Completed {get;private set;}
    public bool Active {get;private set;}
    public bool Ready => Completed && m_Result!=null && m_Result.participant==SessionConfig.ParticipantId &&
        (m_Result.outcome=="skipped" || (SharedGazeStream.Instance?.QuickAlignment.IsActive==true &&
        SharedGazeStream.Instance.QuickAlignment.Report?.id==m_Result.alignmentId));
    [Serializable] sealed class Receipt
    {
        public int schema=1,run;
        public string id,participant,utc,outcome,alignmentId,validationId,validationPath;
        public bool offsetApplied;
        public ParticipantFitMeasurements participantFit;
        public StudyGazeAccuracy.Result accuracy;
    }
    Receipt m_Result;
    EntryGazeAlignment m_Entry;
    EyeValidationSession m_Validation;
    GameObject m_Panel;
    TextMeshProUGUI m_Text;
    Button m_Continue;
    string m_Choice;

    public IEnumerator Run()
    {
        if(Active)yield break;
        Completed=false;Active=true;
        m_Entry=GetComponent<EntryGazeAlignment>()??gameObject.AddComponent<EntryGazeAlignment>();
        m_Validation=EyeValidationSession.Instance;
        while(Active)
        {
            m_Result=new Receipt {id=Guid.NewGuid().ToString("N"),participant=SessionConfig.ParticipantId,
                run=SessionConfig.RunNumber,utc=DateTime.UtcNow.ToString("o"),participantFit=SessionConfig.ParticipantFit?.Copy()};
            m_Entry.ResetForRetry();
            yield return m_Entry.Run(studyVerification:true);
            if(!Active || !m_Entry.Completed){Abort();yield break;}
            m_Result.alignmentId=SharedGazeStream.Instance?.QuickAlignment.Report?.id;
            if(m_Entry.Outcome=="skipped")
            {
                if(Save("skipped")){Completed=true;Active=false;yield break;}
                ShowResult("Could not save preparation. Retry after restoring storage.",false);
            }
            else
            {
                bool started=m_Validation!=null && m_Validation.BeginStudyAccuracy();
                if(started)
                {
                    GetComponent<HeadsetPreparationStatus>()?.Set("preparing","Independent five-dot accuracy check");
                    while(Active && m_Validation.Active)yield return null;
                    if(!Active)yield break;
                    m_Result.validationId=m_Validation.Status.id;m_Result.validationPath=m_Validation.Status.recordingPath;
                    m_Result.accuracy=m_Validation.StudyAccuracyResult;
                }
                bool passed=started && m_Validation.Status.completed && m_Result.accuracy?.passed==true &&
                    SharedGazeStream.Instance.QuickAlignment.IsActive;
                string detail=passed?$"Mean gaze error: <b>{m_Result.accuracy.meanDegrees:F2}°</b> across five dots.\nSelect Continue for look-and-trigger practice.":
                    "Accuracy check did not pass or was interrupted.\nRetry calibration, or explicitly Skip without an offset.";
                if(started && m_Validation.Status.stage=="skipped")
                {
                    if(Save("skipped")){Completed=true;Active=false;yield break;}
                    detail="Could not save preparation. Retry after restoring storage.";
                }
                // Keep every attempt, including failures, independent of the eventual choice.
                if(!Save(passed?"checked":"needs_attention")){passed=false;detail="Could not save preparation. Retry after restoring storage.";}
                ShowResult(detail,passed);
            }
            m_Choice=null;
            while(Active && m_Choice==null)
            {
                bool valid=SharedGazeStream.Instance?.QuickAlignment.IsActive==true &&
                    SharedGazeStream.Instance.QuickAlignment.Report?.id==m_Result.alignmentId;
                if(m_Continue!=null)m_Continue.interactable=Application.isFocused && valid && m_Result.accuracy?.passed==true;
                yield return null;
            }
            HideResult();
            if(!Active)yield break;
            if(m_Choice=="retry")continue;
            if(m_Choice=="skip")SharedGazeStream.Instance?.ClearQuickAlignment("Participant explicitly skipped study preparation.");
            if(Save(m_Choice=="skip"?"skipped":"passed")){Completed=true;Active=false;}
        }
    }
    bool Save(string outcome)
    {
        m_Result.outcome=outcome;m_Result.offsetApplied=SharedGazeStream.Instance?.QuickAlignment.IsActive==true;
        try
        {
            string dir=Path.Combine(Application.persistentDataPath,"EyeMeasurements");Directory.CreateDirectory(dir);
            string path=Path.Combine(dir,"study_preparation_"+m_Result.id+".json"),temp=path+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(m_Result,true));
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
            GetComponent<FindObjectGameManager>()?.RecordReadinessEvent("study_preparation_"+outcome);
            return true;
        }
        catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
        {Debug.LogError("[StudyPreparation] Could not save: "+e.Message);SharedGazeStream.Instance?.ClearQuickAlignment("Preparation save failed.");return false;}
    }
    void Choose(string choice)
    {
        if(!Active || !Application.isFocused)return;
        if(choice=="continue" && (m_Continue==null || !m_Continue.interactable))return;
        m_Choice=choice;
    }
    void ShowResult(string detail,bool passed)
    {
        HideResult();
        m_Panel=StudyPromptCard.Create(null,"Study preparation result",new Vector2(1100,390));
        var head=Camera.main.transform;var facing=EntryGazeAlignment.HorizontalFacing(head.rotation);
        m_Panel.transform.SetPositionAndRotation(head.position+facing*new Vector3(0,-.15f,1.7f),facing);
        m_Panel.transform.localScale=Vector3.one*.001f;
        var canvas=m_Panel.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;
        m_Panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        var label=new GameObject("Result",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(m_Panel.transform,false);
        m_Text=label.GetComponent<TextMeshProUGUI>();m_Text.rectTransform.sizeDelta=new Vector2(1040,240);m_Text.rectTransform.anchoredPosition=new Vector2(0,50);
        StudyPromptCard.StyleText(m_Text,29);m_Text.text="<b>"+(passed?"Preparation check complete":"Preparation needs attention")+"</b>\n"+detail;
        RayMenuFeedback.CreateButton(m_Panel.transform,"Retry",new Vector2(-350,-130),new Vector2(300,65),()=>Choose("retry"));
        RayMenuFeedback.CreateButton(m_Panel.transform,"Skip (no offset)",new Vector2(0,-130),new Vector2(340,65),()=>Choose("skip"));
        m_Continue=RayMenuFeedback.CreateButton(m_Panel.transform,"Continue",new Vector2(350,-130),new Vector2(300,65),()=>Choose("continue"));
        m_Continue.interactable=passed;
        GetComponent<HeadsetPreparationStatus>()?.Set("preparing",passed?"Accuracy checked; continue to activation practice":"Accuracy check: retry or skip");
    }
    void HideResult(){if(m_Panel!=null){m_Panel.SetActive(false);Destroy(m_Panel);}m_Panel=null;m_Continue=null;}
    public void Abort()
    {
        if(!Active)return;
        Active=false;Completed=false;m_Validation?.AbortStudyAccuracy();m_Entry?.Abort();HideResult();
        SharedGazeStream.Instance?.ClearQuickAlignment("Study preparation interrupted.");
        if(m_Result!=null)Save("interrupted");
    }
    void OnDisable()=>Abort();
}
