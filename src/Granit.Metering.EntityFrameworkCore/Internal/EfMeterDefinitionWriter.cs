using Granit.Metering.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

internal sealed class EfMeterDefinitionWriter(
    IDbContextFactory<MeteringDbContext> contextFactory)
    : EfStoreBase<MeterDefinition, MeteringDbContext>(contextFactory),
      IMeterDefinitionWriter
{
    Task IMeterDefinitionWriter.AddAsync(MeterDefinition definition, CancellationToken cancellationToken) =>
        base.AddAsync(definition, cancellationToken);

    Task IMeterDefinitionWriter.UpdateAsync(MeterDefinition definition, CancellationToken cancellationToken) =>
        base.UpdateAsync(definition, cancellationToken);
}
