namespace Granit.Analytics.Metrics;

/// <summary>
/// Semantic kind of a metric's value — drives frontend formatting (number, currency,
/// percentage, etc.) without leaking culture-specific formatting to the server.
/// </summary>
public enum MetricValueKind
{
    /// <summary>Plain count of items (rendered as integer).</summary>
    Count,

    /// <summary>Generic numeric value (rendered with locale separators, optional decimals).</summary>
    Number,

    /// <summary>Monetary amount (rendered with locale + ISO 4217 currency code).</summary>
    Currency,

    /// <summary>Ratio in [0, 1] (rendered as percentage with locale separators).</summary>
    Percentage,

    /// <summary>Duration in seconds (rendered with the locale's duration formatting).</summary>
    Duration,

    /// <summary>Date / instant (rendered with the locale's date formatting).</summary>
    Date,
}
