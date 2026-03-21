namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for password change.
/// </summary>
/// <param name="CurrentPassword">The user's current password.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record AccountPasswordChangeRequest(
    string CurrentPassword,
    string NewPassword);

/// <summary>
/// Request DTO for forgot password.
/// </summary>
/// <param name="Email">The user's email address.</param>
public sealed record AccountForgotPasswordRequest(string Email);

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
