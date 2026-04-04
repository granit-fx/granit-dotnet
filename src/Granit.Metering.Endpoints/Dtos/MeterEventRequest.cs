namespace Granit.Metering.Endpoints.Dtos;

/// <summary>A single usage event to record.</summary>
/// <param name="MeterDefinitionId">The meter this event belongs to.</param>
/// <param name="IdempotencyKey">Client-provided key for deduplication.</param>
/// <param name="Quantity">The measured quantity (must be positive).</param>
/// <param name="Timestamp">When the usage occurred.</param>
/// <param name="Metadata">Optional JSON metadata.</param>
public sealed record MeterEventRequest(
    Guid MeterDefinitionId,
    string IdempotencyKey,
    decimal Quantity,
    DateTimeOffset Timestamp,
    string? Metadata = null);

/// <summary>Request to record one or more usage events.</summary>
/// <param name="Events">The events to record.</param>
public sealed record RecordUsageRequest(
    IReadOnlyList<MeterEventRequest> Events);
