using Granit.Exceptions;

namespace Granit.Vault.Exceptions;

/// <summary>
/// Thrown when the vault or secret store is misconfigured (missing auth, invalid options,
/// mount point not available, etc.). Carries a structured <see cref="IHasErrorCode.ErrorCode"/>
/// for localized resolution by <c>GranitExceptionHandler</c>.
/// </summary>
/// <remarks>
/// Dev-facing error. No localized user message is attached to the base class — if a
/// specific configuration issue needs a localized message, add a key to
/// <c>Localization/Vault/*.json</c> and reference it via <see cref="ErrorCode"/>.
/// </remarks>
public sealed class SecretVaultConfigurationException : SecretVaultException, IHasErrorCode
{
    /// <inheritdoc />
    public string ErrorCode { get; }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Structured error code (e.g. <c>"Vault:Secret:MissingMountPoint"</c>).</param>
    /// <param name="message">Technical message for logs and developers.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public SecretVaultConfigurationException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
