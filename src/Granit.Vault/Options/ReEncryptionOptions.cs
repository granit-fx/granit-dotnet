namespace Granit.Vault.Options;

/// <summary>
/// Options that control re-encryption behaviour and retired key version detection.
/// </summary>
public sealed class ReEncryptionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Vault:ReEncryption";

    /// <summary>
    /// Set of key versions that are considered retired (e.g. <c>"v1"</c>, <c>"v2"</c>).
    /// When <see cref="ITransitEncryptionService.GetKeyVersion"/> returns a version
    /// present in this set, <see cref="Exceptions.RetiredKeyVersionException"/> is thrown
    /// during decryption.
    /// </summary>
    public ISet<string> RetiredKeyVersions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Default batch size for the re-encryption job. Default: 500.</summary>
    public int BatchSize { get; set; } = 500;
}
