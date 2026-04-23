namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for initiating an email change. Requires the user's current password
/// as step-up authentication so a stolen session cookie alone cannot pivot a
/// compromised account into a permanent take-over via email change → password reset.
/// </summary>
/// <param name="NewEmail">The desired new email address.</param>
/// <param name="CurrentPassword">The user's current password — required as step-up
/// authentication (OWASP ASVS V2.8.1).</param>
public sealed record AccountChangeEmailRequest(string NewEmail, string CurrentPassword);
