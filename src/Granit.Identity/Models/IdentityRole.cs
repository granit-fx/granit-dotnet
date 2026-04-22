namespace Granit.Identity.Models;

/// <summary>
/// Represents a role from an external identity provider.
/// </summary>
/// <param name="Id">Unique role identifier in the identity provider.</param>
/// <param name="Name">Role name.</param>
/// <param name="Description">Optional role description.</param>
public sealed record IdentityRole(
    string Id,
    string Name,
    string? Description)
{
    /// <summary>
    /// OIDC client identifier this role is scoped to, or <see langword="null"/> for
    /// realm / global roles. Non-null on client-scope roles surfaced by providers
    /// implementing <see cref="IIdentityClientRoleManager"/> (Keycloak client roles,
    /// Entra app roles, Cognito client-bound groups).
    /// </summary>
    /// <remarks>
    /// Non-positional <c>init</c> property: the record's positional constructor
    /// (<c>Id, Name, Description</c>) stays binary-compatible and positional
    /// deconstruction continues to yield exactly three elements.
    /// </remarks>
    public string? ClientId { get; init; }
}
