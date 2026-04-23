using Granit.Identity.Models;

namespace Granit.Identity;

/// <summary>
/// Capability interface surfaced by identity providers that distinguish <b>client-scoped roles</b>
/// from realm / global roles — Keycloak client roles, Entra app roles, Cognito groups bound to
/// an app client. Implemented only by providers that support the concept natively; others
/// (notably <c>AspNetIdentityProvider</c>) have no equivalent and intentionally do not
/// implement this contract.
/// </summary>
/// <remarks>
/// <para>
/// Not inherited by <see cref="IIdentityProvider"/> so the existing 5 realm-role methods on
/// <see cref="IIdentityRoleManager"/> stay unchanged for every current implementation.
/// Consumers that care about client-scope roles inject this interface directly and can
/// guard their code with <c>IIdentityProviderCapabilities.SupportsClientRoles</c>.
/// </para>
/// <para>
/// Phase 2 shipped the read-only shape; Phase 3 (ADR-031) adds the three write methods
/// — <see cref="CreateClientRoleAsync"/>, <see cref="AssignClientRoleAsync"/>,
/// <see cref="RemoveClientRoleAsync"/> — for Granit-first deployments where the admin UI
/// is the source of truth rather than upstream IaC. Providers that cannot honour
/// writes (or don't want to, by policy) report
/// <c>IIdentityProviderCapabilities.SupportsClientRoleWrites = false</c>; callers must
/// guard on that flag.
/// </para>
/// </remarks>
public interface IIdentityClientRoleManager
{
    /// <summary>Enumerates every OIDC client id known to the identity provider.</summary>
    Task<IReadOnlyList<string>> GetClientsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Enumerates the client-scope roles defined on <paramref name="clientId"/>.</summary>
    /// <remarks>
    /// Returned <see cref="IdentityRole.ClientId"/> is populated with
    /// <paramref name="clientId"/> so downstream consumers can persist the scope.
    /// </remarks>
    Task<IReadOnlyList<IdentityRole>> GetClientRolesAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Roles assigned to <paramref name="userId"/> scoped to the given client.
    /// </summary>
    Task<IReadOnlyList<IdentityRole>> GetUserClientRolesAsync(
        string userId,
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a client-scoped role under <paramref name="clientId"/> with the given
    /// <paramref name="name"/> and optional <paramref name="description"/>. Returns the
    /// created <see cref="IdentityRole"/> — its <c>Id</c> is the provider-assigned
    /// identifier, <c>ClientId</c> is the given <paramref name="clientId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provider-specific naming / collision semantics:
    /// </para>
    /// <list type="bullet">
    ///   <item>Keycloak: creates via <c>POST /admin/realms/{realm}/clients/{uuid}/roles</c>.
    ///         Duplicates return HTTP 409 which the implementation surfaces as the caller's
    ///         responsibility.</item>
    ///   <item>Entra ID: App Roles are stored as a single array on the application. The
    ///         implementation reads the current <c>appRoles</c>, appends the new entry, and
    ///         PATCHes the whole array back — inherently not safe against concurrent
    ///         application-level edits.</item>
    ///   <item>Cognito: creates a group named <c>{clientId}{Delimiter}{name}</c> per
    ///         ADR-027 naming convention. The provider reads the delimiter from its
    ///         injected <c>CognitoClientRoleSyncOptions</c>.</item>
    /// </list>
    /// </remarks>
    Task<IdentityRole> CreateClientRoleAsync(
        string clientId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants the role identified by <paramref name="roleName"/> on <paramref name="clientId"/>
    /// to the given user. Idempotent at the provider level when the assignment already
    /// exists (Keycloak / Entra / Cognito all accept the call as a no-op).
    /// </summary>
    Task AssignClientRoleAsync(
        string userId,
        string clientId,
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a previously granted client-scoped role assignment. No-op if the user
    /// did not carry the role.
    /// </summary>
    Task RemoveClientRoleAsync(
        string userId,
        string clientId,
        string roleName,
        CancellationToken cancellationToken = default);
}
