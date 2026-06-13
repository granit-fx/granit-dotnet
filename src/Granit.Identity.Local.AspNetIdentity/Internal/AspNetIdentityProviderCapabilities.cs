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
    // Delivered by publishing PasswordResetRequestedEto, consumed by Granit.Identity.Local.Notifications
    // (same path as the self-service forgot-password flow). Requires that module + an email channel to be wired.
    public bool SupportsNativePasswordResetEmail => true;

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
    /// <remarks>
    /// The local store supports group CRUD natively (via <c>OpenIddictGroupStore</c>), but
    /// tenant-admin-facing endpoints are not yet exposed. Reported as <see langword="false"/>
    /// pending the dedicated work item (see <c>docs/framework/identity/group-management.md</c>).
    /// </remarks>
    public bool SupportsGroupManagement => false;

    /// <inheritdoc/>
    public bool IsLocalStore => true;
}
