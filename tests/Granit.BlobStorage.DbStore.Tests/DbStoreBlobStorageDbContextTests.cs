using Granit.BlobStorage.DbStore.Entities;
using Granit.BlobStorage.DbStore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.DbStore.Tests;

public sealed class DbStoreBlobStorageDbContextTests
{
    private static DbStoreBlobStorageDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<DbStoreBlobStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // ── Schema creation ─────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureCreatedAsync_WithInMemoryProvider_DoesNotThrow()
    {
        await using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        Func<Task> act = () => ctx.Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // ── Entity configuration — table mapping ────────────────────────────────

    [Fact]
    public void Model_TableName_IsStorageBlobContents()
    {
        using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        string? tableName = ctx.Model
            .FindEntityType(typeof(DbStoreBlobContent))!
            .GetTableName();

        tableName.ShouldBe("storage_blob_contents");
    }

    // ── Entity configuration — property constraints ─────────────────────────

    [Fact]
    public void Model_ObjectKey_HasMaxLength1024_AndIsRequired()
    {
        using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(DbStoreBlobContent))!
            .FindProperty(nameof(DbStoreBlobContent.ObjectKey));

        property.ShouldNotBeNull();
        property!.GetMaxLength().ShouldBe(1024);
        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_Content_IsRequired()
    {
        using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(DbStoreBlobContent))!
            .FindProperty(nameof(DbStoreBlobContent.Content));

        property.ShouldNotBeNull();
        property!.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_CreatedAt_IsRequired()
    {
        using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(DbStoreBlobContent))!
            .FindProperty(nameof(DbStoreBlobContent.CreatedAt));

        property.ShouldNotBeNull();
        property!.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_TenantId_IsNullable()
    {
        using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(DbStoreBlobContent))!
            .FindProperty(nameof(DbStoreBlobContent.TenantId));

        property.ShouldNotBeNull();
        property!.IsNullable.ShouldBeTrue();
    }

    // ── Entity configuration — indexes ──────────────────────────────────────

    [Fact]
    public void Model_UniqueIndex_OnObjectKey()
    {
        using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(DbStoreBlobContent))!;

        IIndex? uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.IsUnique &&
                i.Properties.Any(p => p.Name == nameof(DbStoreBlobContent.ObjectKey)));

        uniqueIndex.ShouldNotBeNull("a unique index on ObjectKey must be configured");
    }

    [Fact]
    public void Model_CompositeIndex_OnTenantIdAndObjectKey()
    {
        using DbStoreBlobStorageDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(DbStoreBlobContent))!;

        IIndex? compositeIndex = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(DbStoreBlobContent.TenantId)) &&
                i.Properties.Any(p => p.Name == nameof(DbStoreBlobContent.ObjectKey)));

        compositeIndex.ShouldNotBeNull("a composite index on (TenantId, ObjectKey) must be configured");
    }

    // ── CRUD round-trip ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndReload_AllFields_MatchOriginal()
    {
        await using DbStoreBlobStorageDbContext ctx = CreateInMemory();
        var tenantId = Guid.NewGuid();
        byte[] content = [0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]; // "%PDF-1.4"

        DbStoreBlobContent entity = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ObjectKey = $"{tenantId}/medical-images/2026/03/blob-id",
            Content = content,
            CreatedAt = new DateTimeOffset(2026, 3, 14, 10, 0, 0, TimeSpan.Zero),
        };

        ctx.BlobContents.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        DbStoreBlobContent? loaded = await ctx.BlobContents
            .FindAsync([entity.Id], TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded!.TenantId.ShouldBe(tenantId);
        loaded.ObjectKey.ShouldBe(entity.ObjectKey);
        loaded.Content.ShouldBe(content);
        loaded.CreatedAt.ShouldBe(entity.CreatedAt);
    }

    [Fact]
    public async Task SaveAndReload_WithNullTenantId_ShouldPersist()
    {
        await using DbStoreBlobStorageDbContext ctx = CreateInMemory();
        byte[] content = [0x89, 0x50, 0x4E, 0x47]; // PNG magic bytes

        DbStoreBlobContent entity = new()
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            ObjectKey = "avatars/2026/03/blob-id",
            Content = content,
            CreatedAt = new DateTimeOffset(2026, 3, 14, 10, 0, 0, TimeSpan.Zero),
        };

        ctx.BlobContents.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        DbStoreBlobContent? loaded = await ctx.BlobContents
            .FindAsync([entity.Id], TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded!.TenantId.ShouldBeNull();
    }
}
