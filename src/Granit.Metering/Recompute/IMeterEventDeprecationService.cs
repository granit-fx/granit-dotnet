namespace Granit.Metering.Recompute;

/// <summary>
/// Soft-deprecates a single <see cref="Domain.MeterEvent"/> (audit-safe alternative to
/// DELETE) and triggers an automatic <see cref="IUsageRecomputeService"/> pass over
/// the affected hourly bucket so existing aggregates immediately reflect the change.
/// </summary>
/// <remarks>
/// Deprecation is admin-only — see <c>Metering.Events.Manage</c>. It is intentionally
/// NOT idempotent: deprecating an already-deprecated event throws (and the HTTP layer
/// returns 409). This guards against accidental re-deprecation that would otherwise
/// be silently swallowed and could mask bugs in admin tooling.
/// </remarks>
public interface IMeterEventDeprecationService
{
    /// <summary>Deprecates the event and recomputes its hourly bucket in one transaction.</summary>
    Task<MeterEventDeprecationResult> DeprecateAsync(
        Guid eventId,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a successful deprecation.</summary>
/// <param name="EventId">Id of the deprecated event.</param>
/// <param name="MeterDefinitionId">Owning meter (for audit / metrics).</param>
/// <param name="DeprecatedAt">Server-side UTC timestamp written on the event.</param>
/// <param name="Recompute">Recompute summary for the affected hourly window.</param>
public sealed record MeterEventDeprecationResult(
    Guid EventId,
    Guid MeterDefinitionId,
    DateTimeOffset DeprecatedAt,
    UsageRecomputeResult Recompute);
