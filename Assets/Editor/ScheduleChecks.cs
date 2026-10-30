using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Synthetic schedule, pinned geometry and actual block-break regression.</summary>
public static class ScheduleChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Set(object target, string name, object value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [MenuItem("Tools/Codex/Check 64 Trial Schedule")]
    public static void Run()
    {
        string participant = SessionConfig.ParticipantId;
        var blocks = typeof(SessionConfig).GetField("<VoiceBlocksEnabled>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        bool enabled = SessionConfig.VoiceBlocksEnabled;
        var quickProperties = new[] { "NeutralQuickTest", "CloneQuickTest" }
            .Select(name => typeof(SessionConfig).GetProperty(name)).ToArray();
        var quickValues = quickProperties.Select(property => property.GetValue(null)).ToArray();
        int debugOverride = ChallengeSet.DebugRoundCountOverride;
        var host = new GameObject("ScheduleFixture"); host.SetActive(false);
        StudyCheckpoint checkpoint = null;
        try
        {
            SessionConfig.ParticipantId = "SCHEDULE_V8_SYNTHETIC";
            foreach (var property in quickProperties) property.SetValue(null, false);
            ChallengeSet.DebugRoundCountOverride = 0;
            blocks.SetValue(null, true);
            Check(ChallengeSet.TotalRounds == 64 && ChallengeSet.PracticeTrialCount == 1, "64 measured plus one practice");
            var rounds = ChallengeSet.Rounds;
            foreach (var round in rounds)
            {
                const float radius = 1.5f;
                float min = RotationalSearchLayout.DefaultMinHeight, max = RotationalSearchLayout.DefaultMaxHeight;
                var rotation = Quaternion.Euler(0, round.startPlane * 45, 0);
                var cross = rotation * (Vector3.forward * radius + Vector3.right *
                    (round.crossOffsetFraction * RotationalSearchLayout.UsableHalfWidth(radius)));
                float height = Mathf.Lerp(min, max, round.targetHeightFraction);
                Check(RotationalSearchLayout.TryTarget(radius, 0, 0, cross.x, cross.z, round.SignedTheta,
                    round.targetPlane, round.targetSlot, height, out var target), "Assigned bearing fits its wall");
                var slots = RotationalSearchLayout.Build(radius, min, max, round.layoutSeed, target);
                var actual = slots[round.targetPlane * 21 + round.targetSlot];
                Check(slots.Length == 168 && Mathf.Abs(actual.y - height) < .00001f,
                    "Pinned target retains assigned height among 168 objects");
                Check((actual.y > (min + max) / 2) == (round.targetHeightFraction > .5f), "Realized height side matches cross");
            }
            for (int block = 0; block < 2; block++)
            {
                var subset = rounds.Where(r => r.blockIndex == block).ToArray();
                Check(subset.Count(r => r.targetHeightFraction > .5f) == 16, "16 above / 16 below");
                foreach (int angle in new[] { 0,45,-45,90,-90,135,-135,180 })
                {
                    var group = subset.Where(r => r.SignedTheta == angle).ToArray();
                    int above = group.Count(r => r.targetHeightFraction > .5f);
                    Check(group.Length == 4 && above == 2, "Four per signed step with 2/2 heights");
                    Debug.Log($"[ScheduleChecks] ALLOCATION block={block} signed={angle} above={above} below={4-above}");
                }
            }
            string export = Environment.GetEnvironmentVariable("SCHEDULE_CHECK_EXPORT");
            if (!string.IsNullOrEmpty(export))
            {
                Directory.CreateDirectory(export);
                using (var writer = new StreamWriter(Path.Combine(export, "synthetic-trial-schedule.csv"))) ChallengeSet.WriteSchedule(writer);
            }
            var ui = host.AddComponent<FindObjectUI>();
            var game = host.AddComponent<FindObjectGameManager>();
            checkpoint = host.AddComponent<StudyCheckpoint>();
            Set(game, "m_UI", ui); Set(game, "m_Checkpoint", checkpoint); Set(game, "m_UseRotationalLayout", true);
            var transition = typeof(FindObjectGameManager).GetMethod("TransitionToNextRound", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (int index in new[] { 31,32,40 })
            {
                Set(checkpoint, "<Waiting>k__BackingField", false);
                Set(game, "m_CurrentRound", index);
                var routine = (System.Collections.IEnumerator)transition.Invoke(game, null);
                Check(routine.MoveNext(), "Transition starts");
                Check(checkpoint.Waiting == (index == 32), "Break occurs only after trial 32, never at old boundary 40");
                (routine as IDisposable)?.Dispose();
            }
        }
        finally
        {
            if (checkpoint != null)
            {
                var panel = (GameObject)typeof(StudyCheckpoint).GetField("m_Panel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(checkpoint);
                if (panel != null) UnityEngine.Object.DestroyImmediate(panel);
            }
            UnityEngine.Object.DestroyImmediate(host);
            blocks.SetValue(null, enabled); SessionConfig.ParticipantId = participant;
            for (int i = 0; i < quickProperties.Length; i++) quickProperties[i].SetValue(null, quickValues[i]);
            ChallengeSet.DebugRoundCountOverride = debugOverride;
        }
        Debug.Log("[ScheduleChecks] PASS: 64-trial v8 schedule, realized pinned geometry, height allocation and actual 32-trial block break.");
    }
}
