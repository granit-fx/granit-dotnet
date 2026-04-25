using System.ComponentModel.DataAnnotations;

namespace Granit.Tax.Stripe.Options;

/// <summary>Configuration for Stripe Tax API integration.</summary>
public sealed class StripeTaxOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Tax:Stripe";

    /// <summary>Stripe secret API key.</summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Default product tax code (SaaS = txcd_10000000).</summary>
    public string ProductTaxCode { get; set; } = "txcd_10000000";

    /// <summary>
    /// ISO 4217 currency code used for Stripe Tax calculations. Defaults to <c>"eur"</c>.
    /// Stripe expects lowercase (e.g., <c>"eur"</c>, <c>"usd"</c>).
    /// </summary>
    public string Currency { get; set; } = "eur";
}
