using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Internal;
using Granit.AuditLog.EntityFrameworkCore.Internal.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.AuditLog.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditLogWriterTests
{
    [Fact]
    public async Task WriteAsync_PersistsEntry()
    {
        DbContextOptions<AuditLogDbContext> dbOptions = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(dbOptions);
        EfCoreAuditLogWriter writer = new(factory);

        AuditLogEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.ConfigurationChange,
        };

        await writer.WriteAsync(entry, TestContext.Current.CancellationToken);

        await using AuditLogDbContext verifyCtx = new(dbOptions);
        AuditLogEntry? persisted = await verifyCtx.AuditLogEntries.FindAsync([entry.Id], TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.UserId.ShouldBe("user-1");
        persisted.Category.ShouldBe(AuditLogCategory.ConfigurationChange);
    }

    [Fact]
    public async Task WriteAsync_WithEntityChanges_PersistsHierarchy()
    {
        DbContextOptions<AuditLogDbContext> dbOptions = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(dbOptions);
        EfCoreAuditLogWriter writer = new(factory);

        var entryId = Guid.NewGuid();
        var changeId = Guid.NewGuid();

        AuditLogEntry entry = new()
        {
            Id = entryId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
            EntityChanges =
            [
                new AuditEntityChange
                {
                    Id = changeId,
                    AuditLogEntryId = entryId,
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

        await using AuditLogDbContext verifyCtx = new(dbOptions);
        AuditLogEntry? persisted = await verifyCtx.AuditLogEntries
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
        DbContextOptions<AuditLogDbContext> dbOptions = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(dbOptions);
        EfCoreAuditLogWriter writer = new(factory);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            writer.WriteAsync(null!, TestContext.Current.CancellationToken));
    }

    private sealed class TestDbContextFactory(DbContextOptions<AuditLogDbContext> options)
        : IDbContextFactory<AuditLogDbContext>
    {
        public AuditLogDbContext CreateDbContext() => new(options);
    }
}
