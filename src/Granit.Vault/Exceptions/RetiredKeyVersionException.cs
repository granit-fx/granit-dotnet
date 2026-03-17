namespace Granit.Vault.Exceptions;

/// <summary>
/// Thrown when decryption is attempted on ciphertext encrypted with a retired key version.
/// Configure retired versions via <see cref="Options.ReEncryptionOptions.RetiredKeyVersions"/>.
/// </summary>
public sealed class RetiredKeyVersionException : InvalidOperationException
{
    /// <summary>The retired key version that was encountered (e.g. <c>"v1"</c>).</summary>
    public string KeyVersion { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RetiredKeyVersionException"/>.
    /// </summary>
    /// <param name="keyVersion">The retired key version string.</param>
    public RetiredKeyVersionException(string keyVersion)
        : base($"Key version '{keyVersion}' has been retired. Run the re-encryption job before retiring this key version.")
    {
        KeyVersion = keyVersion;
    }
}
