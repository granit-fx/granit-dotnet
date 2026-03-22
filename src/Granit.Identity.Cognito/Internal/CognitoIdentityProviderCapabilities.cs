namespace Granit.Identity.Cognito.Internal;

/// <summary>
/// AWS Cognito-specific <see cref="IIdentityProviderCapabilities"/> implementation.
/// </summary>
internal sealed class CognitoIdentityProviderCapabilities : IIdentityProviderCapabilities
{
    /// <inheritdoc/>
    public string ProviderName => "Cognito";

    /// <inheritdoc/>
    /// <remarks>Cognito does not support individual session termination — only global sign-out.</remarks>
    public bool SupportsIndividualSessionTermination => false;

    /// <inheritdoc/>
    /// <remarks>Cognito can send password reset emails natively via <c>AdminResetUserPassword</c>.</remarks>
    public bool SupportsNativePasswordResetEmail => true;

    /// <inheritdoc/>
    /// <remarks>Cognito groups are flat (no hierarchical sub-groups).</remarks>
    public bool SupportsGroupHierarchy => false;

    /// <inheritdoc/>
    /// <remarks>Cognito supports custom attributes (prefixed with <c>custom:</c>).</remarks>
    public bool SupportsCustomAttributes => true;

    /// <inheritdoc/>
    /// <remarks>Cognito limits custom attributes to 50 per user pool.</remarks>
    public int MaxCustomAttributes => 50;

    /// <inheritdoc/>
    /// <remarks>Cognito supports credential verification via <c>ADMIN_USER_PASSWORD_AUTH</c> flow.</remarks>
    public bool SupportsCredentialVerification => true;

    /// <inheritdoc/>
    public bool SupportsUserCreation => true;

    /// <inheritdoc/>
    public bool IsLocalStore => false;
}
