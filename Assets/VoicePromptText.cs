using System;
using System.Collections.Generic;

/// <summary>Voice wording perspective used by the phrase library and pilot.</summary>
public enum VoicePerspective
{
    Collaborative,
    External
}

/// <summary>Stable wording transforms for the two voice perspectives.</summary>
public static class VoicePromptText
{
    public const string Version = "perspective-v2-controller";

    // Neutral/external source phrase to collaborative equivalent. Keeping this
    // table explicit preserves meaning across every live hint and terminal line.
    static readonly Dictionary<string, string> k_Collaborative = new Dictionary<string, string>
    {
        { "You're very close.", "We're very close." },
        { "Stay with this area.", "Let's stay with this area." },
        { "You're right where you need to be.", "We're right where we need to be." },
        { "Excellent. Stay with this area.", "Excellent. Let's stay with this area." },
        { "Great, you're very close. Keep your focus here.", "Great, we're very close. Let's keep our focus here." },
        { "You're nearly on it. Stay with this area.", "We're nearly on it. Let's stay with this area." },
        { "You're on the right track.", "We're on the right track." },
        { "You're getting closer. Keep searching this direction.", "We're getting closer. Let's keep searching this direction." },
        { "Good progress. You're moving toward it.", "Good progress. We're moving toward it." },
        { "This direction looks better. Keep going.", "This direction looks better. Let's keep going." },
        { "You're in a better area now. Keep scanning.", "We're in a better area now. Let's keep scanning." },
        { "Nice adjustment. You're getting warmer.", "Nice adjustment. We're getting warmer." },
        { "You're close. Keep working this side.", "We're close. Let's keep working this side." },
        { "Much better. Stay focused and keep scanning.", "Much better. Let's stay focused and keep scanning." },
        { "You're narrowing it down. Keep at it.", "We're narrowing it down. Let's keep at it." },
        { "Good path. Keep looking around here.", "Good path. Let's keep looking around here." },
        { "You're way off right now.", "We're way off right now." },
        { "Look in a different area.", "Let's look in a different area." },
        { "This area isn't working. Shift your search.", "This area isn't working. Let's shift our search." },
        { "Not here. Try a different area.", "Not here. Let's try a different area." },
        { "You're off target. Move your search.", "We're off target. Let's move our search." },
        { "Let's switch areas and try again.", "Let's switch areas and try again." },
        { "Wrong area right now. Reposition your search.", "Wrong area right now. Let's reposition our search." },
        { "Don't stay here. Check another area.", "Let's check another area instead of staying here." },
        { "This isn't the zone. Move to a different area.", "This isn't the zone. Let's move to a different area." },
        { "Way off. Search somewhere else.", "We're way off. Let's search somewhere else." },
        { "You're getting colder. You're still on the right track, keep scanning.", "We're getting colder. We're still on the right track, let's keep scanning." },
        { "You're getting colder. You're close, but adjust a little.", "We're getting colder. We're close, but let's adjust a little." },
        { "You're getting colder. Keep searching this direction with small adjustments.", "We're getting colder. Let's keep searching this direction with small adjustments." },
        { "You're getting colder now. You're near it, just refine your search.", "We're getting colder now. We're near it, let's refine our search." },
        { "You're getting colder. Look in a different area.", "We're getting colder. Let's look in a different area." },
        { "You're getting colder now. Move your search elsewhere.", "We're getting colder now. Let's move our search elsewhere." },
        { "You're getting colder. This area is falling off.", "We're getting colder. This area is falling off." },
        { "You're getting colder. Shift to another area.", "We're getting colder. Let's shift to another area." },
        { "Nice!", "We did it!" },
        { "Locate the target by its color and shape. You're on the right track.", "Let's find the target by its color and shape. We're on the right track." },
        { "Excellent! You located all the objects. Please complete the NASA T L X questionnaire now.", "Excellent! We've found all the objects. Let's complete the NASA T L X questionnaire now." },
        { "Thank you for your participation in this experiment, please remove the headset now and have a great day", "We've finished the experiment. Let's remove the headset now and enjoy the rest of our day." },
    };

    static readonly Dictionary<string, string> k_External = new Dictionary<string, string>
    {
        { "Let's switch areas and try again.", "Switch areas and try again." },
    };

    /// <summary>Formats a known prompt for the requested perspective.</summary>
    public static string Format(string text, VoicePerspective perspective)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (!Enum.IsDefined(typeof(VoicePerspective), perspective))
            throw new ArgumentOutOfRangeException(nameof(perspective), perspective, "Unknown voice perspective.");
        if (perspective == VoicePerspective.Collaborative) return SelfSimilar(text);
        return k_External.TryGetValue(text, out var wording) ? wording : text;
    }

    /// <summary>Compatibility name for the legacy self-similar voice condition.</summary>
    public static string SelfSimilar(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (k_Collaborative.TryGetValue(text, out var wording)) return wording;
        if (text.StartsWith("Welcome to the surrounding search. ", StringComparison.Ordinal))
            return "Let's begin the surrounding search. Let's stay seated at the center. " +
                "We'll see objects on eight planes around us. Let's turn to find the target by its color and shape. " +
                "Let's point the controller ray at the matching object to highlight it, then press the trigger to select it. Our first two rounds are practice.";
        const string practice = "This is a practice round. It does not count toward the study. ";
        if (text.StartsWith(practice, StringComparison.Ordinal))
            return "Let's try a practice round. This one does not count toward our study rounds. " + SelfSimilar(text.Substring(practice.Length));
        if (text.StartsWith("Locate the ", StringComparison.Ordinal))
            return "Let's find the " + text.Substring("Locate the ".Length).ToLowerInvariant();
        if (text.StartsWith("Hi, I will guide you through this task. ", StringComparison.Ordinal))
            return "Let's work through this task together. " +
                "Let's find the target object by its color and shape as quickly and accurately as we can. " +
                "Let's point the controller ray at an object to highlight it, then press the trigger to select it. " +
                "Let's begin by tapping a nearby surface with the controller. " +
                "We'll see our goal in the center of the view each round.";
        return text;
    }
}
