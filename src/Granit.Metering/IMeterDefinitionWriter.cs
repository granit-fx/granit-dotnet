using Granit.Metering.Domain;

namespace Granit.Metering;

/// <summary>Persists meter definition changes (command side of CQRS).</summary>
public interface IMeterDefinitionWriter
{
    /// <summary>Persists a new meter definition.</summary>
    Task AddAsync(MeterDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing meter definition.</summary>
    Task UpdateAsync(MeterDefinition definition, CancellationToken cancellationToken = default);
}
