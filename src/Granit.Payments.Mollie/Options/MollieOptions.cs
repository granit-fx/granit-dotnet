using System.ComponentModel.DataAnnotations;

namespace Granit.Payments.Mollie.Options;

/// <summary>
/// Configuration options for the Mollie payment provider.
/// </summary>
public sealed class MollieOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Mollie";

    /// <summary>Mollie API key (test_xxx or live_xxx).</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Webhook URL base for payment status callbacks.</summary>
    public string? WebhookBaseUrl { get; set; }
}
