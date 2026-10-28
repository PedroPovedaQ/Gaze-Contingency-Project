using System;
using System.Globalization;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Detail coordinates are Unity world metres, not the replay's fixed-origin frame.
// Keep the original JSON visible/exportable rather than transforming evidence.
public static class TrialReviewTheta
{
    [Serializable] public sealed class Snapshot
    {
        public string reference;
        public int round;
        public long gazeSequence;
        public double time, gazeReceiptTime;
        public Vector3 eyeOrigin, readinessPosition, targetPosition;
        public float assignedSignedDegrees, realizedSignedDegrees, absoluteDegrees, assignmentErrorDegrees;
        public float targetHeightRelativeToCross;
    }
    public static Snapshot Read(TrialReplayRecord record)
    {
        if(record==null || record.kind!="readiness_completed" || string.IsNullOrEmpty(record.detail))return null;
        try
        {
            var json=JObject.Parse(record.detail,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            foreach(string key in new[]{"round","gazeSequence","time","gazeReceiptTime","assignedSignedDegrees","realizedSignedDegrees",
                "absoluteDegrees","assignmentErrorDegrees","targetHeightRelativeToCross"})
                if(!Numeric(json[key]))return null;
            foreach(string key in new[]{"eyeOrigin","readinessPosition","targetPosition"})
            {
                if(!(json[key] is JObject vector))return null;
                foreach(string axis in new[]{"x","y","z"})if(!Numeric(vector[axis]))return null;
            }
            if(json["round"].Type!=JTokenType.Integer || json["gazeSequence"].Type!=JTokenType.Integer ||
                (long)json["round"]!=record.round || (long)json["round"]<0 || (long)json["gazeSequence"]<0 ||
                json["reference"]?.Type!=JTokenType.String || (string)json["reference"]!="pre_cue_eye_origin_to_readiness_cross")return null;
            var value=json.ToObject<Snapshot>();
            foreach(float number in new[]{value.eyeOrigin.x,value.eyeOrigin.y,value.eyeOrigin.z,
                value.readinessPosition.x,value.readinessPosition.y,value.readinessPosition.z,
                value.targetPosition.x,value.targetPosition.y,value.targetPosition.z,value.assignedSignedDegrees,
                value.realizedSignedDegrees,value.absoluteDegrees,value.assignmentErrorDegrees,value.targetHeightRelativeToCross})
                if(float.IsNaN(number)||float.IsInfinity(number))return null;
            if(value.assignedSignedDegrees!=0 && value.assignedSignedDegrees!=45 && value.assignedSignedDegrees!=-45 &&
                value.assignedSignedDegrees!=90 && value.assignedSignedDegrees!=-90 && value.assignedSignedDegrees!=135 &&
                value.assignedSignedDegrees!=-135 && value.assignedSignedDegrees!=180)return null;
            if(value.absoluteDegrees<0 || value.absoluteDegrees>180 || Math.Abs(value.assignmentErrorDegrees)>180 || value.gazeReceiptTime<0 || value.time<value.gazeReceiptTime || Math.Abs(value.realizedSignedDegrees)>180 ||
                Math.Abs(value.assignedSignedDegrees)>180)return null;
            Vector3 cross=value.readinessPosition-value.eyeOrigin, target=value.targetPosition-value.eyeOrigin;
            cross.y=target.y=0;
            if(cross.sqrMagnitude<1e-12 || target.sqrMagnitude<1e-12)return null;
            float actual=Vector3.SignedAngle(cross,target,Vector3.up);
            float error=Mathf.DeltaAngle(value.assignedSignedDegrees,value.realizedSignedDegrees);
            if(Math.Abs(Mathf.DeltaAngle(actual,value.realizedSignedDegrees))>.02f ||
                Math.Abs(Math.Abs(value.realizedSignedDegrees)-value.absoluteDegrees)>.001f ||
                Math.Abs(Mathf.DeltaAngle(error,value.assignmentErrorDegrees))>.001f || Math.Abs(error)>.0101f ||
                Math.Abs(value.targetPosition.y-value.readinessPosition.y-value.targetHeightRelativeToCross)>.001f)return null;
            return value;
        }
        catch(JsonException){return null;}
        catch(OverflowException){return null;}
        catch(ArgumentException){return null;}
    }
    static bool Numeric(JToken token)
    {
        if(token==null || token.Type!=JTokenType.Integer && token.Type!=JTokenType.Float)return false;
        double number=(double)token;
        return !double.IsNaN(number)&&!double.IsInfinity(number);
    }
    public static string Number(double number)=>number.ToString("R",CultureInfo.InvariantCulture);
    public static string Position(Vector3 value)=>$"({Number(value.x)}, {Number(value.y)}, {Number(value.z)}) m";
}

public partial class TrialReviewApp
{
    bool m_ThetaRawExpanded;
    void ThetaInspector()
    {
        var record=m_Timeline.State.ThetaSnapshot;
        var value=TrialReviewTheta.Read(record);
        if(value==null)
        {
            GUILayout.Label(record==null?"Unavailable at this cursor. Older recordings and practice may have no snapshot.":"Malformed snapshot; inspect the raw event detail.",m_MutedLabel);
        }
        else
        {
            Metric("Assigned",TrialReviewTheta.Number(value.assignedSignedDegrees)+"°");
            Metric("Realized",TrialReviewTheta.Number(value.realizedSignedDegrees)+"°");
            Metric("Absolute",TrialReviewTheta.Number(value.absoluteDegrees)+"°");
            Metric("Error",TrialReviewTheta.Number(value.assignmentErrorDegrees)+"°");
            Metric("Target height",TrialReviewTheta.Number(value.targetHeightRelativeToCross)+" m from cross");
            Metric("Eye origin",TrialReviewTheta.Position(value.eyeOrigin));
            Metric("Cross",TrialReviewTheta.Position(value.readinessPosition));
            Metric("Target",TrialReviewTheta.Position(value.targetPosition));
            Metric("Gaze sequence",value.gazeSequence.ToString());
            Metric("Round index",value.round.ToString());
            Metric("Snapshot clock",TrialReviewTheta.Number(value.time)+" s realtime");
            Metric("Gaze receipt",TrialReviewTheta.Number(value.gazeReceiptTime)+" s realtime");
            Metric("Reference",value.reference);
            GUILayout.Label("Recorded at readiness completion before the opening cue. Positions use Unity world coordinates; clocks are app realtime, not replay time. This is horizontal geometry, not accumulated movement.",m_MutedLabel);
        }
        if(record!=null)
        {
            m_ThetaRawExpanded=GUILayout.Toggle(m_ThetaRawExpanded,"Show exact angle snapshot JSON",m_Chip);
            if(m_ThetaRawExpanded)GUILayout.TextArea(record.detail,m_Code);
        }
    }
}
