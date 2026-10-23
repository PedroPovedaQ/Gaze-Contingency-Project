using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    /// <summary>Bounded, trial-scoped evidence for advice, never authority to speak.
    /// Unity's main thread writes actual software playback and phase transitions.
    /// Snapshots are detached before async work. No proposals or inferred behavior.</summary>
    public sealed class SpeechDecisionMemory
    {
        public const int Capacity = 4;
        public const double WindowSeconds = 15;
        readonly Queue<JObject> m_Speech = new Queue<JObject>();
        readonly Queue<JObject> m_Transitions = new Queue<JObject>();
        JObject m_Active;
        string m_Phase;

        public void Clear()
        {
            m_Speech.Clear(); m_Transitions.Clear(); m_Active = null; m_Phase = null;
        }

        public void Observe(GuidanceStateSnapshot state)
        {
            if (state == null || state.phase == m_Phase) return;
            m_Phase = state.phase;
            Add(m_Transitions, new JObject {
                ["at_seconds"] = state.capturedAtSeconds, ["phase"] = state.phase,
                ["previousPhase"] = state.previousPhase, ["reason"] = state.transitionReason
            });
        }

        public void Started(string text, string context, double now, GuidanceStateSnapshot state)
        {
            Ended(now, interrupted: true);
            m_Active = new JObject {
                ["at_seconds"] = now, ["text"] = text, ["context"] = context,
                ["phase"] = state?.phase, ["action"] = state?.allowedAction,
                ["outcome"] = "playback_started"
            };
            Add(m_Speech, m_Active);
        }

        public void Ended(double now, bool interrupted)
        {
            if (m_Active == null) return;
            m_Active["end_seconds"] = now;
            m_Active["outcome"] = interrupted ? "interrupted" : "completed";
            m_Active = null;
        }

        public JObject Snapshot(double now) => new JObject {
            ["schema"] = "speech-memory-v1",
            ["scope"] = "since_guidance_reset",
            ["window_seconds"] = WindowSeconds,
            ["speech"] = Recent(m_Speech, now),
            ["transitions"] = Recent(m_Transitions, now)
        };

        static void Add(Queue<JObject> queue, JObject item)
        {
            if (queue.Count == Capacity) queue.Dequeue();
            queue.Enqueue(item);
        }

        static JArray Recent(Queue<JObject> queue, double now)
        {
            var result = new JArray();
            foreach (var item in queue)
            {
                double age = now - (double)item["at_seconds"];
                if (age < 0 || age > WindowSeconds || double.IsNaN(age)) continue;
                var copy = (JObject)item.DeepClone();
                copy.Remove("at_seconds");
                copy["age_seconds"] = Math.Round(age, 3);
                if (copy["end_seconds"] != null)
                {
                    copy["ended_age_seconds"] = Math.Round(Math.Max(0, now - (double)copy["end_seconds"]), 3);
                    copy.Remove("end_seconds");
                }
                result.Add(copy);
            }
            return result;
        }
    }
}
