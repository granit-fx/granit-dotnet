using System.ComponentModel.DataAnnotations;

namespace Granit.Payments.SepaDirectDebit.GoCardless.Options;

/// <summary>Configuration for the GoCardless SEPA DD provider.</summary>
public sealed class GoCardlessOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:SepaDirectDebit:GoCardless";

    /// <summary>GoCardless access token.</summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>GoCardless webhook secret for signature verification.</summary>
    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Use GoCardless sandbox environment.</summary>
    public bool UseSandbox { get; set; }

    /// <summary>
    /// Fallback redirect URL used when a mandate setup request does not provide one.
    /// Must be an absolute HTTPS URL registered in the GoCardless dashboard.
    /// </summary>
    [Required]
    [Url]
    public string DefaultMandateRedirectUrl { get; set; } = string.Empty;
}
