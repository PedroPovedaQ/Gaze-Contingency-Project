using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    /// <summary>Authored meaning, separate from wording and audio-cache identity.
    /// Unknown text stays unknown; never guess that the word "right" means a right turn.</summary>
    [Serializable]
    public sealed class SpeechPhraseMetadata
    {
        public string intent = "unknown", horizontal = "none", vertical = "none";
        public string magnitude = "unspecified", stage = "search", interruptPolicy = "finish";
        public string[] requires = Array.Empty<string>(), canBeSupersededBy = Array.Empty<string>();
        static readonly Dictionary<string, SpeechPhraseMetadata> Phrases = Build();

        public static SpeechPhraseMetadata Describe(string text) => text != null && Phrases.TryGetValue(text, out var value)
            ? value : new SpeechPhraseMetadata();
        public static void Register(string text, SpeechPhraseMetadata metadata)
        {
            if (!string.IsNullOrEmpty(text) && metadata != null) Phrases[text] = metadata;
        }
        public static bool CanReplace(string current, string proposed) =>
            Describe(current).interruptPolicy == "replace_if_misleading" && Describe(proposed).intent != "unknown";
        static SpeechPhraseMetadata Direction(string direction, string intent, string magnitude = "unspecified")
        {
            return new SpeechPhraseMetadata {
                intent = intent, magnitude = magnitude, interruptPolicy = "replace_if_misleading",
                horizontal = direction == DirectionalHintPolicy.LeftPhrase || direction == DirectionalHintPolicy.UpLeftPhrase || direction == DirectionalHintPolicy.DownLeftPhrase ? "left" :
                    direction == DirectionalHintPolicy.RightPhrase || direction == DirectionalHintPolicy.UpRightPhrase || direction == DirectionalHintPolicy.DownRightPhrase ? "right" : "none",
                vertical = direction == DirectionalHintPolicy.UpPhrase || direction == DirectionalHintPolicy.UpLeftPhrase || direction == DirectionalHintPolicy.UpRightPhrase ? "up" :
                    direction == DirectionalHintPolicy.DownPhrase || direction == DirectionalHintPolicy.DownLeftPhrase || direction == DirectionalHintPolicy.DownRightPhrase ? "down" : "none",
                requires = new[] { "fresh_valid_gaze", intent == "return_to_target" ? "verified_target_departure" : "qualified_direction" },
                canBeSupersededBy = new[] { "target_acquired", "direction_reversed", "target_wall_reached" }
            };
        }
        static Dictionary<string, SpeechPhraseMetadata> Build()
        {
            var result = new Dictionary<string, SpeechPhraseMetadata>(StringComparer.Ordinal);
            var directions = new[] { DirectionalHintPolicy.LeftPhrase, DirectionalHintPolicy.RightPhrase,
                DirectionalHintPolicy.UpPhrase, DirectionalHintPolicy.DownPhrase, DirectionalHintPolicy.UpLeftPhrase,
                DirectionalHintPolicy.UpRightPhrase, DirectionalHintPolicy.DownLeftPhrase, DirectionalHintPolicy.DownRightPhrase,
                DirectionalHintPolicy.AlignedPhrase };
            foreach (var direction in directions)
            {
                foreach (float fraction in new[] { .1f, .3f, .7f })
                {
                    string magnitude = fraction <= .2f ? "small" : fraction <= .5f ? "medium" : "large";
                    for (int i = 0; i < 8; i++)
                    {
                        result[DirectionalHintPolicy.Variant(direction, i, verticalFraction: fraction)] = Direction(direction, "direction", DirectionalHintPolicy.HasVertical(direction) ? magnitude : "unspecified");
                        result[DirectionalHintPolicy.Variant(direction, i, far: true, verticalFraction: fraction)] = Direction(direction, "direction", DirectionalHintPolicy.HasVertical(direction) ? magnitude : "large");
                        var arrival = Direction(direction, "area_arrival", DirectionalHintPolicy.HasVertical(direction) ? magnitude : "unspecified");
                        arrival.stage = "within_target_wall"; arrival.requires = new[] { "fresh_valid_gaze", "stable_wall_entry" };
                        result[DirectionalHintPolicy.AreaEntry(direction, i, fraction)] = arrival;
                    }
                    if (direction == DirectionalHintPolicy.LeftPhrase || direction == DirectionalHintPolicy.RightPhrase || direction == DirectionalHintPolicy.UpPhrase || direction == DirectionalHintPolicy.DownPhrase)
                        result[DirectionalHintPolicy.PassedTarget(direction, fraction)] = Direction(direction, "return_to_target", DirectionalHintPolicy.HasVertical(direction) ? magnitude : "small");
                }
                if (direction != DirectionalHintPolicy.AlignedPhrase)
                    result[DirectionalHintPolicy.ReturnToTarget(direction)] = Direction(direction, "return_to_target");
            }
            foreach (var direction in new[] { DirectionalHintPolicy.LeftPhrase, DirectionalHintPolicy.RightPhrase })
            {
                result[DirectionalHintPolicy.Turn(direction, 180, 0)] = Direction(direction, "turn", "large");
                result[DirectionalHintPolicy.ReturnToArea(direction)] = Direction(direction, "return_to_target");
            }
            for (int i = 0; i < 4; i++)
            {
                result[DirectionalHintPolicy.Variant(DirectionalHintPolicy.LookingPhrase, i)] = new SpeechPhraseMetadata {
                    intent = "target_confirmation", stage = "target_acquired", requires = new[] { "qualified_target_gaze" } };
                result[DirectionalHintPolicy.Variant(DirectionalHintPolicy.AreaPhrase, i)] = new SpeechPhraseMetadata {
                    intent = "area_arrival", stage = "within_target_wall", requires = new[] { "stable_wall_entry" } };
                result[DirectionalHintPolicy.Variant(DirectionalHintPolicy.ReturnPhrase, i)] = new SpeechPhraseMetadata {
                    intent = "return_to_target", requires = new[] { "verified_target_departure" } };
                result[DirectionalHintPolicy.Progress(i)] = new SpeechPhraseMetadata {
                    intent = "progress_encouragement", requires = new[] { "new_turn_milestone" } };
            }
            foreach (var text in new[] { DirectionalHintPolicy.BitMorePhrase, DirectionalHintPolicy.LittleMorePhrase })
                result[text] = new SpeechPhraseMetadata { intent = "progress_encouragement", magnitude = "small",
                    stage = "within_target_wall", requires = new[] { "recent_explicit_direction", "same_direction", "measured_progress", "small_remaining_error", "continuation_not_already_used" } };
            result[DirectionalHintPolicy.PointingPhrase] = new SpeechPhraseMetadata {
                intent = "target_confirmation", stage = "target_acquired", requires = new[] { "controller_target_hit", "qualified_target_gaze" } };
            result[DirectionalHintPolicy.SelectionReminderPhrase] = new SpeechPhraseMetadata {
                intent = "selection_reminder", stage = "target_acquired", requires = new[] { "sustained_target_gaze" } };
            foreach (var direction in new[] { DirectionalHintPolicy.LeftPhrase, DirectionalHintPolicy.RightPhrase, DirectionalHintPolicy.UpPhrase, DirectionalHintPolicy.DownPhrase, DirectionalHintPolicy.AlignedPhrase })
                result[DirectionalHintPolicy.Opening(direction)] = new SpeechPhraseMetadata { intent = "trial_opening", stage = "before_search" };
            foreach (float yaw in new[] { -30f, 0f, 30f })
                foreach (float pitch in new[] { -20f, 0f, 20f })
                    result[DirectionalHintPolicy.ReadinessReminder(yaw, pitch)] = new SpeechPhraseMetadata { intent = "readiness_reminder", stage = "between_trials" };
            return result;
        }
    }

    /// <summary>Small, identifier-free snapshot. Geometry is measured locally, not inferred by Jev.</summary>
    [Serializable]
    public sealed class SpeechGuidanceEvidence
    {
        public bool valid, insideTargetWall, targetGazeQualified, controllerOnTarget, geometryAvailable, verticalMeasured;
        public double gazeAgeSeconds;
        public float yawErrorDegrees, pitchErrorDegrees, verticalPlaneFraction;
    }

    public static class SpeechInterruptionState
    {
        public static string Create(string current, string proposed, string reason, double elapsed, double remaining,
            SpeechGuidanceEvidence evidence, string context = "tip", string speechStartStage = "unknown") => new JObject {
                ["schema"] = "speech-interruption-v2", ["task"] = "seated_hidden_target_guidance",
                ["current"] = Phrase(current, speechStartStage), ["proposed"] = Phrase(proposed),
                ["relationship"] = new JObject { ["arrival_already_acknowledged_by_current_stage"] = GuidanceSpeechPolicy.IsRedundantArrival(reason, speechStartStage) },
                ["event"] = new JObject { ["kind"] = reason, ["meaning"] = Meaning(reason) },
                ["evidence"] = evidence == null ? (JToken)JValue.CreateNull() : JObject.FromObject(evidence),
                ["playback"] = new JObject { ["context"] = context, ["elapsed_seconds"] = elapsed,
                    ["speech_remaining_seconds"] = remaining, ["word_boundary_known"] = false },
                ["local_limits"] = new JObject { ["minimum_elapsed_seconds"] = .65,
                    ["protected_ending_seconds"] = 1.0, ["fade_seconds"] = GuidanceSpeechPolicy.FadeSeconds,
                    ["response_deadline_seconds"] = GuidanceSpeechPolicy.AdviceDeadline,
                    ["revalidate_before_and_after_fade"] = true }
            }.ToString(Newtonsoft.Json.Formatting.None);
        static JObject Phrase(string text, string startedInStage = "not_started") => new JObject { ["text"] = text, ["started_in_stage"] = startedInStage, ["semantics"] = JObject.FromObject(SpeechPhraseMetadata.Describe(text)) };
        static string Meaning(string reason)
        {
            switch (reason)
            {
                case "gaze_target_plane": case "stable_wall_entry": return "The listener has reached the target wall; horizontal turning to another wall is now obsolete.";
                case "gaze_target_vicinity_hold": return "Gaze has acquired the target: it uniquely wins the nearby-object comparison and meets the gaze hold. Search movement should stop.";
                case "controller_target_gaze_vicinity": return "The controller is on the target for the required hold with gaze nearby. Search movement should stop; selection still needs the trigger.";
                case "target_bearing_overshoot": return "Valid gaze history crossed the target bearing. The proposed correction points back toward it.";
                case "recent_target_departure": return "The listener was looking at the target and has since looked away. The proposed correction points back toward it.";
                case "gaze_overshoot_return": return "The listener left the correct wall after a stable visit. The proposed correction points back to it.";
                default: return "No qualified interruption event; finish the current phrase.";
            }
        }
    }
}
