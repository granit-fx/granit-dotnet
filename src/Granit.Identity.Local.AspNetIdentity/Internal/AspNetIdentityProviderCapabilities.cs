using Granit.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Declares the capabilities of the ASP.NET Core Identity provider.
/// </summary>
internal sealed class AspNetIdentityProviderCapabilities : IIdentityProviderCapabilities
{
    /// <inheritdoc/>
    public string ProviderName => "AspNetIdentity";

    /// <inheritdoc/>
    public bool SupportsIndividualSessionTermination => false;

    /// <inheritdoc/>
    public bool SupportsNativePasswordResetEmail => false;

    /// <inheritdoc/>
    public bool SupportsGroupHierarchy => false;

    /// <inheritdoc/>
    public bool SupportsCustomAttributes => true;

    /// <inheritdoc/>
    public int MaxCustomAttributes => int.MaxValue;

    /// <inheritdoc/>
    public bool SupportsCredentialVerification => true;

    /// <inheritdoc/>
    public bool SupportsUserCreation => true;

    /// <inheritdoc/>
    public bool IsLocalStore => true;
}
