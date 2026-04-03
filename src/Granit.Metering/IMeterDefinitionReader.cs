using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;

namespace Granit.Metering;

/// <summary>Reads meter definitions (query side of CQRS).</summary>
public interface IMeterDefinitionReader
{
    /// <summary>Returns a meter definition by ID.</summary>
    Task<MeterDefinition?> GetByIdAsync(MeterDefinitionId id, CancellationToken cancellationToken = default);

    /// <summary>Returns a meter definition by name within the current tenant.</summary>
    Task<MeterDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Returns all active meter definitions for the current tenant.</summary>
    Task<IReadOnlyList<MeterDefinition>> GetActiveAsync(CancellationToken cancellationToken = default);
}
