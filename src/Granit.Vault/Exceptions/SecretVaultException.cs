namespace Granit.Vault.Exceptions;

/// <summary>
/// Base class for all <see cref="ISecretStore"/> failures. Concrete subclasses distinguish
/// the failure kind (not found, access denied, transient, configuration) so callers can
/// react appropriately.
/// </summary>
/// <remarks>
/// Architecture test (<c>VaultConventionTests</c>) asserts that every exception thrown
/// by an <see cref="ISecretStore"/> implementation inherits either from this class or
/// from <see cref="Granit.Exceptions.NotFoundException"/> / <see cref="Granit.Exceptions.ForbiddenException"/>.
/// </remarks>
public abstract class SecretVaultException : Exception
{
    /// <inheritdoc />
    protected SecretVaultException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
