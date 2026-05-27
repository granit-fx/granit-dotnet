using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests;

/// <summary>
/// Locks the ADR-061 adoption: the <see cref="Granit.Domain.IConcurrencyAware"/> job
/// aggregates must be configured as EF Core concurrency tokens by
/// <c>ApplyGranitConventions</c> in the real <c>DataExchangeDbContext</c> model — not just
/// in the generic convention unit tests. Catches an accidental removal of the interface.
/// </summary>
public sealed class ConcurrencyConventionTests
{
    [Theory]
    [InlineData(typeof(ExportJob))]
    [InlineData(typeof(ImportJob))]
    public void Job_aggregates_expose_ConcurrencyStamp_as_a_concurrency_token(Type entityType)
    {
        using DataExchangeDbContext context =
            new InMemoryDataExchangeContextFactory(Guid.NewGuid().ToString()).CreateDbContext();

        IProperty property = context.Model
            .FindEntityType(entityType)
            .ShouldNotBeNull()
            .FindProperty("ConcurrencyStamp")
            .ShouldNotBeNull();

        property.IsConcurrencyToken.ShouldBeTrue();
        property.GetMaxLength().ShouldBe(36);
    }
}
