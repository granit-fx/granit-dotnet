using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Tests;

public sealed class AssetMetadataStoreSqliteTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private IDbContextFactory<AssetMetadataDbContext> _factory = null!;
    private AssetMetadataStore _sut = null!;
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        DbContextOptions<AssetMetadataDbContext> options = new DbContextOptionsBuilder<AssetMetadataDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using AssetMetadataDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new SingletonDbContextFactory(options);
        _sut = new AssetMetadataStore(_factory);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private static DocumentAssetMetadata NewRow(Guid documentId, Guid versionId) =>
        DocumentAssetMetadata.Create(
            Guid.NewGuid(), Guid.NewGuid(), documentId, versionId, "image/jpeg", Now);

    [Fact]
    public async Task AddAsync_then_GetByVersionAsync_round_trips_with_typed_columns()
    {
        DocumentAssetMetadata m = NewRow(Guid.NewGuid(), Guid.NewGuid());
        m.MarkExtracting();
        m.ApplyExtraction(new AssetMetadataResult(
            "exif",
            new Dictionary<string, string?>
            {
                ["Make"] = "Canon",
                ["GpsLatitude"] = "48.8566",
            })
        {
            Width = 4000,
            Height = 3000,
            CameraMake = "Canon",
            Iso = 200,
            GpsLatitude = 48.8566,
            TakenAt = Now,
        });
        m.MarkReady(Now.AddSeconds(1));

        await _sut.AddAsync(m, TestContext.Current.CancellationToken);

        DocumentAssetMetadata? found = await _sut.GetByVersionAsync(
            m.DocumentVersionId, TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(m.Id);
        found.Status.ShouldBe(AssetMetadataStatus.Ready);
        found.Width.ShouldBe(4000);
        found.CameraMake.ShouldBe("Canon");
        found.Iso.ShouldBe(200);
        found.GpsLatitude.ShouldBe(48.8566);
        found.RawMetadata.ShouldContainKeyAndValue("exif:Make", "Canon");
        found.RawMetadata.ShouldContainKeyAndValue("exif:GpsLatitude", "48.8566");
    }

    [Fact]
    public async Task AddAsync_enforces_unique_version_id()
    {
        var docId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        DocumentAssetMetadata first = NewRow(docId, versionId);
        await _sut.AddAsync(first, TestContext.Current.CancellationToken);

        DocumentAssetMetadata duplicate = NewRow(docId, versionId);
        await Should.ThrowAsync<DbUpdateException>(() =>
            _sut.AddAsync(duplicate, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_persists_state_transitions_and_extracted_payload()
    {
        DocumentAssetMetadata m = NewRow(Guid.NewGuid(), Guid.NewGuid());
        await _sut.AddAsync(m, TestContext.Current.CancellationToken);

        m.MarkExtracting();
        m.ApplyExtraction(new AssetMetadataResult(
            "exif", new Dictionary<string, string?> { ["Make"] = "Sony" })
        {
            CameraMake = "Sony",
        });
        m.MarkReady(Now.AddSeconds(2));

        await _sut.UpdateAsync(m, TestContext.Current.CancellationToken);

        DocumentAssetMetadata? reread = await _sut.GetByVersionAsync(
            m.DocumentVersionId, TestContext.Current.CancellationToken);

        reread.ShouldNotBeNull();
        reread.Status.ShouldBe(AssetMetadataStatus.Ready);
        reread.CameraMake.ShouldBe("Sony");
        reread.ExtractorCount.ShouldBe(1);
        reread.CompletedAt.ShouldBe(Now.AddSeconds(2));
    }

    [Fact]
    public async Task ListForDocumentAsync_returns_every_row_for_the_document()
    {
        var docId = Guid.NewGuid();
        var v1 = DocumentAssetMetadata.Create(
            Guid.NewGuid(), Guid.NewGuid(), docId, Guid.NewGuid(), "image/jpeg", Now);
        var v2 = DocumentAssetMetadata.Create(
            Guid.NewGuid(), Guid.NewGuid(), docId, Guid.NewGuid(), "image/jpeg", Now.AddSeconds(5));

        await _sut.AddAsync(v2, TestContext.Current.CancellationToken);
        await _sut.AddAsync(v1, TestContext.Current.CancellationToken);

        IReadOnlyList<DocumentAssetMetadata> all = await _sut.ListForDocumentAsync(
            docId, TestContext.Current.CancellationToken);

        all.Count.ShouldBe(2);
        System.Linq.Enumerable.Select(all, m => m.Id).ShouldBe([v1.Id, v2.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task DeleteForDocumentAsync_drops_every_row_for_the_document()
    {
        var docId = Guid.NewGuid();
        await _sut.AddAsync(NewRow(docId, Guid.NewGuid()), TestContext.Current.CancellationToken);
        await _sut.AddAsync(NewRow(docId, Guid.NewGuid()), TestContext.Current.CancellationToken);
        await _sut.AddAsync(NewRow(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        await _sut.DeleteForDocumentAsync(docId, TestContext.Current.CancellationToken);

        IReadOnlyList<DocumentAssetMetadata> remaining = await _sut.ListForDocumentAsync(
            docId, TestContext.Current.CancellationToken);
        remaining.ShouldBeEmpty();
    }

    private sealed class SingletonDbContextFactory(DbContextOptions<AssetMetadataDbContext> options)
        : IDbContextFactory<AssetMetadataDbContext>
    {
        public AssetMetadataDbContext CreateDbContext() => new(options);
        public Task<AssetMetadataDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssetMetadataDbContext(options));
    }
}
