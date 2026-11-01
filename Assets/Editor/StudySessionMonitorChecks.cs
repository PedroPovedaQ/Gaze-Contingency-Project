using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class StudySessionMonitorChecks
{
    [MenuItem("Tools/Codex/Check Session Monitor")]
    public static void Run()
    {
        var host=new GameObject("Synthetic session monitor");host.SetActive(false);
        string root=Path.Combine(Path.GetTempPath(),"FMV-DEVELOPMENT-MONITOR-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var results=new JArray();
        try
        {
            var status=host.AddComponent<HeadsetPreparationStatus>();
            typeof(HeadsetPreparationStatus).GetField("m_Path",flags).SetValue(status,Path.Combine(root,"live-headset-state.json"));
            var game=host.AddComponent<FindObjectGameManager>();
            typeof(HeadsetPreparationStatus).GetField("m_Game",flags).SetValue(status,game);
            var selector=host.AddComponent<VoiceModeSelector>();
            var assignment=new ParticipantSessionAssignment { id=new string('d',32), participantCode="P900000000001",configuration="A" };
            typeof(VoiceModeSelector).GetField("m_DesktopAssignment",flags).SetValue(selector,assignment);
            var phase=typeof(VoiceModeSelector).GetField("m_Phase",flags);
            void Call(string name,params object[] args)=>typeof(HeadsetPreparationStatus).GetMethod(name,flags).Invoke(status,args);
            void AssertScreen(string expected)
            {
                Call("Write");var value=JObject.Parse(File.ReadAllText(Path.Combine(root,"live-headset-state.json")));
                if(!((string)value["screen"]).Contains(expected))throw new Exception("Expected "+expected+", received "+value["screen"]);
                if((int)value["trialTotal"]!=96)throw new Exception("Trial total changed");
                results.Add(value);
            }
            foreach(string screen in new[]{"Waiting for participant assignment","Confirm your session","Check Selected Voice audio","Check your self-similar audio"})
            {
                typeof(VoiceModeSelector).GetMethod("SetText",flags).Invoke(selector,new object[]{"<b>"+screen+"</b>\nDetails"});AssertScreen(screen);
            }
            phase.SetValue(selector,Enum.Parse(phase.FieldType,"Done"));
            Call("Checkpoint","search_paused");AssertScreen("researcher checkpoint");
            Call("Checkpoint","readiness_resumed");AssertScreen("Readiness cross");
            Call("Checkpoint","search_resumed");AssertScreen("Active search");
            typeof(FindObjectGameManager).GetField("<IsPractice>k__BackingField",flags).SetValue(game,true);
            Call("SearchStarted",0);AssertScreen("Practice · Active search");
            typeof(FindObjectGameManager).GetField("<IsPractice>k__BackingField",flags).SetValue(game,false);
            foreach(string screen in new[]{"Waiting to start three-dot calibration","Calibrating dot 1","Calibrating dot 2","Calibrating dot 3","Independent five-dot accuracy check","Accuracy checked; continue to activation practice","Look-and-select activation exercise","Instructions · center and begin","Practice complete · confirm understanding"})
            {status.Set("preparing",screen);AssertScreen(screen);}
            var snapshot=typeof(HeadsetPreparationStatus).GetField("m_Status",flags).GetValue(status);
            var validationField=snapshot.GetType().GetField("validation");
            for(int dot=0;dot<5;dot++)
            {
                validationField.SetValue(snapshot,new EyeValidationStatus{active=true,stage="running",step=dot,count=5});
                Call("UpdateMonitor",selector,"Verification");
                var value=JObject.Parse(JsonUtility.ToJson(snapshot));
                if((string)value["screen"]!="Verification · running · dot "+(dot+1)+" of 5")throw new Exception("Verification dot label changed");
                results.Add(value);
            }
            validationField.SetValue(snapshot,null);
            for(int trial=0;trial<96;trial++)
            {
                typeof(FindObjectGameManager).GetField("m_CurrentRound",flags).SetValue(game,trial);
                typeof(FindObjectGameManager).GetField("<PresentedRoundIndex>k__BackingField",flags).SetValue(game,trial);
                Call("Transition",trial,"Blue","Cube");AssertScreen("Trial "+(trial+1)+" of 96 · Readiness cross");
                Call("Readiness","readiness_completed");AssertScreen("Opening cue");
                Call("SearchStarted",trial);AssertScreen("Active search");
                Call(trial%2==0?"Selected":"TimedOut",trial);AssertScreen(trial%2==0?"Selection feedback":"Timeout feedback");
                if(trial==47){Call("Checkpoint","block_end");AssertScreen("Block break");Call("Checkpoint","block_start");AssertScreen("Block 2");}
            }
            typeof(FindObjectGameManager).GetField("m_CurrentRound",flags).SetValue(game,96);
            Call("Completed",100f);AssertScreen("Finished · survey handoff");
            if((int)results[results.Count-1]["trialsCompleted"]!=96)throw new Exception("Completion count must include timeouts");
            Call("OnApplicationPause",true);AssertScreen("paused / sleeping");Call("OnApplicationPause",false);
            Call("OnApplicationFocus",false);AssertScreen("out of focus");
            var lines=File.ReadAllLines(Path.Combine(root,"session-"+assignment.id,"screen-transitions.jsonl"));
            if(lines.Length<390)throw new Exception("Missing timestamped transitions");
            string output=Environment.GetEnvironmentVariable("FMV_MONITOR_RECEIPT");
            if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject {["development"]=true,["states"]=results,["transitions"]=lines.Length}.ToString());
            Debug.Log("[StudySessionMonitorChecks] PASS: synthetic 96-trial screen flow, setup, checks, feedback, both blocks, completion, pause/focus and timestamped transitions; no headset or participant data.");
        }
        finally {UnityEngine.Object.DestroyImmediate(host);Directory.Delete(root,true);}
    }
}
