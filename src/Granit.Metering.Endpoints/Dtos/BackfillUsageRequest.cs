namespace Granit.Metering.Endpoints.Dtos;

/// <summary>
/// HTTP body for <c>POST /metering/events/backfill</c>. Same shape as
/// <see cref="RecordUsageRequest"/>; the dedicated DTO surfaces a distinct OpenAPI
/// operation so client SDKs can scope the broader event-age validator (up to
/// 365 days) only to the backfill code path.
/// </summary>
/// <param name="Events">The historical events to record.</param>
public sealed record BackfillUsageRequest(
    IReadOnlyList<MeterEventRequest> Events);

/// <summary>HTTP response for a backfill batch.</summary>
/// <param name="EventsAccepted">Total events the server attempted to insert (post-validation).</param>
/// <param name="MetersAffected">Distinct meters that received at least one event.</param>
/// <param name="AggregatesRebuilt">Sum of hourly UsageAggregate rows updated across the per-meter recomputes.</param>
public sealed record BackfillUsageResponse(
    int EventsAccepted,
    int MetersAffected,
    int AggregatesRebuilt);
