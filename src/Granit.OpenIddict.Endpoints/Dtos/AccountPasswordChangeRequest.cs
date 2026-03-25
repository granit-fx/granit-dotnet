namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for password change.
/// </summary>
/// <param name="CurrentPassword">The user's current password.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record AccountPasswordChangeRequest(
    string CurrentPassword,
    string NewPassword);
