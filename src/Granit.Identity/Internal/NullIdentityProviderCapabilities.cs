namespace Granit.Identity.Internal;

/// <summary>
/// Null-object implementation of <see cref="IIdentityProviderCapabilities"/>.
/// Reports no capabilities, registered by default when no provider package is installed.
/// </summary>
internal sealed class NullIdentityProviderCapabilities : IIdentityProviderCapabilities
{
    /// <inheritdoc/>
    public string ProviderName => "None";

    /// <inheritdoc/>
    public bool SupportsIndividualSessionTermination => false;

    /// <inheritdoc/>
    public bool SupportsNativePasswordResetEmail => false;

    /// <inheritdoc/>
    public bool SupportsGroupHierarchy => false;

    /// <inheritdoc/>
    public bool SupportsCustomAttributes => false;

    /// <inheritdoc/>
    public int MaxCustomAttributes => 0;

    /// <inheritdoc/>
    public bool SupportsCredentialVerification => false;

    /// <inheritdoc/>
    public bool SupportsUserCreation => false;

    /// <inheritdoc/>
    public bool SupportsGroupManagement => false;

    /// <inheritdoc/>
    public bool IsLocalStore => false;
}
