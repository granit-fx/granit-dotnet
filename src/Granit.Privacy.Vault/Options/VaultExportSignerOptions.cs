using System.ComponentModel.DataAnnotations;

namespace Granit.Privacy.Vault.Options;

/// <summary>
/// Configuration for <see cref="VaultExportHmacSigner"/>.
/// </summary>
public sealed class VaultExportSignerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Privacy:VaultExportSigner";

    /// <summary>
    /// Logical key name passed to <see cref="Granit.Vault.ITransitMacService.MacAsync"/>
    /// for fragment-identity tags (the <c>ExportHmacParameters</c> canonical bytes). Required.
    /// </summary>
    [Required]
    public string FragmentKeyName { get; set; } = string.Empty;

    /// <summary>
    /// Logical key name used for <see cref="Granit.Privacy.DataExport.Security.IExportContentSigner"/>
    /// — signing manifest sidecars and arbitrary byte payloads. Separating fragment vs.
    /// content keys is an ISO 27001 A.10.1 usage-separation hygiene control. Required.
    /// </summary>
    [Required]
    public string ContentKeyName { get; set; } = string.Empty;
}
