namespace Granit.Webhooks.Endpoints.Dtos;

/// <summary>
/// Aggregate webhook statistics for the administration dashboard.
/// </summary>
public sealed record WebhookSubscriptionStatsResponse(
    int TotalSubscriptions,
    int ActiveCount,
    int SuspendedCount,
    int DeactivatedCount,
    int DeliveriesLast24h,
    double SuccessRateLast24h,
    double AvgResponseTimeMsLast24h);
