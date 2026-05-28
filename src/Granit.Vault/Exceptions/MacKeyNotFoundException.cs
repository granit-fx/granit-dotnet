namespace Granit.Vault.Exceptions;

/// <summary>
/// Thrown when an <see cref="ITransitMacService"/> operation targets a logical key
/// name that the provider does not recognise (typo in configuration, key not yet
/// provisioned in the vault, or referenced version disabled/destroyed).
/// </summary>
public sealed class MacKeyNotFoundException : SecretVaultException
{
    /// <summary>Logical key name that triggered the failure. Safe for structured logs only — do NOT echo to end users.</summary>
    public string KeyName { get; }

    /// <summary>Initializes a new instance.</summary>
    public MacKeyNotFoundException(string keyName, Exception? innerException = null)
        : base("The requested MAC key was not found in the configured vault.", innerException)
    {
        KeyName = keyName;
    }
}
