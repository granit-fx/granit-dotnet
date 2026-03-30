namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Response returning the current opt-out status for the requesting user or visitor.
/// </summary>
public sealed record PrivacyOptOutStatusResponse(
    bool IsOptedOut,
    DateTimeOffset? OptedOutAt,
    string? Regulation);
