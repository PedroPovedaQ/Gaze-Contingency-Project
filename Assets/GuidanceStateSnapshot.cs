using System;

/// <summary>Recorded local guidance state. Times use Unity realtime, not replay-relative time.
/// Missing snapshot means not recorded; empty hit with valid evidence means no object.</summary>
[Serializable]
public sealed class GuidanceStateSnapshot
{
    public string version = "guidance-state-v2";
    public AssistanceRuntimeSnapshot assistance;
    public string guidancePolicy; // Null in older recordings means unavailable, not baseline.
    public string phase, previousPhase, transitionReason, allowedAction;
    public long revision, observationSequence = -1;
    public double enteredAtSeconds, capturedAtSeconds;
    public bool gazeValid, controllerValid;
    // New automatic guidance only; does not mute setup/success or cut active speech.
    public bool newGuidanceAllowed;
    public string gazeHitObjectId, vicinityWinnerObjectId, controllerHitObjectId, targetObjectId;
}

/// <summary>Diagnostic provenance, not model input. A last decision is accepted advice, not audio onset.</summary>
[Serializable]
public sealed class AssistanceRuntimeSnapshot
{
    public const string LocalModelVersion = "20261026-minilm-reassurance-v8";
    public string mode, provider, modelVersion, policy, connection;
    public string lastDecisionId, lastDecisionProvider, lastStrategy, lastOutcome;
    public double capturedAtSeconds, lastDecisionAtSeconds;

    public string ProviderLabel => provider == "local_minilm" ? "Local MiniLM" : provider == "jev" ? "Jev" :
        provider == "baseline_rules" ? "Baseline rules" : "Not recorded";
    public string SelectionLabel => string.IsNullOrEmpty(mode) ? "Not recorded" :
        mode == "baseline" ? "Baseline · local rules" : "Adaptive · " + ProviderLabel;
    public string ConnectionLabel => connection == "not_checked" ? "Not checked" : connection == "not_required" ? "Not used" :
        connection == "not_configured" ? "Not configured" : connection == "checking" ? "Checking…" :
        connection == "configured" ? "Configured" : connection == "ready" ? "Ready" : connection == "unavailable" ? "Unavailable" : "Not recorded";
    public string StrategyLabel => lastStrategy == "observe_1s" ? "Observe for 1s" :
        lastStrategy == "orient_brief" ? "Brief direction" :
        lastStrategy == "orient_explicit" ? "Fuller direction" : lastStrategy == "refine_brief" ? "Brief refinement" :
        lastStrategy == "refine_explicit" ? "Precise refinement" : lastStrategy == "correct" ? "Correct direction" :
        lastStrategy == "encourage" ? "Reinforce progress" : "Unknown strategy";
    public string LastDecisionLabel => string.IsNullOrEmpty(lastDecisionId) ? "No accepted model decision" :
        StrategyLabel + " · " + (lastOutcome ?? "outcome not recorded") +
        " · " + Math.Max(0, capturedAtSeconds - lastDecisionAtSeconds).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s ago";
}
