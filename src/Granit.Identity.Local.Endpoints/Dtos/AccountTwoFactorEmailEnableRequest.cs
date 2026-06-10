namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for enabling the email one-time-code two-factor method.
/// </summary>
/// <param name="Code">The 6-digit code previously sent to the user's email address.</param>
public sealed record AccountTwoFactorEmailEnableRequest(string Code);
