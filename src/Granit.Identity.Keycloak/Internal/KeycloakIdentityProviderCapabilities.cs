namespace Granit.Identity.Keycloak.Internal;

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
    public bool IsLocalStore => false;
}
