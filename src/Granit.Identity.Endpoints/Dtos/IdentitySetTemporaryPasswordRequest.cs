namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Request body for setting a temporary password for a user.
/// </summary>
public sealed record IdentitySetTemporaryPasswordRequest(string Password);
