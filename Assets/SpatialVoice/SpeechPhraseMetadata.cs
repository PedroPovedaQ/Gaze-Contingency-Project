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
            result[DirectionalHintPolicy.TriggerReminderPhrase] = new SpeechPhraseMetadata {
                intent = "selection_reminder", stage = "target_acquired", requires = new[] { "sustained_target_gaze", "controller_target_hit" } };
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
            }
            foreach (var direction in DirectionalHintPolicy.ProgressDirections)
                for (int i = 0; i < 3; i++)
                {
                    var progress = Direction(direction, "progress_encouragement");
                    progress.interruptPolicy = "finish";
                    progress.requires = new[] { "fresh_valid_gaze", "same_direction", "measured_progress" };
                    result[DirectionalHintPolicy.Progress(direction, i)] = progress;
                }
            foreach (var text in new[] { DirectionalHintPolicy.BitMorePhrase, DirectionalHintPolicy.LittleMorePhrase })
                result[text] = new SpeechPhraseMetadata { intent = "progress_encouragement", magnitude = "small",
                    stage = "within_target_wall", requires = new[] { "recent_explicit_direction", "same_direction", "measured_progress", "small_remaining_error", "continuation_not_already_used" } };
            for (int i = 0; i < 3; i++)
                result[DirectionalHintPolicy.BriefProgress(i)] = new SpeechPhraseMetadata {
                    intent = "progress_encouragement", stage = "turning", interruptPolicy = "finish",
                    requires = new[] { "fresh_valid_gaze", "recent_explicit_direction", "same_direction",
                        "recent_measured_progress", "unconsumed_turn_milestone" } };
            result[DirectionalHintPolicy.PointingPhrase] = new SpeechPhraseMetadata {
                intent = "controller_confirmation", stage = "target_acquired",
                requires = new[] { "fresh_valid_gaze", "controller_target_hit", "qualified_controller_hold", "gaze_in_target_vicinity" } };
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
        public GuidanceStateSnapshot guidanceState;
        public bool valid, insideTargetWall, targetGazeQualified, controllerOnTarget, geometryAvailable, verticalMeasured;
        public bool wallEntryQualified, gazeWithinTargetVicinity, controllerEvidenceValid, controllerTargetHoldQualified;
        public bool vicinityAmbiguous, targetAcknowledged, targetReturnPending, continuationUsed;
        public string phase = "unavailable", gazeRayHit = "unavailable", controllerRayHit = "unavailable", previousDirection;
        public int controllerTriggerHeld = -1, vicinityCandidateCount;
        public double snapshotTimeSeconds, gazeAgeSeconds = -1, gazeReceiptAgeSeconds = -1, controllerAgeSeconds = -1, lastTargetSeenAgeSeconds = -1;
        public float yawErrorDegrees, pitchErrorDegrees, verticalPlaneFraction;
        public float vicinityErrorDegrees, vicinityRadiusDegrees;
        public bool vicinityMeasured;

        public JObject Snapshot()
        {
            var result = JObject.FromObject(this);
            // Missing or invalid observations are unknown, never zero-error evidence.
            if (!valid || !geometryAvailable) { result["yawErrorDegrees"] = null; result["pitchErrorDegrees"] = null; }
            if (!valid || !verticalMeasured) result["verticalPlaneFraction"] = null;
            if (!valid || !vicinityMeasured) { result["vicinityErrorDegrees"] = null; result["vicinityRadiusDegrees"] = null; }
            foreach (var key in new[] { "gazeAgeSeconds", "gazeReceiptAgeSeconds", "controllerAgeSeconds", "lastTargetSeenAgeSeconds" })
                if ((double)result[key] < 0 || double.IsNaN((double)result[key]) || double.IsInfinity((double)result[key])) result[key] = null;
            result["qualification_thresholds_seconds"] = new JObject {
                ["target_gaze"] = DirectionalHintPolicy.LookingHoldSeconds, ["controller_target"] = 1.0, ["target_wall"] = GuidanceSpeechPolicy.DirectionSettleSeconds };
            return result;
        }
    }

    public static class SpeechNavigationState
    {
        public static string Create(float angle, float distance, string tone, string lastPhrase, string behavior, string runtimeContext) =>
            new JObject { ["schema"] = "speech-navigation-v2", ["task"] = "seated_hidden_target_guidance",
                ["relative_yaw_degrees"] = angle, ["horizontal_distance_metres"] = distance,
                ["angle_convention"] = "negative left, positive right, zero forward",
                ["preferred_tone"] = tone, ["last_catalog_phrase"] = lastPhrase, ["behavior"] = behavior,
                ["runtime"] = runtimeContext == null ? (JToken)JValue.CreateNull() : JObject.Parse(runtimeContext),
                ["constraint"] = "Choose only an offered phrase. Unknown input is not evidence of alignment or acquisition. Local eligibility and revalidation remain authoritative."
            }.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static class SpeechInterruptionState
    {
        public static string Create(string current, string proposed, string reason, double elapsed, double remaining,
            SpeechGuidanceEvidence evidence, string context = "tip", string speechStartStage = "unknown", JObject playback = null) => new JObject {
                ["schema"] = "speech-interruption-v3", ["task"] = "seated_hidden_target_guidance",
                ["current"] = Phrase(current, speechStartStage), ["proposed"] = Phrase(proposed),
                ["relationship"] = new JObject { ["arrival_already_acknowledged_by_current_stage"] = GuidanceSpeechPolicy.IsRedundantArrival(reason, speechStartStage) },
                ["event"] = new JObject { ["kind"] = reason, ["meaning"] = Meaning(reason) },
                ["evidence"] = evidence == null ? (JToken)JValue.CreateNull() : evidence.Snapshot(),
                ["playback"] = playback ?? new JObject { ["is_speaking"] = true, ["context"] = context, ["elapsed_seconds"] = elapsed,
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
