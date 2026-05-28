using System.ComponentModel.DataAnnotations;

namespace Granit.Vault.Azure.Options;

/// <summary>
/// Configuration for the Azure Managed HSM-backed <see cref="ITransitMacService"/>.
/// </summary>
/// <remarks>
/// Azure Key Vault <b>Standard</b> tier does NOT support HMAC keys — only Managed HSM
/// exposes <c>oct-HSM</c> keys with the <c>HS256</c> sign/verify operations. Hosts that
/// cannot adopt Managed HSM should use <see cref="Granit.Vault.Services.SecretBackedMacService"/>
/// instead.
/// </remarks>
public sealed class AzureManagedHsmMacOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Vault:Azure:ManagedHsm:Mac";

    /// <summary>
    /// URI of the Managed HSM instance (e.g. <c>https://my-hsm.managedhsm.azure.net/</c>). Required.
    /// </summary>
    [Required]
    public string HsmUri { get; set; } = string.Empty;

    /// <summary>
    /// Name of the <c>oct-HSM</c> key used to sign new tags. Required.
    /// </summary>
    [Required]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Optional explicit key version pin. When unset, the implementation resolves the latest
    /// version on every <c>MacAsync</c> call — the per-call latency cost is amortised by
    /// the SDK's connection pool.
    /// </summary>
    public string? KeyVersion { get; set; }
}
