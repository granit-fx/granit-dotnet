using Granit.Exceptions;

namespace Granit.Vault.HashiCorp.Exceptions;

/// <summary>
/// Thrown when the HashiCorp Vault client configuration is invalid (wrong auth method, missing token, etc.).
/// Carries a structured <see cref="IHasErrorCode.ErrorCode"/> for localized resolution
/// by <c>GranitExceptionHandler</c>.
/// </summary>
public sealed class HashiCorpVaultConfigurationException : InvalidOperationException, IHasErrorCode
{
    /// <inheritdoc/>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HashiCorpVaultConfigurationException"/>.
    /// </summary>
    /// <param name="errorCode">Structured error code (e.g. <c>"Vault:UnknownAuthMethod"</c>).</param>
    /// <param name="message">Technical message for logs and developers.</param>
    public HashiCorpVaultConfigurationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
