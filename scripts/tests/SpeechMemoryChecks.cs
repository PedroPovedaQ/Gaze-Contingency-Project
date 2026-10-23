using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpatialVoice;

partial class SpatialVoiceChecks
{
    static void CheckSpeechMemory()
    {
        var memory = new SpeechDecisionMemory();
        var state = new GuidanceStateSnapshot { phase="Turning", capturedAtSeconds=1, allowedAction="Direction" };
        memory.Observe(state);
        state.capturedAtSeconds=2; memory.Observe(state);
        Check(((JArray)memory.Snapshot(2)["transitions"]).Count==1, "Same phase does not flood memory");
        memory.Started("Look right.", "tip", 2, state);
        var frozen=memory.Snapshot(2.1);
        state.phase="Refining"; state.capturedAtSeconds=3; memory.Observe(state);
        memory.Ended(3, true);
        Check((string)frozen["speech"][0]["outcome"]=="playback_started", "Async snapshot detached from end updates");
        Check((string)memory.Snapshot(4)["speech"][0]["outcome"]=="interrupted", "Interrupted is not completed");
        Check((string)memory.Snapshot(4)["speech"][0]["phase"]=="Turning", "Snapshot detached from mutable phase");
        memory.Ended(4, false);
        Check((string)memory.Snapshot(4)["speech"][0]["outcome"]=="interrupted", "Duplicate end cannot rewrite interruption");
        for(int i=0;i<10;i++) { memory.Started("Look left.", "tip", 4+i, state); memory.Ended(4.5+i,false); }
        Check(((JArray)memory.Snapshot(14)["speech"]).Count==4, "Memory bounded to four starts");
        Check(((JArray)memory.Snapshot(40)["speech"]).Count==0, "Old speech expires");
        Check(((JArray)memory.Snapshot(0)["speech"]).Count==0, "Future entries excluded");
        memory.Clear();
        Check(((JArray)memory.Snapshot(14)["speech"]).Count==0 && ((JArray)memory.Snapshot(14)["transitions"]).Count==0,
            "Guidance reset clears speech and transitions");

        foreach(JObject probe in MemoryProbes())
        {
            var old=(JObject)probe["payloads"]["baseline"];
            var compact=(JObject)probe["payloads"]["compact"];
            var remembered=(JObject)probe["payloads"]["compact_memory"];
            foreach(var choice in ((JObject)old["questions"]["navigation_phrase"]["criteria"]).Properties())
            {
                var reconstructed=(JObject)compact["state"]["candidate_semantics_defaults"].DeepClone();
                foreach(var field in ((JObject)compact["questions"]["navigation_phrase"]["criteria"][choice.Name]["semantics"]).Properties())
                    reconstructed[field.Name]=field.Value.DeepClone();
                Check(JToken.DeepEquals(reconstructed,choice.Value["semantics"]), "Factored semantics reconstruct exactly");
            }
            Check(JToken.DeepEquals(old["state"]["runtime"]["evidence"],remembered["state"]["runtime"]["evidence"]),
                "No current evidence lost in optimized payload");
            Check(compact.ToString(Formatting.None).Length<old.ToString(Formatting.None).Length,
                "Factoring reduces request size");
            Check(remembered["state"]["runtime"]["playback"]["recent_history"]["speech"].HasValues,
                "Actual playback history reaches production builder");
        }
    }

    static JArray MemoryProbes()
    {
        var result=new JArray();
        for(int i=0;i<9;i++)
        {
            bool left=i%2==0, invalid=i==4, speaking=i==5, repeated=i==6, busy=i==7;
            string direction=left?"left":"right", opposite=left?"right":"left";
            var choices=new List<NavigationPhrase>();
            foreach(string text in new[] {"Look "+direction+".","Look slightly "+direction+".","Okay, look "+direction+".","A little more "+direction+".","Turn "+direction+"."})
                choices.Add(new NavigationPhrase {text=text,tone=NavigationTone.Direct,
                    metadata=new SpeechPhraseMetadata {intent="direction",horizontal=direction,vertical="none",
                        requires=new[]{"fresh_valid_gaze","qualified_direction"},canBeSupersededBy=new[]{"target_acquired","direction_reversed","target_wall_reached"},
                        interruptPolicy="replace_if_misleading"}});
            var phase=new GuidanceStateSnapshot {phase="Turning",previousPhase="Suspended",transitionReason="valid_gaze_recovered",
                allowedAction=i==8?"None":"Direction",newGuidanceAllowed=!invalid && i!=8,
                capturedAtSeconds=90,gazeValid=!invalid,controllerValid=true,revision=3};
            var memory=new SpeechDecisionMemory(); memory.Observe(phase);
            string last="Look "+(repeated?direction:opposite)+".";
            memory.Started(last,"tip",repeated?98:92,phase); memory.Ended(repeated?99:93,i==3);
            var runtime=new JObject {
                ["evidence"]=new SpeechGuidanceEvidence {valid=!invalid,geometryAvailable=true,phase="Turning",guidanceState=phase,
                    yawErrorDegrees=left?-45:45,gazeAgeSeconds=.01,controllerEvidenceValid=true,controllerAgeSeconds=.01}.Snapshot(),
                ["playback"]=new JObject {["is_speaking"]=speaking,["is_busy"]=speaking||busy,
                    ["current_phrase"]=speaking?last:null,["pending_phrase"]=busy?"Look "+direction+".":null,
                    ["last_spoken_phrase"]=last},
                ["seconds_since_speech_ended"]=repeated?1:7,["locally_qualified"]=!invalid,
                ["deferred_seconds"]=0,["maximum_deferral_seconds"]=1 };
            string state=SpeechNavigationState.Create(left?-45:45,3,"Direct",last,"Coarse horizontal guidance; target wall not reached.",runtime.ToString());
            var baseline=JObject.Parse(JevDecisionClient.GuidancePayload(state,choices,false));
            var compact=JObject.Parse(JevDecisionClient.GuidancePayload(state,choices,true));
            runtime["playback"]["recent_history"]=memory.Snapshot(100);
            string memoryState=SpeechNavigationState.Create(left?-45:45,3,"Direct",last,"Coarse horizontal guidance; target wall not reached.",runtime.ToString());
            result.Add(new JObject {["id"]="production_contract_"+i,["expected_transmit"]=!(invalid||speaking||repeated||busy||i==8),
                ["payloads"]=new JObject {["baseline"]=baseline,["compact"]=compact,
                    ["compact_memory"]=JObject.Parse(JevDecisionClient.GuidancePayload(memoryState,choices))}});
        }
        return result;
    }

    static void ExportMemoryProbes(string path) => File.WriteAllText(path,MemoryProbes().ToString(Formatting.Indented));
}
