namespace Granit.Hostnames.Endpoints.Dtos;

/// <summary>
/// Result of an availability pre-check for a hostname.
/// </summary>
/// <param name="Host">The normalised hostname that was checked.</param>
/// <param name="IsAvailable">
/// <c>true</c> if no active registration exists for this hostname and it can be registered;
/// <c>false</c> if it is already taken by another owner.
/// </param>
public sealed record HostnameAvailabilityResponse(string Host, bool IsAvailable);
