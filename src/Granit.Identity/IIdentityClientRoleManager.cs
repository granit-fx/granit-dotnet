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
/// The read-only shape below is the Phase 2a surface: <c>CreateClientRoleAsync</c> and
/// user ↔ client-role assignment operations are deferred until a concrete need arises —
/// most deployments provision client roles via IaC (Keycloak CLI, Terraform, Cognito
/// console) and expose them in Granit for permission grants only.
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
}
