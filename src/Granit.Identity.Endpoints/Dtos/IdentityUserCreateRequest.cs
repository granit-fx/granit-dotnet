namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Request body for creating a new user in the identity provider.
/// </summary>
public sealed record IdentityUserCreateRequest(
    string Username,
    string Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    string? TemporaryPassword);
