namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for password reset.
/// </summary>
/// <param name="UserId">The user's identifier.</param>
/// <param name="Token">The password reset token from the email.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record AccountPasswordResetRequest(
    string UserId,
    string Token,
    string NewPassword);
