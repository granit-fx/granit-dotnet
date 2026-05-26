using Granit.DataFiltering;
using Granit.Indexing.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests;

/// <summary>
/// Folding-path tests for the new <c>ConfigureIndexingModule</c> extension — proves a
/// host-owned DbContext can map IndexedEntryRow tables WITHOUT the isolated
/// IndexingDbContext + AddGranitIndexingEntityFrameworkCore wiring.
/// </summary>
public sealed class ConfigureIndexingModuleTests
{
    [Fact]
    public async Task Host_DbContext_can_fold_indexing_tables_via_ConfigureIndexingModule()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        MutableTenant tenant = new() { Id = Guid.NewGuid() };
        DbContextOptions<HostFoldedDbContext> options = new DbContextOptionsBuilder<HostFoldedDbContext>()
            .UseSqlite(connection)
            .Options;

        await using HostFoldedDbContext db = new(options, tenant);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // IndexedEntryRow<Guid> table was mapped by the extension.
        IEntityType? entryType = db.Model.FindEntityType(typeof(IndexedEntryRow<Guid>));
        entryType.ShouldNotBeNull();

        // Rebuild-checkpoint table is also mapped — same single-source-of-truth as the
        // isolated DbContext.
        IEntityType? checkpointType = db.Model.FindEntityType(
            "Granit.Indexing.EntityFrameworkCore.IndexingRebuildCheckpointRow");
        checkpointType.ShouldNotBeNull();

        // Index + insert via the host context, prove the table is fully wired.
        IndexedEntryRow<Guid> row = new()
        {
            Key = Guid.NewGuid(),
            TenantId = tenant.Id,
            Content = "hello folded host context",
            Language = "en",
            CharCount = 25,
        };
        db.Set<IndexedEntryRow<Guid>>().Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IndexedEntryRow<Guid>? roundTripped = await db.Set<IndexedEntryRow<Guid>>()
            .SingleOrDefaultAsync(r => r.Key == row.Key, TestContext.Current.CancellationToken);
        roundTripped.ShouldNotBeNull();
        roundTripped.Content.ShouldBe("hello folded host context");

        await connection.DisposeAsync();
    }

    [Fact]
    public void ConfigureIndexingModule_throws_when_no_key_type_supplied()
    {
        ModelBuilder mb = new();
        Should.Throw<ArgumentException>(() =>
            mb.ConfigureIndexingModule([]))
            .ParamName.ShouldBe("indexedKeyTypes");
    }

    /// <summary>
    /// Host-owned DbContext that demonstrates the folding pattern. Pretends to be the
    /// consumer's app context — declares its own DbSets PLUS the indexing tables.
    /// </summary>
    private sealed class HostFoldedDbContext(
        DbContextOptions<HostFoldedDbContext> options,
        ICurrentTenant currentTenant,
        IDataFilter? dataFilter = null)
        : GranitDbContext(options, currentTenant, dataFilter)
    {
        protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ConfigureIndexingModule(
                indexedKeyTypes: [typeof(Guid)],
                defaultDictionary: "simple",
                embeddingDimensions: null,
                isPostgres: false);
        }
    }
}
