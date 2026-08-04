using Granit.Domain;

namespace Granit.Testing.Persistence.Domain;

/// <summary>
/// Conformance entity covering the full interceptor/filter surface in one shape:
/// audit fields + soft delete (<see cref="FullAuditedEntity"/>), tenant scoping
/// (<see cref="IMultiTenant"/>), optimistic concurrency (<see cref="IConcurrencyAware"/>),
/// and an enum property (string-column convention).
/// </summary>
public sealed class ConformanceOrder : FullAuditedEntity, IMultiTenant, IConcurrencyAware
{
    /// <inheritdoc/>
    public Guid? TenantId { get; set; }

    /// <inheritdoc/>
    public string ConcurrencyStamp { get; set; } = string.Empty;

    /// <summary>Enum property — persisted as its PascalCase string name by convention.</summary>
    public ConformanceOrderStatus Status { get; set; }

    /// <summary>Free label so tests can tag their own rows.</summary>
    public string Label { get; set; } = string.Empty;
}
