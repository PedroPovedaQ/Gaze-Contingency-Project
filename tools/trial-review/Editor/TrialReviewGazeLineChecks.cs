using System;
using UnityEngine;

public static class TrialReviewGazeLineChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Gaze line: "+message);}
    public static void Run()
    {
        var cameraObject=new GameObject("Gaze projection check");
        var lineObject=new GameObject("Gaze line check");
        try
        {
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.nearClipPlane=.01f;camera.fieldOfView=65;
            camera.transform.SetPositionAndRotation(new Vector3(1,2,3),Quaternion.Euler(20,70,0));
            var line=lineObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=2;
            Vector3 origin=camera.transform.TransformPoint(new Vector3(.02f,.01f,-.005f));
            Vector3 direction=camera.transform.TransformDirection(new Vector3(.1f,.15f,1).normalized);
            Vector3 end=origin+direction*3;
            foreach(float height in new[]{240f,500f,1200f})
            {
                line.enabled=true;line.SetPosition(0,origin);line.SetPosition(1,end);
                TrialReviewGazeLine.FitToCamera(line,camera,height);
                Check(line.enabled,"Forward ray remains visible");
                Check(Vector3.Cross(line.GetPosition(0)-origin,direction).magnitude<.00001f,"Clipped start stays on original ray");
                Check(Vector3.Distance(line.GetPosition(1),end)<.00001f,"Endpoint unchanged");
                for(int i=0;i<2;i++)
                {
                    float depth=Vector3.Dot(line.GetPosition(i)-camera.transform.position,camera.transform.forward);
                    float width=i==0?line.startWidth:line.endWidth;
                    float pixels=width*height/(2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad/2));
                    Check(depth>=camera.nearClipPlane&&Mathf.Abs(pixels-2.5f)<.001f,"Thin 2.5px line at both ends for every viewport size");
                }
            }
            line.SetPosition(0,camera.transform.position-camera.transform.forward);
            line.SetPosition(1,camera.transform.position-camera.transform.forward*3);
            TrialReviewGazeLine.FitToCamera(line,camera,500);
            Check(!line.enabled,"Behind-camera ray hidden");
            line.enabled=true;line.SetPosition(0,end);line.SetPosition(1,origin);
            TrialReviewGazeLine.FitToCamera(line,camera,500);
            Check(line.enabled&&Vector3.Distance(line.GetPosition(0),end)<.00001f,"Reverse near-plane crossing retains visible segment");
            Debug.Log("TRIAL_REVIEW_GAZE_LINE_PASS near-plane clipping, unchanged ray/endpoint, constant screen width, rotated camera, viewport sizes, behind-camera");
        }
        finally{UnityEngine.Object.DestroyImmediate(lineObject);UnityEngine.Object.DestroyImmediate(cameraObject);}
    }
}
