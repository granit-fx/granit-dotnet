namespace Granit.Metering.Endpoints.Dtos;

/// <summary>
/// HTTP body for <c>POST /metering/meters/{id}/recompute</c>.
/// </summary>
/// <param name="From">Inclusive lower bound of the recompute window (UTC, ISO 8601).</param>
/// <param name="To">Exclusive upper bound of the recompute window (UTC, ISO 8601).</param>
/// <remarks>
/// Window edges are snapped to hourly buckets server-side; partial hours are fully
/// rebuilt. The global ingestion watermark is never rewound — concurrent ingestion
/// past <paramref name="To"/> is unaffected.
/// </remarks>
public sealed record RecomputeUsageRequest(
    DateTimeOffset From,
    DateTimeOffset To);
