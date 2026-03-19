namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Aggregate webhook statistics for the administration dashboard.
/// </summary>
public sealed record WebhookStats(
    int TotalSubscriptions,
    int ActiveCount,
    int SuspendedCount,
    int DeactivatedCount,
    int DeliveriesLast24h,
    double SuccessRateLast24h,
    double AvgResponseTimeMsLast24h);
