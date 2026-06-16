using System.Data;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Postgres.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests.Internal;

/// <summary>
/// Provider-agnostic behaviour of <see cref="NpgsqlTenantDbIsolator"/>: it resolves the
/// tenant schema name, opens the underlying connection if needed, and delegates the
/// <c>SET search_path</c> SQL to <see cref="ITenantSchemaActivator"/>. Verified against a
/// SQLite in-memory connection so no live PostgreSQL is required — the isolator never
/// emits Postgres-specific SQL itself (that lives in the activator).
/// </summary>
public sealed class NpgsqlTenantDbIsolatorTests
{
    [Fact]
    public async Task IsolateAsync_resolves_schema_and_activates_it_on_an_opened_connection()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        DbContextOptionsBuilder<IsolatorTestDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connection);
        await using var context = new IsolatorTestDbContext(optionsBuilder.Options);

        var tenantId = Guid.NewGuid();

        ITenantSchemaProvider schemaProvider = Substitute.For<ITenantSchemaProvider>();
        schemaProvider.GetSchemaNameAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns("tenant_acme");
        ITenantSchemaActivator schemaActivator = Substitute.For<ITenantSchemaActivator>();

        NpgsqlTenantDbIsolator sut = new(schemaProvider, schemaActivator);

        await sut.IsolateAsync(context, tenantId, TestContext.Current.CancellationToken);

        connection.State.ShouldBe(ConnectionState.Open);
        await schemaActivator.Received(1).ActivateSchemaAsync(
            connection, "tenant_acme", Arg.Any<CancellationToken>());
    }

    private sealed class IsolatorTestDbContext(DbContextOptions<IsolatorTestDbContext> options)
        : DbContext(options);
}
