using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

internal sealed class EfMeterDefinitionReader(
    IDbContextFactory<MeteringDbContext> contextFactory)
    : EfStoreBase<MeterDefinition, MeteringDbContext>(contextFactory),
      IMeterDefinitionReader
{
    public Task<MeterDefinition?> GetByIdAsync(MeterDefinitionId id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id.Value, cancellationToken);

    public Task<MeterDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        ReadAsync(async db => await db.MeterDefinitions
            .FirstOrDefaultAsync(m => m.Name == name, cancellationToken)
            .ConfigureAwait(false), cancellationToken);

    public Task<IReadOnlyList<MeterDefinition>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<MeterDefinition>().Where(m => m.IsActive),
            cancellationToken);
}
