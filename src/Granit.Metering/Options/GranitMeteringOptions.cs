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
}
