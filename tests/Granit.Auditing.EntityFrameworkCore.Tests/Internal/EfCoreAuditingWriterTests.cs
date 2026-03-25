using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditingWriterTests
{
    [Fact]
    public async Task WriteAsync_PersistsEntry()
    {
        DbContextOptions<AuditingDbContext> dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(dbOptions);
        EfCoreAuditingWriter writer = new(factory);

        AuditEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.ConfigurationChange,
        };

        await writer.WriteAsync(entry, TestContext.Current.CancellationToken);

        await using AuditingDbContext verifyCtx = new(dbOptions);
        AuditEntry? persisted = await verifyCtx.AuditEntries.FindAsync([entry.Id], TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.UserId.ShouldBe("user-1");
        persisted.Category.ShouldBe(AuditCategory.ConfigurationChange);
    }

    [Fact]
    public async Task WriteAsync_WithEntityChanges_PersistsHierarchy()
    {
        DbContextOptions<AuditingDbContext> dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(dbOptions);
        EfCoreAuditingWriter writer = new(factory);

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

        await using AuditingDbContext verifyCtx = new(dbOptions);
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
        DbContextOptions<AuditingDbContext> dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(dbOptions);
        EfCoreAuditingWriter writer = new(factory);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            writer.WriteAsync(null!, TestContext.Current.CancellationToken));
    }

    private sealed class TestDbContextFactory(DbContextOptions<AuditingDbContext> options)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options);
    }
}
