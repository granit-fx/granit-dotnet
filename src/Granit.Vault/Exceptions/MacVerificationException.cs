namespace Granit.Vault.Exceptions;

/// <summary>
/// Thrown when an <see cref="ITransitMacService"/> operation cannot complete because
/// the underlying provider rejected the request for a reason other than "key not found"
/// (malformed tag, cryptographic algorithm mismatch, provider configuration error).
/// </summary>
/// <remarks>
/// A negative verification result is NOT an exception — <see cref="ITransitMacService.VerifyAsync"/>
/// returns <see langword="false"/>. This exception is reserved for situations where
/// the verify call itself could not be answered.
/// </remarks>
public sealed class MacVerificationException : SecretVaultException
{
    /// <summary>Logical key name that triggered the failure.</summary>
    public string KeyName { get; }

    /// <summary>Initializes a new instance.</summary>
    public MacVerificationException(string keyName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        KeyName = keyName;
    }
}
