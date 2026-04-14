using System.Data.Common;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.Hosting.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Tests;

public sealed class AutoTenantProvisionerTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private const string TenantName = "Acme Corp";

    // SQLite in-memory connection kept alive for the test duration.
    // MigrateAsync requires a relational provider (InMemory doesn't support it).
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AutoTenantProvisionerTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ProvisionAsync_NoIsolatedContexts_DoesNothing()
    {
        // Arrange — no IsolatedDbContextMarker registered
        ServiceCollection services = [];
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ITenantDbIsolator>(Substitute.For<ITenantDbIsolator>());
        using ServiceProvider sp = services.BuildServiceProvider();

        AutoTenantProvisioner sut = CreateProvisioner(sp);

        // Act & Assert — should complete without throwing
        await sut.ProvisionAsync(TenantId, TenantName, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProvisionAsync_WithIsolatedContext_MigratesContext()
    {
        // Arrange
        ServiceCollection services = BuildServicesWithTestContext();
        using ServiceProvider sp = services.BuildServiceProvider();

        AutoTenantProvisioner sut = CreateProvisioner(sp);

        // Act
        await sut.ProvisionAsync(TenantId, TenantName, TestContext.Current.CancellationToken);

        // Assert — the test context's Database.MigrateAsync would have been called.
        // Since we use InMemory, we verify the context was resolved (no exception thrown)
        // and the isolator was called.
        ITenantDbIsolator isolator = sp.GetRequiredService<ITenantDbIsolator>();
        await isolator.Received(1).IsolateAsync(
            Arg.Any<DbContext>(), TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvisionAsync_WithDataSeeder_CallsSeedTenantAsync()
    {
        // Arrange
        ServiceCollection services = BuildServicesWithTestContext();
        IDataSeeder seeder = Substitute.For<IDataSeeder>();
        services.AddSingleton(seeder);
        using ServiceProvider sp = services.BuildServiceProvider();

        AutoTenantProvisioner sut = CreateProvisioner(sp);

        // Act
        await sut.ProvisionAsync(TenantId, TenantName, TestContext.Current.CancellationToken);

        // Assert
        await seeder.Received(1).SeedTenantAsync(TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvisionAsync_NoDataSeeder_CompletesWithoutError()
    {
        // Arrange — no IDataSeeder registered
        ServiceCollection services = BuildServicesWithTestContext();
        using ServiceProvider sp = services.BuildServiceProvider();

        AutoTenantProvisioner sut = CreateProvisioner(sp);

        // Act & Assert — should not throw
        await sut.ProvisionAsync(TenantId, TenantName, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProvisionAsync_ActivatesTenantContext()
    {
        // Arrange
        ServiceCollection services = BuildServicesWithTestContext();
        Guid? capturedTenantId = null;
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(ci =>
            {
                capturedTenantId = ci.Arg<Guid>();
                return Substitute.For<IDisposable>();
            });
        services.AddScoped(_ => currentTenant);
        using ServiceProvider sp = services.BuildServiceProvider();

        AutoTenantProvisioner sut = CreateProvisioner(sp);

        // Act
        await sut.ProvisionAsync(TenantId, TenantName, TestContext.Current.CancellationToken);

        // Assert
        capturedTenantId.ShouldBe(TenantId);
    }

    [Fact]
    public async Task ProvisionAsync_WithSchemaProvider_QueriesSchemaName()
    {
        // Arrange
        ServiceCollection services = BuildServicesWithTestContext();
        ITenantSchemaProvider schemaProvider = Substitute.For<ITenantSchemaProvider>();
#pragma warning disable CA2012 // NSubstitute setup requires intermediate ValueTask
        schemaProvider.GetSchemaNameAsync(TenantId, Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new ValueTask<string>("tenant_acme"));
#pragma warning restore CA2012
        services.AddSingleton(schemaProvider);

        using ServiceProvider sp = services.BuildServiceProvider();

        AutoTenantProvisioner sut = CreateProvisioner(sp);

        // Act — SchemaEnsurer uses DbProviderFactories.GetFactory("Npgsql") which is
        // not registered in unit tests. The connectionString overload will throw.
        // We verify the schema provider was consulted by catching the expected exception.
        await Should.ThrowAsync<Exception>(
            () => sut.ProvisionAsync(TenantId, TenantName, TestContext.Current.CancellationToken));

        // Assert — schema provider was consulted before the factory error
#pragma warning disable CA2012
        await schemaProvider.Received(1).GetSchemaNameAsync(TenantId, Arg.Any<CancellationToken>());
#pragma warning restore CA2012
    }

    [Fact]
    public async Task ProvisionAsync_WithoutSchemaProvider_SkipsSchemaCreation()
    {
        // Arrange — no ITenantSchemaProvider registered
        ServiceCollection services = BuildServicesWithTestContext();
        using ServiceProvider sp = services.BuildServiceProvider();

        AutoTenantProvisioner sut = CreateProvisioner(sp);

        // Act & Assert — should complete without error (schema step skipped)
        await sut.ProvisionAsync(TenantId, TenantName, TestContext.Current.CancellationToken);
    }

    private static AutoTenantProvisioner CreateProvisioner(ServiceProvider sp) =>
        new(sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<AutoTenantProvisioner>>());

    private ServiceCollection BuildServicesWithTestContext()
    {
        ServiceCollection services = [];
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Register a test DbContext as isolated — uses SQLite in-memory so MigrateAsync works.
        services.AddSingleton(new IsolatedDbContextMarker(typeof(TestTenantDbContext)));
        DbConnection connection = _connection;
        services.AddDbContext<TestTenantDbContext>(opts => opts.UseSqlite(connection));

        // Required infrastructure
        ITenantDbIsolator isolator = Substitute.For<ITenantDbIsolator>();
        services.AddSingleton(isolator);

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Substitute.For<IDisposable>());
        services.AddScoped(_ => currentTenant);

        return services;
    }
}

/// <summary>
/// Minimal DbContext for testing AutoTenantProvisioner.
/// Uses InMemory provider (MigrateAsync is a no-op with InMemory).
/// </summary>
internal sealed class TestTenantDbContext(DbContextOptions<TestTenantDbContext> options) : DbContext(options);
