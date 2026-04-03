namespace Granit.Metering.Options;

/// <summary>
/// Configuration options for the metering module.
/// </summary>
public sealed class GranitMeteringOptions
{
    /// <summary>
    /// The usage percentage at which a <c>QuotaThresholdReachedEto</c> is published.
    /// Default: 80.
    /// </summary>
    public decimal ThresholdPercentage { get; set; } = 80;

    /// <summary>
    /// Maximum allowed quantity per meter event. Prevents aggregation overflow
    /// from maliciously large values. Default: 1,000,000,000.
    /// </summary>
    public decimal MaxEventQuantity { get; set; } = 1_000_000_000m;
}
