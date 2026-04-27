using System.ComponentModel.DataAnnotations;

namespace Granit.CustomerBalance.Options;

/// <summary>
/// Configuration options for the Granit.CustomerBalance module.
/// </summary>
public sealed class CustomerBalanceOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "CustomerBalance";

    /// <summary>
    /// Number of days before a promotional credit's expiration date at which the
    /// "credit expiring soon" notification should be triggered by the daily scanner.
    /// Default: 7 days. Range: 1-90.
    /// </summary>
    [Range(1, 90)]
    public int ExpirationLeadTimeDays { get; set; } = 7;

    /// <summary>
    /// Minimum interval, in days, between two "credit expiring soon" notifications for
    /// the same credit. Prevents the scanner from re-alerting the same user every run
    /// during the lead-time window. Default: 7 days. Range: 1-30.
    /// </summary>
    [Range(1, 30)]
    public int ExpirationNotificationCooldownDays { get; set; } = 7;
}
