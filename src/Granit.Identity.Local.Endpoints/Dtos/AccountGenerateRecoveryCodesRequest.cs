namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for generating new recovery codes (requires password confirmation as step-up authentication).
/// </summary>
/// <param name="Password">The user's current password for confirmation.</param>
public sealed record AccountGenerateRecoveryCodesRequest(string Password);
