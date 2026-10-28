using System;
using UnityEngine;

public sealed partial class EyeValidationSession
{
    EntryGazeAlignment m_Alignment;

    void BeginAlignment()
    {
        // Reuse the study's bounded fit + three held-out checks + wearer confirmation.
        // The later validation dots never contribute to the fitted correction.
        HideCanvases();
        var host=new GameObject("Validation-only gaze alignment");
        m_Alignment=host.AddComponent<EntryGazeAlignment>();
        m_Alignment.StartCoroutine(m_Alignment.Run());
        Status.stage="alignment";Status.exercise="alignment";Status.condition="fit_and_independent_checks";
        Status.count=m_Steps.Length;Status.step=0;
        Status.prompt="In the headset, select Start calibration. Look at each red center, then check the shapes and confirm OK. The accuracy check follows separately.";
        Event("alignment_start");SaveManifest("in_progress","");
    }

    void UpdateAlignment()
    {
        Status.error=m_Alignment.Outcome=="needs_attention"?m_Stream.QuickAlignment.Status:"";
        if(m_Alignment.Active)
        {
            Status.condition=m_Alignment.Outcome;
            if(Now-m_LastCheckpoint>=5)
            {m_Manifest.alignment=m_Stream.QuickAlignment.Report;SaveManifest("in_progress","");m_LastCheckpoint=Now;}
            return;
        }
        m_Manifest.alignment=m_Stream.QuickAlignment.Report;
        if(!m_Alignment.Completed || m_Alignment.Outcome!="passed" || !m_Stream.QuickAlignment.IsActive ||
            m_Manifest.alignment?.participant!=Status.participant)
        {End("interrupted","Alignment was skipped or did not pass. No corrected accuracy check was started.");return;}
        Event("alignment_accepted","headset",m_Manifest.alignment.id);
        ReleaseAlignment();
        BeginExercises();
    }

    void BeginExercises()
    {
        CreateView();m_Manifest.worldAnchor=m_Anchor;m_Manifest.worldHeading=m_Heading;
        // Also identify an existing correction during an ordinary check, when present.
        if(m_Stream.QuickAlignment.IsActive || m_Stream.QuickAlignment.AwaitingAccuracy)m_Manifest.alignment=m_Stream.QuickAlignment.Report;
        m_Progress.since=Now;m_Progress.revision++;Status.error="";
        SaveManifest("in_progress","");EnterStep();
    }

    void ReleaseAlignment()
    {
        if(m_Alignment==null)return;
        m_Alignment.StopAllCoroutines();m_Alignment.Abort();
        m_Alignment.gameObject.SetActive(false);Destroy(m_Alignment.gameObject);m_Alignment=null;
        RestoreCanvases();
    }
}
