using System.Diagnostics.Metrics;
using Granit.Caching.Extensions;
using Granit.Documents.Authorization;
using Granit.Documents.Diagnostics;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Authorization;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Documents.EntityFrameworkCore.Tests.Authorization;

/// <summary>
/// SQLite + real FusionCache integration test for the F6.3 cache layer. Validates the hit /
/// miss path, the cache hit / miss metrics, and the tag-based invalidation flow on share
/// grant / revoke and folder path change events.
/// </summary>
public sealed class CachedEffectivePermissionResolverTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TestFactory _factory = null!;
    private CachedEffectivePermissionResolver _sut = null!;
    private EffectivePermissionResolver _inner = null!;
    private IFusionCache _cache = null!;
    private TestMeterListener _meter = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestFactory(options);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        _inner = new EffectivePermissionResolver(_factory, clock);

        // Real FusionCache via Granit.Caching — covers the tag invalidation pipeline that
        // the production decorator relies on. Tenant context is set explicitly so cache
        // keys stay scoped to TenantId.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddSingleton(tenant);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddGranitCaching();
        ServiceProvider provider = services.BuildServiceProvider();
        _cache = provider.GetRequiredService<IFusionCache>();

        _meter = new TestMeterListener();
        DocumentsMetrics metrics = new(_meter);

        _sut = new CachedEffectivePermissionResolver(
            _inner, _cache, tenant, metrics,
            Microsoft.Extensions.Options.Options.Create(
                new GranitDocumentsOptions { AclCacheTtl = TimeSpan.FromMinutes(5) }));
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task FirstCall_IsMiss_SecondCall_IsHit()
    {
        (Folder folder, Document doc, Guid user) = await SeedAsync();
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Edit, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel first = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);
        EffectivePermissionLevel second = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        first.ShouldBe(EffectivePermissionLevel.Edit);
        second.ShouldBe(EffectivePermissionLevel.Edit);
        _meter.Misses.ShouldBe(1);
        _meter.Hits.ShouldBe(1);
    }

    [Fact]
    public async Task EmptyPrincipal_ShortCircuits_DoesNotTouchCache()
    {
        var docId = Guid.NewGuid();
        var emptyPrincipal = new DocumentPrincipal(Guid.Empty, [], []);

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            docId, emptyPrincipal, TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.None);
        _meter.Hits.ShouldBe(0);
        _meter.Misses.ShouldBe(0);
    }

    [Fact]
    public async Task DocumentShareGranted_InvalidatesEntry()
    {
        (Folder folder, Document doc, Guid user) = await SeedAsync();

        // First call — None (no shares yet), populates cache.
        EffectivePermissionLevel first = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);
        first.ShouldBe(EffectivePermissionLevel.None);

        // Second call — still None, served from cache.
        await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        // Grant + emit event (the decorator subscribes via the AclCacheInvalidationHandler).
        var share = DocumentShare.ShareToDocument(
            Guid.NewGuid(), TenantId, doc.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, OwnerId, Now);
        await SeedShareAsync(share);
        await AclCacheInvalidationHandler.HandleAsync(
            new Events.DocumentShareGrantedEvent(
                share.Id, TenantId, share.TargetType, share.FolderId, share.DocumentId,
                share.GranteeType, share.GranteeId, share.Permission, share.IsDefault, share.ExpiresAt),
            _cache, TestContext.Current.CancellationToken);

        // Third call — must miss (invalidated) and resolve to Manage.
        EffectivePermissionLevel third = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);
        third.ShouldBe(EffectivePermissionLevel.Manage);

        _meter.Misses.ShouldBe(2); // first call + after invalidation
        _meter.Hits.ShouldBe(1); // second call
        _ = folder;
    }

    [Fact]
    public async Task FolderShareGranted_InvalidatesAncestorTaggedEntries()
    {
        (Folder folder, Document doc, Guid user) = await SeedAsync();
        // Seed an unrelated share so the cached entry is tagged with the doc's folder.
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Read, isDefault: true, OwnerId, Now));

        await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        // New folder share grant on the same folder → handler invalidates by folder tag.
        var upgrade = DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, isDefault: true, OwnerId, Now);
        await SeedShareAsync(upgrade);
        await AclCacheInvalidationHandler.HandleAsync(
            new Events.DocumentShareGrantedEvent(
                upgrade.Id, TenantId, upgrade.TargetType, upgrade.FolderId, upgrade.DocumentId,
                upgrade.GranteeType, upgrade.GranteeId, upgrade.Permission, upgrade.IsDefault, upgrade.ExpiresAt),
            _cache, TestContext.Current.CancellationToken);

        EffectivePermissionLevel after = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);
        after.ShouldBe(EffectivePermissionLevel.Manage);

        _meter.Misses.ShouldBe(2);
    }

    [Fact]
    public async Task FolderPathChanged_BulkInvalidatesTenantEntries()
    {
        (Folder folder, Document doc, Guid user) = await SeedAsync();
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Read, isDefault: true, OwnerId, Now));

        // Populate cache for two different documents.
        await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        await AclCacheInvalidationHandler.HandleAsync(
            new Events.FolderPathChangedEvent(folder.Id, TenantId, "/old", "/new"),
            _cache, TestContext.Current.CancellationToken);

        // Next call must miss (acl:all wiped).
        await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        _meter.Misses.ShouldBe(2);
    }

    // ---- helpers --------------------------------------------------------

    private async Task<(Folder, Document, Guid user)> SeedAsync()
    {
        var user = Guid.NewGuid();
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync();
        var root = Folder.CreateTenantRoot(Guid.NewGuid(), TenantId, OwnerId);
        var folder = Folder.Create(Guid.NewGuid(), root, "Contracts", OwnerId);
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "doc.pdf");
        db.Folders.Add(root);
        db.Folders.Add(folder);
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return (folder, doc, user);
    }

    private async Task SeedShareAsync(DocumentShare share)
    {
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync();
        db.DocumentShares.Add(share);
        await db.SaveChangesAsync();
    }

    private sealed class TestFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }

    /// <summary>
    /// Minimal IMeterFactory + MeterListener that observes the Granit.Documents counters and
    /// exposes hit/miss totals. Avoids pulling DiagnosticsMetricsListener into the test pkg.
    /// </summary>
    private sealed class TestMeterListener : IMeterFactory
    {
        private readonly Meter _meter = new("Granit.Documents");
        private readonly MeterListener _listener;

        public long Hits { get; private set; }
        public long Misses { get; private set; }

        public TestMeterListener()
        {
            _listener = new MeterListener();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Granit.Documents")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((inst, value, _, _) =>
            {
                if (inst.Name == "granit.documents.acl.cache.hits")
                {
                    Hits += value;
                }
                else if (inst.Name == "granit.documents.acl.cache.misses")
                {
                    Misses += value;
                }
            });
            _listener.Start();
        }

        public Meter Create(MeterOptions options) => _meter;

        public void Dispose()
        {
            _listener.Dispose();
            _meter.Dispose();
        }
    }
}
