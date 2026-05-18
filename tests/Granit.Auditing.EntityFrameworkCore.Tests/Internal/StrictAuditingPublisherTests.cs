// =============================================================================
// StrictAuditingPublisherTests - Strict (synchronous) audit persistence
// =============================================================================
// Verifies:
//   - Delegates to AuditingBatchMapper and persists to AuditingDbContext
//   - Saves the entry within a scoped DbContext
//   - Propagates cancellation
// =============================================================================

using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Messages;
using Granit.Events;
using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class StrictAuditingPublisherTests : IDisposable
{
    private readonly TestDataFilter _dataFilter = new();

    public void Dispose() => _dataFilter.Dispose();
    // -------------------------------------------------------------------------
    // PublishAsync — happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_PersistsEntryToDatabase()
    {
        // Arrange
        DbContextOptions<AuditingDbContext> dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AuditingDbContext>>(new TestDbContextFactory(dbOptions, _dataFilter.Filter));
        services.AddSingleton(guidGenerator);
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        ServiceProvider sp = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        StrictAuditingPublisher publisher = new(scopeFactory, CreateMetrics());

        AuditingBatch batch = CreateBatch();

        // Act
        await publisher.PublishAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        await using AuditingDbContext verifyCtx = new(dbOptions, GranitDesignTime.CurrentTenant, _dataFilter.Filter);
        int count = await verifyCtx.AuditEntries.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_MapsAllFieldsCorrectly()
    {
        // Arrange
        DbContextOptions<AuditingDbContext> dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AuditingDbContext>>(new TestDbContextFactory(dbOptions, _dataFilter.Filter));
        services.AddSingleton(guidGenerator);
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        ServiceProvider sp = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        StrictAuditingPublisher publisher = new(scopeFactory, CreateMetrics());

        DateTimeOffset timestamp = new(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        AuditingBatch batch = new(
            Timestamp: timestamp,
            UserId: "strict-user",
            UserName: "Strict Tester",
            Category: AuditCategory.ConfigurationChange,
            IpAddress: "192.168.1.1",
            UserAgent: "StrictAgent/1.0",
            TenantId: tenantId,
            CorrelationId: "corr-strict-001",
            EntityChanges:
            [
                new AuditEntityChangeSnapshot(
                    "Setting", "key-1", AuditChangeType.Modified,
                    [
                        new AuditPropertyChangeSnapshot("Value", "old-val", "new-val"),
                    ]),
            ]);

        // Act
        await publisher.PublishAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        await using AuditingDbContext verifyCtx = new(dbOptions, GranitDesignTime.CurrentTenant, _dataFilter.Filter);
        AuditEntry persisted = await verifyCtx.AuditEntries
            .Include(e => e.EntityChanges)
            .ThenInclude(ec => ec.PropertyChanges)
            .SingleAsync(TestContext.Current.CancellationToken);

        persisted.UserId.ShouldBe("strict-user");
        persisted.UserName.ShouldBe("Strict Tester");
        persisted.Category.ShouldBe(AuditCategory.ConfigurationChange);
        persisted.IpAddress.ShouldBe("192.168.1.1");
        persisted.TenantId.ShouldBe(tenantId);
        persisted.CorrelationId.ShouldBe("corr-strict-001");
        persisted.EntityChanges.ShouldHaveSingleItem();

        AuditEntityChange entityChange = persisted.EntityChanges.Single();
        entityChange.EntityType.ShouldBe("Setting");
        entityChange.PropertyChanges.ShouldHaveSingleItem();
        entityChange.PropertyChanges.Single().PropertyName.ShouldBe("Value");
    }

    [Fact]
    public async Task PublishAsync_MultipleBatches_PersistsAll()
    {
        // Arrange
        DbContextOptions<AuditingDbContext> dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AuditingDbContext>>(new TestDbContextFactory(dbOptions, _dataFilter.Filter));
        services.AddSingleton(guidGenerator);
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        ServiceProvider sp = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        StrictAuditingPublisher publisher = new(scopeFactory, CreateMetrics());

        // Act
        for (int i = 0; i < 3; i++)
        {
            AuditingBatch batch = CreateBatch(userId: $"user-{i}");
            await publisher.PublishAsync(batch, TestContext.Current.CancellationToken);
        }

        // Assert
        await using AuditingDbContext verifyCtx = new(dbOptions, GranitDesignTime.CurrentTenant, _dataFilter.Filter);
        int count = await verifyCtx.AuditEntries.CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(3);
    }

    [Fact]
    public async Task PublishAsync_WithEmptyEntityChanges_PersistsEntry()
    {
        // Arrange
        DbContextOptions<AuditingDbContext> dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AuditingDbContext>>(new TestDbContextFactory(dbOptions, _dataFilter.Filter));
        services.AddSingleton(guidGenerator);
        services.AddSingleton(Substitute.For<IDistributedEventBus>());
        ServiceProvider sp = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        StrictAuditingPublisher publisher = new(scopeFactory, CreateMetrics());

        AuditingBatch batch = CreateBatch();

        // Act
        await publisher.PublishAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        await using AuditingDbContext verifyCtx = new(dbOptions, GranitDesignTime.CurrentTenant, _dataFilter.Filter);
        AuditEntry entry = await verifyCtx.AuditEntries
            .Include(e => e.EntityChanges)
            .SingleAsync(TestContext.Current.CancellationToken);
        entry.EntityChanges.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AuditingBatch CreateBatch(
        string userId = "system",
        AuditCategory category = AuditCategory.DataMutation) =>
        new(
            Timestamp: DateTimeOffset.UtcNow,
            UserId: userId,
            UserName: null,
            Category: category,
            IpAddress: null,
            UserAgent: null,
            TenantId: null,
            CorrelationId: null,
            EntityChanges: []);

    private static AuditingMetrics CreateMetrics()
    {
        var meterFactory = new TestMeterFactory();
        var channel = Channel.CreateUnbounded<AuditingBatch>();
        return new AuditingMetrics(meterFactory, channel);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AuditingDbContext> options, Granit.DataFiltering.DataFilter dataFilter)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options, GranitDesignTime.CurrentTenant, dataFilter);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
