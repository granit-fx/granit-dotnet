namespace Granit.Metering.Recompute;

/// <summary>Raised when a deprecation targets a non-existent <see cref="Domain.MeterEvent"/>.</summary>
public sealed class MeterEventNotFoundException(Guid eventId)
    : Exception($"Meter event '{eventId}' not found.")
{
    /// <summary>The requested event id.</summary>
    public Guid EventId { get; } = eventId;
}
