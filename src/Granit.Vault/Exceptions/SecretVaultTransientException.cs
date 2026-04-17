namespace Granit.Vault.Exceptions;

/// <summary>
/// Thrown when the vault reports a transient failure (HTTP 429, 503, network timeout, gRPC <c>Unavailable</c>).
/// Callers that implement retry policies should catch this type specifically.
/// </summary>
/// <remarks>
/// No automatic retry is performed by the store — the retry policy belongs to the caller
/// because it depends on SLOs that vary per context (startup, hot path, batch).
/// </remarks>
public sealed class SecretVaultTransientException : SecretVaultException
{
    /// <summary>Name of the secret whose retrieval failed transiently.</summary>
    public string SecretName { get; }

    /// <summary>Initializes a new instance.</summary>
    public SecretVaultTransientException(string secretName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        SecretName = secretName;
    }
}
