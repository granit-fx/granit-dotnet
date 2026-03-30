namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for account deletion (GDPR right to erasure).
/// </summary>
/// <param name="Password">The user's current password for confirmation.</param>
public sealed record AccountDeleteRequest(string Password);
