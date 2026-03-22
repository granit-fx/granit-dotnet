namespace Granit.Identity.GoogleCloud.Internal;

/// <summary>
/// Capabilities of the Google Cloud Identity Platform (Firebase Auth) identity provider.
/// </summary>
internal sealed class GoogleCloudIdentityProviderCapabilities : IIdentityProviderCapabilities
{
    /// <inheritdoc />
    public string ProviderName => "Google Cloud Identity Platform";

    /// <inheritdoc />
    public bool SupportsIndividualSessionTermination => false;

    /// <inheritdoc />
    public bool SupportsNativePasswordResetEmail => true;

    /// <inheritdoc />
    public bool SupportsGroupHierarchy => false;

    /// <inheritdoc />
    public bool SupportsCustomAttributes => true;

    /// <inheritdoc />
    public int MaxCustomAttributes => 100;

    /// <inheritdoc />
    public bool SupportsCredentialVerification => true;

    /// <inheritdoc />
    public bool SupportsUserCreation => true;

    /// <inheritdoc />
    public bool IsLocalStore => false;
}
