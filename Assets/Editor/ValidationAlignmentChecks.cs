using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ValidationAlignmentChecks
{
    static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Fields).SetValue(target,value);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}

    [MenuItem("Tools/Codex/Check Validation Alignment")]
    public static void Run()
    {
        EntryGazeAlignmentChecks.Run();
        var host=new GameObject("Validation context test");host.SetActive(false);
        var previous=EyeValidationSession.Instance;
        var instance=typeof(EyeValidationSession).GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
        EyeValidationSession validation=null;
        try
        {
            validation=host.AddComponent<EyeValidationSession>();
            instance.SetValue(null,validation);
            validation.Status.active=true;validation.Status.id="fixture";validation.Status.participant="P987";
            Set(validation,"m_Steps",EyeValidationProtocol.Build("drift",1,false));
            Set(validation,"m_Progress",new EyeValidationProgress(5));
            var alignment=host.AddComponent<EntryGazeAlignment>();Set(validation,"m_Alignment",alignment);
            Check(SharedGazeStream.MeasurementParticipant=="P987","Validation identity must be selected before fitting/applying an offset");
            var row=new GazeObservation {participant=SharedGazeStream.MeasurementParticipant,
                validationTargetPresent=true,validationTarget=new Vector3(1,2,3),targetId="target_3"};
            validation.Annotate(row);
            Check(row.validationStage=="alignment" && row.validationSet=="alignment" && row.validationId=="fixture",
                "Fit/check rows remain separate from scored running dots");
            Check(row.validationTarget==new Vector3(1,2,3) && row.validationTargetPresent && row.targetId=="target_3",
                "Annotation preserves independent calibration geometry");
            Check(!validation.Command("validation_next",new EyeValidationRequest {id="fixture",revision=0}),
                "Remote Begin cannot bypass wearer calibration acceptance");
            Check(!validation.Command("validation_answer",new EyeValidationRequest {id="fixture",revision=0,value=1}),
                "Remote response cannot accept calibration");
            var request=JsonUtility.FromJson<EyeValidationRequest>("{\"mode\":\"drift\",\"alignFirst\":true}");
            Check(request.alignFirst && !JsonUtility.FromJson<EyeValidationRequest>("{}").alignFirst,
                "Alignment is explicit and legacy requests keep their measurement-only meaning");
            validation.Status.active=false;
            Check(SharedGazeStream.MeasurementParticipant==SessionConfig.ParticipantId,"Validation identity ends with session");
            Debug.Log("[ValidationAlignmentChecks] PASS: scoped participant, distinct fit rows, target preservation, remote acceptance guards, compatible requests.");
        }
        finally
        {
            if(validation!=null)validation.Status.active=false;
            UnityEngine.Object.DestroyImmediate(host);instance.SetValue(null,previous);
        }
    }
}
