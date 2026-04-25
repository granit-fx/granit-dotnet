namespace Granit.CustomerBalance;

/// <summary>Tunable knobs for <c>Granit.CustomerBalance</c>.</summary>
public sealed class CustomerBalanceOptions
{
    /// <summary>
    /// Configuration section name (e.g. <c>"Granit:CustomerBalance"</c>).
    /// </summary>
    public const string SectionName = "Granit:CustomerBalance";

    /// <summary>
    /// Number of days before <c>ExpiresAt</c> at which the daily pre-expiration
    /// scan emits a <see cref="Events.CreditNearExpirationEto"/> for a promotional
    /// credit. Default = 7. Must be ≥ 1.
    /// </summary>
    public int PreExpirationWarningDays { get; set; } = 7;
}
