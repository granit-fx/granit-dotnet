using System.ComponentModel.DataAnnotations;

namespace Granit.Vault.Options;

/// <summary>
/// Options that shape the behaviour of <see cref="ISecretStore"/> implementations.
/// Bound from configuration section <c>Vault:SecretStore</c>.
/// </summary>
public sealed class SecretStoreOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Vault:SecretStore";

    /// <summary>
    /// L1 FusionCache TTL for secret reads, in seconds. <c>0</c> disables caching
    /// entirely — each <see cref="ISecretStore.GetSecretAsync"/> call hits the provider.
    /// Default: <c>0</c> (secure by default). Recommended value when enabled: ≤ 300 s
    /// to limit exposure after rotation.
    /// </summary>
    [Range(0, 86400)]
    public int CacheSeconds { get; set; }

    /// <summary>
    /// Hard upper bound on the size (bytes) of a <see cref="SecretDescriptor.BinaryValue"/>
    /// that may enter the cache. Secrets above this threshold bypass the cache to avoid
    /// Large Object Heap pressure. Default: 64 KiB.
    /// </summary>
    [Range(1024, 10_485_760)]
    public int MaxCachedBinarySizeBytes { get; set; } = 64 * 1024;
}
