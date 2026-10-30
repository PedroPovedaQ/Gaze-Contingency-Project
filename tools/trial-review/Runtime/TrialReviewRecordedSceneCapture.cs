using System;
using System.Collections;
using System.IO;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    // Development-only, exact-source recapture preset for the recorded-search-scene figure.
    // m_CaptureMode is set in Awake, before Open can register a catalog or request exports.
    const string RecordedSceneHash="6c731b5902d307955ccdfbe5f56ffd5cddf70e6d3a9b425842ef70a965278c15";
    IEnumerator CaptureRecordedScene(string[] args)
    {
        int index=Array.IndexOf(args,"--recording");
        string path=index>=0&&index+1<args.Length?Path.GetFullPath(args[index+1]):null;
        // Only accept a read-only temporary copy. Never fall back to the synthetic demo.
        if(path==null || !path.StartsWith(Path.GetTempPath(),StringComparison.Ordinal) ||
            !File.Exists(path) || (File.GetAttributes(path)&FileAttributes.ReadOnly)==0 ||
            TrialReviewSummary.Hash(path)!=RecordedSceneHash)
        {
            Debug.LogError("Recorded scene capture requires the exact read-only temporary Run 26 recording.");
            Application.Quit(1);yield break;
        }
        m_DetachedReview=true;
        yield return Open(path);
        if(m_Timeline==null) { Application.Quit(1);yield break; }
        m_ShowWalls=false; // Same backing state as the Viewer walls option.
        m_ShowDegrees=m_ShowDetectionRing=m_ShowSubtitles=m_ShowStateMarkers=false;
        m_ShowRoom=m_ShowBody=m_ShowEyeRay=true;
        m_Renderer.ShowTarget=false;m_Renderer.ShowControllers=true;m_Renderer.ShowGaze=false;
        Seek(14.83);
        if(m_Timeline.State.Trial.trialId!="trial_001" || m_Timeline.State.Sample.sequence!=919 ||
            m_Timeline.State.Trial.objects.Length!=168 || m_Correction.Supported ||
            m_Timeline.Header.gazeRayVersion!=TrialReplayGaze.Version)
        {
            Debug.LogError("Recorded scene identity/sample or uncorrected-ray contract changed.");
            Application.Quit(1);yield break;
        }
        Directory.CreateDirectory(m_CaptureDirectory);
        m_Tracks.Refresh(m_Timeline);
        foreach(int mode in new[]{0,2})
        {
            m_CameraMode=mode;m_Renderer.Yaw=0;m_Renderer.Pitch=89;m_Renderer.Distance=5.5f;
            for(int frame=0;frame<4;frame++)yield return null;
            yield return new WaitForEndOfFrame();
            var sample=m_Timeline.State.Sample;
            if(m_Actors.EyeRay.GetPosition(0)!=sample.gazeOrigin ||
                m_Actors.EyeRay.GetPosition(1)!=sample.gazeOrigin+sample.gazeDirection.normalized*3 ||
                m_Renderer.RightControllerRayStart!=sample.rightController.rayOrigin ||
                m_Renderer.RightControllerRayEnd!=sample.rightController.rayEnd)
            {
                Debug.LogError("Recorded scene rays differ from saved origins/endpoints.");
                Application.Quit(1);yield break;
            }
            string name=mode==0?"first-person-no-walls-full.png":"overhead-no-walls-full.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(m_CaptureDirectory,name));
            for(int frame=0;frame<4;frame++)yield return null;
            Debug.Log($"RECORDED_SCENE_CAPTURE {name} screen={Screen.width}x{Screen.height} viewport={m_Viewport} camera={m_Renderer.Camera.transform.position:R} rotation={m_Renderer.Camera.transform.rotation:R} fov={m_Renderer.Camera.fieldOfView:R} walls={m_Environment.WallsVisible} room={m_Environment.RoomVisible} roomEnvironment={m_Timeline.State.Trial.roomEnvironment} sequence={m_Timeline.State.Sample.sequence} gazeCorrection={m_Actors.CorrectionApplied}");
        }
        if(TrialReviewSummary.Hash(path)!=RecordedSceneHash || m_Catalog!=null || m_RunCsvProcess!=null || m_Bridge!=null)
        {
            Debug.LogError("Recorded scene source/isolation verification failed.");
            Application.Quit(1);yield break;
        }
        m_Vicinity.Dispose();m_Environment.Dispose();m_Environment=null;
        m_Actors.Dispose();m_Actors=null;m_Audio.Dispose();m_Audio=null;m_Renderer.Dispose();m_Renderer=null;
        enabled=false;
        Debug.Log("RECORDED_SCENE_CAPTURE_PASS "+RecordedSceneHash+" time=14.83; no catalog, exports, services or source writes");
        Application.Quit();
    }
}
