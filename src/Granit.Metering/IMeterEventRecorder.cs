using Granit.Metering.Domain;

namespace Granit.Metering;

/// <summary>
/// Records usage events with idempotency-key deduplication.
/// </summary>
/// <remarks>
/// Implementations must handle duplicate <see cref="MeterEvent.IdempotencyKey"/> values
/// gracefully (insert-first, ignore on unique constraint violation).
/// </remarks>
public interface IMeterEventRecorder
{
    /// <summary>Records a single meter event. Duplicates are silently ignored.</summary>
    Task RecordAsync(MeterEvent meterEvent, CancellationToken cancellationToken = default);

    /// <summary>Records a batch of meter events. Duplicates are silently ignored.</summary>
    Task RecordBatchAsync(IReadOnlyList<MeterEvent> events, CancellationToken cancellationToken = default);
}
