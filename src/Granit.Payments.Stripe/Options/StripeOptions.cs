using System.ComponentModel.DataAnnotations;

namespace Granit.Payments.Stripe.Options;

/// <summary>
/// Configuration options for the Stripe payment provider.
/// </summary>
public sealed class StripeOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Stripe";

    /// <summary>Stripe secret API key (sk_test_xxx or sk_live_xxx).</summary>
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Stripe webhook signing secret (whsec_xxx).</summary>
    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Stripe publishable key for frontend (pk_test_xxx). Optional.</summary>
    public string? PublishableKey { get; set; }
}
