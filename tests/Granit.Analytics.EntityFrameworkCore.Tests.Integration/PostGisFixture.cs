using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Boots a <c>postgis/postgis</c> container with the PostGIS extension already
/// available — the parity check we need for B8 (#1568) is that the
/// <c>NtsGeographyPointProjector</c> reading a <c>geography(Point)</c> column
/// matches what the unit tests assert when projecting a NetTopologySuite
/// <c>Point</c>. Separate from <see cref="PostgresFixture"/> so the empty-set
/// parity tests don't pay the PostGIS image weight.
/// </summary>
public sealed class PostGisFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgis/postgis:17-3.5-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
