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
    public bool IsLocalStore => false;
}
