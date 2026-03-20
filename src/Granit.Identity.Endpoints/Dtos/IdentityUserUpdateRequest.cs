namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Request body for updating an existing user in the identity provider.
/// </summary>
public sealed record IdentityUserUpdateRequest(
    string? Email,
    string? FirstName,
    string? LastName,
    IReadOnlyDictionary<string, string?>? Attributes);
