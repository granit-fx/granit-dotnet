namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for profile update.
/// </summary>
/// <param name="FirstName">The new first name (max 256).</param>
/// <param name="LastName">The new last name (max 256).</param>
public sealed record AccountProfileUpdateRequest(
    string? FirstName,
    string? LastName);
