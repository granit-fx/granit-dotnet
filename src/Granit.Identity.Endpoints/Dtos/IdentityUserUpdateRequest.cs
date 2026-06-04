namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Request body for updating an existing user in the identity provider.
/// </summary>
public sealed record IdentityUserUpdateRequest(
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    IReadOnlyDictionary<string, string?>? Attributes = null);
