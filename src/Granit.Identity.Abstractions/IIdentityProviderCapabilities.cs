namespace Granit.Identity;

/// <summary>
/// Describes the capabilities supported by the active identity provider at runtime.
/// </summary>
/// <remarks>
/// Consumers (endpoints, frontend) can query these capabilities to adapt their behavior
/// instead of relying on try/catch patterns. For example, hiding the "terminate single session"
/// button when <see cref="SupportsIndividualSessionTermination"/> is <c>false</c>.
/// </remarks>
public interface IIdentityProviderCapabilities
{
    /// <summary>The display name of the identity provider (e.g. "Keycloak", "Entra ID").</summary>
    string ProviderName { get; }

    /// <summary>Whether the provider can terminate a specific session without revoking all sessions.</summary>
    bool SupportsIndividualSessionTermination { get; }

    /// <summary>
    /// Whether <c>SendPasswordResetEmailAsync</c> delivers a reset email — either natively
    /// (federated providers send it themselves) or by publishing <c>PasswordResetRequestedEto</c>
    /// for a bundled notifications subscriber (local providers). When <see langword="false"/>,
    /// the provider endpoint returns 501 and callers should use the self-service forgot-password flow.
    /// </summary>
    bool SupportsNativePasswordResetEmail { get; }

    /// <summary>Whether the provider supports hierarchical group structures (sub-groups).</summary>
    bool SupportsGroupHierarchy { get; }

    /// <summary>Whether the provider supports custom user attributes.</summary>
    bool SupportsCustomAttributes { get; }

    /// <summary>Maximum number of custom attributes supported (0 if not supported).</summary>
    int MaxCustomAttributes { get; }

    /// <summary>Whether the provider supports credential verification (ROPC or equivalent).</summary>
    bool SupportsCredentialVerification { get; }

    /// <summary>Whether the provider supports creating new user accounts.</summary>
    bool SupportsUserCreation { get; }

    /// <summary>
    /// Whether tenant admins can create, update, and delete groups via the API.
    /// </summary>
    /// <remarks>
    /// Currently <see langword="false"/> for every provider — group lifecycle management
    /// is handled by the identity provider's admin console (Keycloak, EntraID, Cognito) or
    /// is unsupported (Firebase). The capability is exposed so the frontend can hide the
    /// "manage groups" menu while the feature is scoped. See
    /// <c>docs/framework/identity/group-management.md</c> for the roadmap.
    /// </remarks>
    bool SupportsGroupManagement { get; }

    /// <summary>
    /// Whether users are stored locally in the application database.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/> (e.g., ASP.NET Core Identity / OpenIddict), the user cache
    /// (<c>FederatedIdentity</c>) and sync middleware (<c>UserCacheSyncMiddleware</c>) are unnecessary
    /// because users are already queryable via SQL. When <see langword="false"/> (e.g., Keycloak,
    /// Entra ID), the cache is required for local queries.
    /// </remarks>
    bool IsLocalStore { get; }

    /// <summary>
    /// Whether the provider surfaces <b>client-scoped roles</b> in addition to realm / global
    /// roles via <see cref="IIdentityClientRoleManager"/>. Default: <see langword="false"/>
    /// (DIM). Providers that support the concept natively (Keycloak client roles, Entra app
    /// roles, Cognito app-client groups) override to <see langword="true"/>.
    /// </summary>
    bool SupportsClientRoles => false;

    /// <summary>
    /// Whether <see cref="IIdentityClientRoleManager"/>'s write methods
    /// (<c>CreateClientRoleAsync</c>, <c>AssignClientRoleAsync</c>,
    /// <c>RemoveClientRoleAsync</c>) are backed by a live implementation on this provider.
    /// Default: <see langword="false"/> (DIM). Providers that want Granit to be the source
    /// of truth for client-role provisioning override to <see langword="true"/>; read-only
    /// deployments (upstream IaC owns the roles) keep the default and the admin surfaces
    /// guard on this flag before exposing create / assign / remove actions. See ADR-031.
    /// </summary>
    bool SupportsClientRoleWrites => false;
}
