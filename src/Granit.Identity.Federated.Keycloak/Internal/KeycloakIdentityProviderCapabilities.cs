namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// Keycloak-specific <see cref="IIdentityProviderCapabilities"/> implementation.
/// </summary>
internal sealed class KeycloakIdentityProviderCapabilities : IIdentityProviderCapabilities
{
    /// <inheritdoc/>
    public string ProviderName => "Keycloak";

    /// <inheritdoc/>
    public bool SupportsIndividualSessionTermination => true;

    /// <inheritdoc/>
    public bool SupportsNativePasswordResetEmail => true;

    /// <inheritdoc/>
    public bool SupportsGroupHierarchy => true;

    /// <inheritdoc/>
    public bool SupportsCustomAttributes => true;

    /// <inheritdoc/>
    public int MaxCustomAttributes => int.MaxValue;

    /// <inheritdoc/>
    public bool SupportsCredentialVerification => true;

    /// <inheritdoc/>
    public bool SupportsUserCreation => true;

    /// <inheritdoc/>
    /// <remarks>
    /// Keycloak natively supports group CRUD via the Admin REST API, but Granit does not yet
    /// expose tenant-admin-facing endpoints. Reported as <see langword="false"/> until the
    /// feature ships (see <c>docs/framework/identity/group-management.md</c>).
    /// </remarks>
    public bool SupportsGroupManagement => false;

    /// <inheritdoc/>
    public bool IsLocalStore => false;

    /// <inheritdoc/>
    public bool SupportsClientRoles => true;
}
