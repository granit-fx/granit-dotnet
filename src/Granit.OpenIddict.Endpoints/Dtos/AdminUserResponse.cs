namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for user administration endpoints.
/// </summary>
/// <param name="UserId">The user's unique identifier.</param>
/// <param name="Username">The user's login name.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
/// <param name="Enabled">Whether the user account is active (not locked out).</param>
/// <param name="ExtraProperties">Application-defined extension properties.</param>
public sealed record AdminUserResponse(
    string UserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    IReadOnlyDictionary<string, string> ExtraProperties);
