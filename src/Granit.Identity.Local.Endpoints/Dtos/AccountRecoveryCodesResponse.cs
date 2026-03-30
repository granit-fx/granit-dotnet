namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Response DTO for newly generated recovery codes.
/// </summary>
/// <param name="RecoveryCodes">The generated recovery codes.</param>
public sealed record AccountRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
