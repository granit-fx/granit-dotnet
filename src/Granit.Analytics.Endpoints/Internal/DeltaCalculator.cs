using Granit.Analytics.Endpoints.Dtos;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Computes the comparison-period payload (<see cref="MetricPreviousPayload"/>) given
/// the current and previous values plus the metric's <c>IsHigherBetter</c> flag.
/// </summary>
internal static class DeltaCalculator
{
    public static MetricPreviousPayload Build(
        decimal? current,
        decimal? previous,
        bool isHigherBetter)
    {
        // Trend is always defined (up / down / flat) but isFavorable / deltaRatio may be null
        // when one side is null or the previous value is zero (delta undefined).
        string trend = (current, previous) switch
        {
            (decimal c, decimal p) when c > p => "up",
            (decimal c, decimal p) when c < p => "down",
            _ => "flat",
        };

        double? deltaRatio = null;
        if (current.HasValue && previous.HasValue && previous.Value != 0m)
        {
            deltaRatio = (double)((current.Value - previous.Value) / Math.Abs(previous.Value));
        }

        bool? isFavorable = (current, previous) switch
        {
            (decimal c, decimal p) when c == p => true,
            (decimal c, decimal p) when c > p => isHigherBetter,
            (decimal c, decimal p) when c < p => !isHigherBetter,
            _ => null,
        };

        return new MetricPreviousPayload(previous, deltaRatio, trend, isFavorable);
    }
}
