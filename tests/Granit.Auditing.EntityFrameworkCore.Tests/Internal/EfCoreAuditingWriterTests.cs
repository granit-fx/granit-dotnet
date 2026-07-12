using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Events;
using Granit.Events;
using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditingWriterTests
{
    private readonly IIntegrationEventDispatcher _eventDispatcher = Substitute.For<IIntegrationEventDispatcher>();

    [Fact]
    public async Task WriteAsync_PersistsEntry()
    {
        DbContextOptions<AuditingDbContext> dbOptions = CreateOptions();
        EfCoreAuditingWriter writer = CreateWriter(dbOptions);

        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.ConfigurationChange,
        };

        await writer.WriteAsync(entry, TestContext.Current.CancellationToken);

        await using AuditingDbContext verifyCtx = new(dbOptions, GranitDesignTime.CurrentTenant);
        AuditEntry? persisted = await verifyCtx.AuditEntries.FindAsync([entry.Id], TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.UserId.ShouldBe("user-1");
        persisted.Category.ShouldBe(AuditCategory.ConfigurationChange);
    }

    [Fact]
    public async Task WriteAsync_EmptyId_PopulatesEntryId()
    {
        DbContextOptions<AuditingDbContext> dbOptions = CreateOptions();
        EfCoreAuditingWriter writer = CreateWriter(dbOptions);

        AuditEntry entry = new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.PrivilegedAccess,
        };

        await writer.WriteAsync(entry, TestContext.Current.CancellationToken);

        entry.Id.ShouldNotBe(Guid.Empty);

        await using AuditingDbContext verifyCtx = new(dbOptions, GranitDesignTime.CurrentTenant);
        AuditEntry? persisted = await verifyCtx.AuditEntries.FindAsync([entry.Id], TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteAsync_DispatchesAuditEntryPersistedEto()
    {
        DbContextOptions<AuditingDbContext> dbOptions = CreateOptions();
        EfCoreAuditingWriter writer = CreateWriter(dbOptions);

        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.AccessDenied,
        };

        await writer.WriteAsync(entry, TestContext.Current.CancellationToken);

        await _eventDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IIntegrationEvent>>(events =>
                events.Count == 1 &&
                events[0] is AuditEntryPersistedEto &&
                ((AuditEntryPersistedEto)events[0]).Id == entry.Id &&
                ((AuditEntryPersistedEto)events[0]).Category == AuditCategory.AccessDenied),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_WithEntityChanges_PersistsHierarchy()
    {
        DbContextOptions<AuditingDbContext> dbOptions = CreateOptions();
        EfCoreAuditingWriter writer = CreateWriter(dbOptions);

        var entryId = Guid.NewGuid();
        var changeId = Guid.NewGuid();

        AuditEntry entry = new()
        {
            Id = entryId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange
                {
                    Id = changeId,
                    AuditEntryId = entryId,
                    EntityType = "Patient",
                    EntityId = "42",
                    ChangeType = AuditChangeType.Modified,
                    PropertyChanges =
                    [
                        new AuditPropertyChange
                        {
                            Id = Guid.NewGuid(),
                            AuditEntityChangeId = changeId,
                            PropertyName = "Name",
                            OriginalValue = "Old",
                            NewValue = "New",
                        },
                    ],
                },
            ],
        };

        await writer.WriteAsync(entry, TestContext.Current.CancellationToken);

        await using AuditingDbContext verifyCtx = new(dbOptions, GranitDesignTime.CurrentTenant);
        AuditEntry? persisted = await verifyCtx.AuditEntries
            .Include(e => e.EntityChanges)
            .ThenInclude(ec => ec.PropertyChanges)
            .FirstOrDefaultAsync(e => e.Id == entryId, TestContext.Current.CancellationToken);

        persisted.ShouldNotBeNull();
        persisted.EntityChanges.ShouldHaveSingleItem();
        persisted.EntityChanges.First().PropertyChanges.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task WriteAsync_NullEntry_ThrowsArgumentNullException()
    {
        DbContextOptions<AuditingDbContext> dbOptions = CreateOptions();
        EfCoreAuditingWriter writer = CreateWriter(dbOptions);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            writer.WriteAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- Helpers ---

    private static DbContextOptions<AuditingDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private EfCoreAuditingWriter CreateWriter(DbContextOptions<AuditingDbContext> dbOptions)
    {
        AuditPersistencePipeline pipeline = new(
            new TestDbContextFactory(dbOptions),
            _eventDispatcher,
            new SimpleGuidGenerator(),
            new AuditingMetrics(new TestMeterFactory()),
            NullLogger<AuditPersistencePipeline>.Instance);

        return new EfCoreAuditingWriter(pipeline);
    }

    private sealed class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) => new(options);

        public void Dispose()
        {
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<AuditingDbContext> options)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options, GranitDesignTime.CurrentTenant);
    }
}
