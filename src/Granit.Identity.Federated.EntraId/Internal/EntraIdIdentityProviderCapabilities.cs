namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Entra ID–specific <see cref="IIdentityProviderCapabilities"/> implementation.
/// </summary>
internal sealed class EntraIdIdentityProviderCapabilities : IIdentityProviderCapabilities
{
    /// <inheritdoc/>
    public string ProviderName => "Entra ID";

    /// <inheritdoc/>
    /// <remarks>
    /// Entra ID's <c>revokeSignInSessions</c> API revokes all sessions at once.
    /// Individual session termination is not supported.
    /// </remarks>
    public bool SupportsIndividualSessionTermination => false;

    /// <inheritdoc/>
    /// <remarks>
    /// Entra ID does not expose a native password reset email API via Graph.
    /// A temporary password is set instead, with an optional <see cref="IPasswordResetNotifier"/>.
    /// </remarks>
    public bool SupportsNativePasswordResetEmail => false;

    /// <inheritdoc/>
    /// <remarks>
    /// Entra ID groups are flat — no sub-groups.
    /// </remarks>
    public bool SupportsGroupHierarchy => false;

    /// <inheritdoc/>
    public bool SupportsCustomAttributes => true;

    /// <inheritdoc/>
    /// <remarks>
    /// Entra ID supports <c>onPremisesExtensionAttributes</c> (extensionAttribute1–15).
    /// </remarks>
    public int MaxCustomAttributes => 15;

    /// <inheritdoc/>
    public bool SupportsCredentialVerification => true;

    /// <inheritdoc/>
    public bool SupportsUserCreation => true;

    /// <inheritdoc/>
    /// <remarks>
    /// EntraID supports group CRUD via Microsoft Graph, but tenant-admin-facing endpoints
    /// are not yet exposed in Granit. In a shared-directory deployment, group CRUD cannot
    /// be safely isolated per SaaS tenant. Reported as <see langword="false"/> pending the
    /// dedicated work item (see <c>docs/framework/identity/group-management.md</c>).
    /// </remarks>
    public bool SupportsGroupManagement => false;

    /// <inheritdoc/>
    public bool IsLocalStore => false;
}
