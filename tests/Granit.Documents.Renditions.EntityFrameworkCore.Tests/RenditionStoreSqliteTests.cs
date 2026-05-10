using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Tests;

public sealed class RenditionStoreSqliteTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private IDbContextFactory<RenditionsDbContext> _factory = null!;
    private RenditionStore _sut = null!;
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        DbContextOptions<RenditionsDbContext> options = new DbContextOptionsBuilder<RenditionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using RenditionsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new SingletonDbContextFactory(options);
        _sut = new RenditionStore(_factory);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task AddAsync_then_FindAsync_should_round_trip()
    {
        var r = DocumentRendition.CreatePending(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            RenditionType.Thumbnail, "image/webp", Now);

        await _sut.AddAsync(r, TestContext.Current.CancellationToken);

        DocumentRendition? found = await _sut.FindAsync(
            r.DocumentVersionId, RenditionType.Thumbnail, "image/webp",
            TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(r.Id);
        found.Status.ShouldBe(RenditionStatus.Pending);
    }

    [Fact]
    public async Task ListForVersionAsync_should_order_by_type_then_format()
    {
        var versionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var webJpg = DocumentRendition.CreatePending(
            Guid.NewGuid(), null, documentId, versionId, RenditionType.Web, "image/jpeg", Now);
        var thumbPng = DocumentRendition.CreatePending(
            Guid.NewGuid(), null, documentId, versionId, RenditionType.Thumbnail, "image/png", Now);
        var thumbWebp = DocumentRendition.CreatePending(
            Guid.NewGuid(), null, documentId, versionId, RenditionType.Thumbnail, "image/webp", Now);

        await _sut.AddAsync(webJpg, TestContext.Current.CancellationToken);
        await _sut.AddAsync(thumbPng, TestContext.Current.CancellationToken);
        await _sut.AddAsync(thumbWebp, TestContext.Current.CancellationToken);

        System.Collections.Generic.IReadOnlyList<DocumentRendition> ordered =
            await _sut.ListForVersionAsync(versionId, TestContext.Current.CancellationToken);

        ordered.Select(r => (r.Type, r.Format)).ToArray().ShouldBe(
        [
            (RenditionType.Thumbnail, "image/png"),
            (RenditionType.Thumbnail, "image/webp"),
            (RenditionType.Web, "image/jpeg"),
        ]);
    }

    [Fact]
    public async Task ListForDocumentAsync_should_return_all_renditions_across_versions()
    {
        var documentId = Guid.NewGuid();
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();

        await _sut.AddAsync(DocumentRendition.CreatePending(
            Guid.NewGuid(), null, documentId, v1, RenditionType.Thumbnail, "image/png", Now),
            TestContext.Current.CancellationToken);
        await _sut.AddAsync(DocumentRendition.CreatePending(
            Guid.NewGuid(), null, documentId, v2, RenditionType.Thumbnail, "image/png", Now),
            TestContext.Current.CancellationToken);

        System.Collections.Generic.IReadOnlyList<DocumentRendition> all =
            await _sut.ListForDocumentAsync(documentId, TestContext.Current.CancellationToken);
        all.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_should_persist_status_transition()
    {
        var r = DocumentRendition.CreatePending(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            RenditionType.Thumbnail, "image/png", Now);
        await _sut.AddAsync(r, TestContext.Current.CancellationToken);

        r.MarkGenerating();
        r.MarkReady(Guid.NewGuid(), sizeBytes: 4096, width: 200, height: 200, now: Now.AddSeconds(1));
        await _sut.UpdateAsync(r, TestContext.Current.CancellationToken);

        DocumentRendition? reloaded = await _sut.FindAsync(
            r.DocumentVersionId, RenditionType.Thumbnail, "image/png", TestContext.Current.CancellationToken);
        reloaded.ShouldNotBeNull();
        reloaded.Status.ShouldBe(RenditionStatus.Ready);
        reloaded.SizeBytes.ShouldBe(4096);
    }

    [Fact]
    public async Task DeleteForDocumentAsync_should_remove_all_rows_for_document()
    {
        var documentId = Guid.NewGuid();
        await _sut.AddAsync(DocumentRendition.CreatePending(
            Guid.NewGuid(), null, documentId, Guid.NewGuid(),
            RenditionType.Thumbnail, "image/png", Now),
            TestContext.Current.CancellationToken);
        await _sut.AddAsync(DocumentRendition.CreatePending(
            Guid.NewGuid(), null, documentId, Guid.NewGuid(),
            RenditionType.Web, "image/webp", Now),
            TestContext.Current.CancellationToken);

        await _sut.DeleteForDocumentAsync(documentId, TestContext.Current.CancellationToken);

        System.Collections.Generic.IReadOnlyList<DocumentRendition> remaining =
            await _sut.ListForDocumentAsync(documentId, TestContext.Current.CancellationToken);
        remaining.ShouldBeEmpty();
    }

    private sealed class SingletonDbContextFactory(DbContextOptions<RenditionsDbContext> options)
        : IDbContextFactory<RenditionsDbContext>
    {
        public RenditionsDbContext CreateDbContext() => new(options);
        public Task<RenditionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<RenditionsDbContext>(new RenditionsDbContext(options));
    }
}
