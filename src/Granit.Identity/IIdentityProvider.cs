namespace Granit.Identity;

/// <summary>
/// Provides read and write access to identity users and roles from an external identity provider
/// (Keycloak, LDAP, Entra ID, etc.).
/// </summary>
/// <remarks>
/// <para>
/// A <c>NullIdentityProvider</c> is registered by default (returns empty lists / no-ops).
/// Install a provider package (e.g. <c>Granit.Identity.Federated.Keycloak</c>) to connect
/// to a real identity system.
/// </para>
/// <para>
/// This composite interface inherits from fine-grained interfaces
/// (<see cref="IIdentityUserReader"/>, <see cref="IIdentityUserWriter"/>, etc.)
/// for backward compatibility. New consumers should prefer injecting the specific
/// interface they need (e.g. <see cref="IIdentityUserReader"/> instead of
/// <see cref="IIdentityProvider"/>).
/// </para>
/// <para>
/// Write operations (<see cref="IIdentityUserWriter.SetUserEnabledAsync"/>,
/// <see cref="IIdentityUserWriter.UpdateUserAsync"/>)
/// propagate exceptions on failure. For Keycloak, the service account must hold the
/// <c>realm-management:manage-users</c> role to perform write operations.
/// </para>
/// </remarks>
public interface IIdentityProvider :
    IIdentityUserReader,
    IIdentityUserWriter,
    IIdentityRoleManager,
    IIdentityGroupManager,
    IIdentitySessionManager,
    IIdentityPasswordManager,
    IIdentityCredentialVerifier
{
}
