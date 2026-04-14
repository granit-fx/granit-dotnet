using Granit.Metering.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

internal sealed class EfMeterDefinitionWriter(
    IDbContextFactory<MeteringDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<MeterDefinition, MeteringDbContext>(contextFactory, currentTenant),
      IMeterDefinitionWriter
{
    Task IMeterDefinitionWriter.AddAsync(MeterDefinition definition, CancellationToken cancellationToken) =>
        base.AddAsync(definition, cancellationToken);

    Task IMeterDefinitionWriter.UpdateAsync(MeterDefinition definition, CancellationToken cancellationToken) =>
        base.UpdateAsync(definition, cancellationToken);
}
