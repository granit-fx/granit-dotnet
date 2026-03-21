namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for the user profile.
/// </summary>
/// <param name="UserId">The user's identifier.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="EmailConfirmed">Whether the email has been confirmed.</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
/// <param name="TwoFactorEnabled">Whether TOTP 2FA is enabled.</param>
/// <param name="HasPassword">Whether the user has a password set.</param>
/// <param name="ExternalLogins">Linked external login providers.</param>
public sealed record AccountProfileResponse(
    Guid UserId,
    string Email,
    bool EmailConfirmed,
    string? FirstName,
    string? LastName,
    bool TwoFactorEnabled,
    bool HasPassword,
    IReadOnlyList<string> ExternalLogins);

/// <summary>
/// Request DTO for profile update.
/// </summary>
/// <param name="FirstName">The new first name (max 256).</param>
/// <param name="LastName">The new last name (max 256).</param>
public sealed record AccountProfileUpdateRequest(
    string? FirstName,
    string? LastName);
