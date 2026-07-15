namespace Granit.Identity.Federated.GoogleCloud.Internal;

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
    /// <remarks>
    /// The Firebase Admin SDK can only <em>generate</em> a password-reset link
    /// (<c>GeneratePasswordResetLinkAsync</c>) — it never sends the email. Advertising
    /// <c>true</c> here made the reset flow a silent no-op for the user. Sending requires
    /// a notifier hook (see the Entra ID provider's <c>IPasswordResetNotifier</c> pattern).
    /// </remarks>
    public bool SupportsNativePasswordResetEmail => false;

    /// <inheritdoc />
    public bool SupportsGroupHierarchy => false;

    /// <inheritdoc />
    public bool SupportsCustomAttributes => true;

    /// <inheritdoc />
    public int MaxCustomAttributes => 100;

    /// <inheritdoc />
    /// <remarks>
    /// The Firebase Admin SDK exposes no password-verify API — verification would require
    /// the Firebase Auth REST API with the Web API key. Advertising <c>true</c> here made
    /// hosts offer credential verification that rejected every valid password.
    /// </remarks>
    public bool SupportsCredentialVerification => false;

    /// <inheritdoc />
    public bool SupportsUserCreation => true;

    /// <inheritdoc />
    /// <remarks>Firebase Auth has no group concept — group management is never supported.</remarks>
    public bool SupportsGroupManagement => false;

    /// <inheritdoc />
    public bool IsLocalStore => false;
}
