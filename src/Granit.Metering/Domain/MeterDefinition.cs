using Granit.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.MultiTenancy;

namespace Granit.Metering.Domain;

/// <summary>
/// Defines a usage meter (e.g., API calls, storage GB, messages sent).
/// </summary>
/// <remarks>
/// Each meter has a name, unit of measure, and aggregation strategy.
/// Meter events are recorded against a definition and aggregated into
/// <see cref="UsageAggregate"/> rollups by background jobs.
/// </remarks>
public sealed class MeterDefinition : AuditedAggregateRoot, IActive, IMultiTenant
{
    private MeterDefinition() { }

    /// <summary>Creates a new meter definition.</summary>
    public static MeterDefinition Create(
        Guid id,
        string name,
        string unit,
        AggregationType aggregationType,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        return new MeterDefinition
        {
            Id = id,
            Name = name,
            Unit = unit,
            AggregationType = aggregationType,
            Description = description,
            Activated = true,
        };
    }

    /// <summary>Meter display name (e.g., "API Calls", "Storage").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Unit of measure (e.g., "requests", "GB", "messages").</summary>
    public string Unit { get; private set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; private set; }

    /// <summary>How events are aggregated into rollups.</summary>
    public AggregationType AggregationType { get; private set; }

    /// <summary>Whether this meter accepts new events.</summary>
    public bool Activated { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>Updates the meter definition metadata.</summary>
    public void Update(string name, string unit, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        Name = name;
        Unit = unit;
        Description = description;
    }

    /// <summary>Deactivates the meter. No new events will be accepted.</summary>
    public void Deactivate() => Activated = false;

    /// <summary>Reactivates the meter.</summary>
    public void Activate() => Activated = true;
}
