namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for enabling 2FA.
/// </summary>
/// <param name="Code">The 6-digit TOTP code to verify.</param>
public sealed record AccountTwoFactorEnableRequest(string Code);
