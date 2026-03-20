namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Response body for the password change timestamp query.
/// </summary>
public sealed record IdentityPasswordChangedAtResponse(DateTimeOffset? ChangedAt);
