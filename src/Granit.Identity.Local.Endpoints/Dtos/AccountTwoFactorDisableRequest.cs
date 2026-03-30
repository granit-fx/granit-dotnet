namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for disabling 2FA (requires password confirmation as step-up authentication).
/// </summary>
/// <param name="Password">The user's current password for confirmation.</param>
public sealed record AccountTwoFactorDisableRequest(string Password);
