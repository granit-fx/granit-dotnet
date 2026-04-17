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

    /// <summary>
    /// Hard upper bound on the decoded size (bytes) of a secret payload returned by a
    /// provider. Payloads above this threshold are rejected with
    /// <see cref="Exceptions.SecretVaultConfigurationException"/> before any allocation,
    /// protecting the host from OOM triggered by a misconfigured or compromised vault
    /// (CWE-400 / CWE-770). Default: 16 MiB — generous for PFX bundles and CA chains,
    /// orders of magnitude below process memory limits.
    /// </summary>
    [Range(4096, 134_217_728)]
    public int MaxBinaryPayloadBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Optional canary secret name used by <c>AddGranitSecretStoreHealthCheck()</c>.
    /// When <c>null</c> or empty, the health check is a no-op — use a dedicated non-critical,
    /// read-only secret (e.g. <c>healthcheck/probe</c>) specifically for liveness probing;
    /// NEVER reuse a business secret here.
    /// </summary>
    public string? HealthCheckSecretName { get; set; }
}
