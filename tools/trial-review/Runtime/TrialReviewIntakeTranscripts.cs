using System;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    int m_TranscriptIndex;
    string m_EditAnswerId, m_EditTranscript, m_EditCategory;
    bool TranscriptEditPending
    {
        get
        {
            var answer=Array.Find(m_Intake?.answers??Array.Empty<IntakeAnswer>(),x=>x.id==m_EditAnswerId);
            return answer!=null && (m_EditTranscript!=(answer.transcript??"") ||
                m_EditCategory!=null && m_EditCategory!=answer.structuredValue);
        }
    }
    void DrawIntakeTranscriptReview(bool idle)
    {
        if(m_Intake.answers==null || m_Intake.answers.Length==0)return;
        GUILayout.Space(12);GUILayout.Label("All answer transcripts · review and correct before saving eligibility",m_Heading);
        GUILayout.Label("Original machine transcripts are retained. Check every answer for names or other identifying details; flags are only a local heuristic.",m_MutedLabel);
        var labels=new string[m_Intake.answers.Length];
        for(int i=0;i<labels.Length;i++)labels[i]=(i+1).ToString();
        GUI.enabled=idle && !TranscriptEditPending;
        m_TranscriptIndex=GUILayout.SelectionGrid(Math.Min(m_TranscriptIndex,labels.Length-1),labels,10,m_Button);
        GUI.enabled=idle;
        var answer=m_Intake.answers[m_TranscriptIndex];
        if(m_EditAnswerId!=answer.id)
        {m_EditAnswerId=answer.id;m_EditTranscript=answer.transcript??"";m_EditCategory=answer.structuredValue;}
        GUILayout.Label(answer.question,m_Label);
        GUILayout.Label($"Transcription: {answer.transcriptionStatus ?? "historical"} · {(answer.researcherCorrected?"researcher corrected":"machine / unreviewed")}",m_MutedLabel);
        if(answer.possibleIdentifiers)GUILayout.Label("Possible name or identifier: researcher review required. Text has not been redacted.",new GUIStyle(m_Label){normal={textColor=m_Warn}});
        if(!string.IsNullOrEmpty(answer.transcriptionError))GUILayout.Label("Transcription failed; audio retained. Retry or type the answer. "+answer.transcriptionError,m_Label);
        m_EditTranscript=GUILayout.TextArea(m_EditTranscript,m_Field,GUILayout.MinHeight(72));
        string[] categories=answer.topic=="vr"||answer.topic=="voice_assistant"?new[]{"never","less_than_monthly","monthly","weekly","daily","unclear"}:
            answer.topic=="eye_tracking"?new[]{"yes","no","unclear"}:answer.topic=="handedness"?new[]{"right","left","both_equally","unclear"}:
            answer.topic=="visual_correction"?new[]{"none","glasses","contact_lenses","other","unclear"}:null;
        if(categories!=null)
        {
            GUILayout.Label("Researcher coding · keep Unclear when frequency or category is not stated",m_MutedLabel);
            int selected=Array.IndexOf(categories,m_EditCategory);if(selected<0)selected=categories.Length-1;
            m_EditCategory=categories[GUILayout.SelectionGrid(selected,categories,3,m_Button)];
        }
        else m_EditCategory=null;
        GUILayout.BeginHorizontal();
        if(!answer.skipped && Button("Listen to this answer"))StartCoroutine(PlayIntakeFile(answer.audio,AudioType.WAV));
        if(!answer.skipped && Button("Retry transcription"))m_IntakeJob=StartIntakeJob("retry_transcript");
        GUI.enabled=idle && !string.IsNullOrWhiteSpace(m_ConsentOperator);
        if(Button("Save transcript / coding correction"))m_IntakeJob=StartIntakeJob("correct_transcript");
        GUILayout.EndHorizontal();GUI.enabled=true;
    }
}
