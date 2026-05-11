using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Tests;

public sealed class EfDocumentPublicLinkStoreSqliteTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);
    private SqliteConnection _connection = null!;
    private IDbContextFactory<DocumentsPublicLinksDbContext> _factory = null!;
    private EfDocumentPublicLinkStore _sut = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        DbContextOptions<DocumentsPublicLinksDbContext> options =
            new DbContextOptionsBuilder<DocumentsPublicLinksDbContext>()
                .UseSqlite(_connection)
                .Options;

        await using DocumentsPublicLinksDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new SingletonFactory(options);
        _sut = new EfDocumentPublicLinkStore(_factory);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private static DocumentPublicLink NewLink(Guid documentId, Guid? tenantId, byte[]? hash = null) =>
        DocumentPublicLink.Create(
            Guid.NewGuid(),
            documentId,
            tenantId,
            hash ?? RandomHash(),
            PublicLinkScope.Download,
            Now.AddDays(1),
            maxUses: null,
            new FixedTime(Now));

    private static byte[] RandomHash()
    {
        byte[] h = new byte[32];
        Random.Shared.NextBytes(h);
        return h;
    }

    [Fact]
    public async Task AddAsync_RoundTrips_ViaResolveByTokenHash()
    {
        byte[] hash = RandomHash();
        DocumentPublicLink link = NewLink(Guid.NewGuid(), Guid.NewGuid(), hash);

        await _sut.AddAsync(link, TestContext.Current.CancellationToken);

        DocumentPublicLink? found = await _sut.ResolveByTokenHashAsync(
            hash, TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(link.Id);
        found.Scope.ShouldBe(PublicLinkScope.Download);
    }

    [Fact]
    public async Task ResolveByTokenHashAsync_ReturnsNull_OnMiss()
    {
        DocumentPublicLink? found = await _sut.ResolveByTokenHashAsync(
            RandomHash(), TestContext.Current.CancellationToken);
        found.ShouldBeNull();
    }

    [Fact]
    public async Task UniqueIndex_OnTokenHash_RejectsCollision()
    {
        byte[] hash = RandomHash();
        await _sut.AddAsync(NewLink(Guid.NewGuid(), Guid.NewGuid(), hash),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DbUpdateException>(() =>
            _sut.AddAsync(NewLink(Guid.NewGuid(), Guid.NewGuid(), hash),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListForDocumentAsync_ReturnsEveryRowForTheDocument()
    {
        var docId = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        var a = DocumentPublicLink.Create(
            Guid.NewGuid(), docId, tenant, RandomHash(), PublicLinkScope.Download,
            Now.AddDays(1), null, new FixedTime(Now));
        var b = DocumentPublicLink.Create(
            Guid.NewGuid(), docId, tenant, RandomHash(), PublicLinkScope.View,
            Now.AddDays(2), null, new FixedTime(Now.AddSeconds(5)));
        var other = DocumentPublicLink.Create(
            Guid.NewGuid(), Guid.NewGuid(), tenant, RandomHash(), PublicLinkScope.Download,
            Now.AddDays(1), null, new FixedTime(Now));

        await _sut.AddAsync(a, TestContext.Current.CancellationToken);
        await _sut.AddAsync(b, TestContext.Current.CancellationToken);
        await _sut.AddAsync(other, TestContext.Current.CancellationToken);

        IReadOnlyList<DocumentPublicLink> list = await _sut.ListForDocumentAsync(
            docId, TestContext.Current.CancellationToken);

        list.Count.ShouldBe(2);
        list.Select(l => l.Id).ShouldBe([a.Id, b.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task FindByIdAsync_LoadsTrackedRow()
    {
        DocumentPublicLink link = NewLink(Guid.NewGuid(), Guid.NewGuid());
        await _sut.AddAsync(link, TestContext.Current.CancellationToken);

        DocumentPublicLink? reloaded = await _sut.FindByIdAsync(
            link.Id, TestContext.Current.CancellationToken);

        reloaded.ShouldNotBeNull();
        reloaded.Id.ShouldBe(link.Id);
    }

    private sealed class SingletonFactory(DbContextOptions<DocumentsPublicLinksDbContext> options)
        : IDbContextFactory<DocumentsPublicLinksDbContext>
    {
        public DocumentsPublicLinksDbContext CreateDbContext() => new(options);
        public Task<DocumentsPublicLinksDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentsPublicLinksDbContext(options));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
