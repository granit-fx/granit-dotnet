namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Response DTO after enabling 2FA with recovery codes.
/// </summary>
/// <param name="RecoveryCodes">The single-use recovery codes.</param>
public sealed record AccountTwoFactorEnableResponse(IReadOnlyList<string> RecoveryCodes);
