namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Provides aggregate webhook statistics for administration dashboards.
/// </summary>
public interface IWebhookStatsReader
{
    /// <summary>
    /// Returns aggregate statistics: subscription counts by status and 24-hour delivery metrics.
    /// </summary>
    Task<WebhookStats> GetStatsAsync(CancellationToken cancellationToken = default);
}
