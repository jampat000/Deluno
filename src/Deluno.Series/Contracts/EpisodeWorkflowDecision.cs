namespace Deluno.Series.Contracts;

public sealed record EpisodeWorkflowDecision(
    string WantedStatus,
    string Reason,
    bool IsReplacementAllowed,
    int? QualityDelta,
    string? CurrentQuality,
    string? TargetQuality);
