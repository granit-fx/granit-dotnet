using System.ComponentModel.DataAnnotations;

namespace Granit.Tax.Options;

/// <summary>
/// Global tax configuration options.
/// </summary>
public sealed class TaxOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Tax";

    /// <summary>Seller's country code (ISO 3166-1 alpha-2). Default: "BE".</summary>
    [Required]
    public string SellerCountryCode { get; set; } = "BE";

    /// <summary>Seller's VAT number (e.g., "BE0123456789").</summary>
    public string? SellerVatNumber { get; set; }

    /// <summary>Whether OSS (One-Stop Shop) is enabled for B2C cross-border EU sales.</summary>
    public bool OssEnabled { get; set; }

    /// <summary>EU countries where the seller is OSS-registered.</summary>
    public HashSet<string> OssRegisteredCountries { get; set; } = [];

    /// <summary>
    /// Whether to accept offline-validated VAT numbers when VIES is unavailable.
    /// </summary>
    /// <remarks>
    /// When <c>true</c> (default for SaaS B2B): if VIES is down and offline format
    /// validation passes, reverse charge (0%) is accepted. The validation is saved
    /// with <c>OfflinePending</c> source and retried by a background job.
    /// When <c>false</c>: VAT is treated as invalid and buyer country rate is charged.
    /// </remarks>
    public bool AllowOfflineFallback { get; set; } = true;

    /// <summary>Cache TTL for validated tax IDs (hours). Default: 24.</summary>
    public int ValidationCacheTtlHours { get; set; } = 24;
}
