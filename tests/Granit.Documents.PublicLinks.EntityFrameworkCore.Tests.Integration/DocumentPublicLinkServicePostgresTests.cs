using System.Reflection;
using System.Runtime.CompilerServices;
using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Granit.Documents.PublicLinks.Events;
using Granit.Documents.PublicLinks.Internal;
using Granit.Documents.PublicLinks.Options;
using Granit.Events;
using Granit.Guids;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed integration tests for <see cref="EfDocumentPublicLinkStore"/> +
/// <see cref="DocumentPublicLinkService"/> (F18.2).
/// </summary>
public sealed class DocumentPublicLinkServicePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] SigningKey = [.. Enumerable.Repeat<byte>(0x42, 32)];

    private readonly PostgresFixture _postgres;
    private TestFactory _factory = null!;
    private EfDocumentPublicLinkStore _store = null!;
    private DocumentPublicLinkService _sut = null!;
    private IDocumentService _documents = null!;
    private CapturingEventBus _eventBus = null!;

    public DocumentPublicLinkServicePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<DocumentsPublicLinksDbContext> options =
            new DbContextOptionsBuilder<DocumentsPublicLinksDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;

        await using DocumentsPublicLinksDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_public_links RESTART IDENTITY CASCADE;");

        _factory = new TestFactory(options);
        _store = new EfDocumentPublicLinkStore(_factory);

        _documents = Substitute.For<IDocumentService>();
        _eventBus = new CapturingEventBus();
        _sut = BuildService();
    }

    public ValueTask DisposeAsync() => default;

    private DocumentPublicLinkService BuildService(
        GranitDocumentsPublicLinksOptions? options = null,
        ICurrentUserService? user = null)
    {
        IOptionsMonitor<GranitDocumentsPublicLinksOptions> monitor =
            Substitute.For<IOptionsMonitor<GranitDocumentsPublicLinksOptions>>();
        monitor.CurrentValue.Returns(options ?? new GranitDocumentsPublicLinksOptions
        {
            DefaultTtl = TimeSpan.FromDays(7),
            MaxTtl = TimeSpan.FromDays(30),
            SigningKey = SigningKey,
        });
        IGuidGenerator guids = Substitute.For<IGuidGenerator>();
        guids.Create().Returns(_ => Guid.NewGuid());
        return new DocumentPublicLinkService(
            _store, _documents, guids,
            new FixedTime(Now),
            monitor,
            distributedEventBus: _eventBus,
            currentUser: user);
    }

    [Fact]
    public async Task CreateAsync_PersistsLink()
    {
        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, tenantId));

        DocumentPublicLinkCreationResult result = await _sut.CreateAsync(
            docId, PublicLinkScope.Download, TimeSpan.FromHours(2), maxUses: 5,
            TestContext.Current.CancellationToken);

        result.Token.Value.Length.ShouldBe(PublicLinkToken.EncodedLength);

        await using DocumentsPublicLinksDbContext db = await _factory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        DocumentPublicLink reloaded = await db.DocumentPublicLinks
            .SingleAsync(l => l.Id == result.Link.Id, TestContext.Current.CancellationToken);

        reloaded.DocumentId.ShouldBe(docId);
        reloaded.TenantId.ShouldBe(tenantId);
        reloaded.Scope.ShouldBe(PublicLinkScope.Download);
        reloaded.MaxUses.ShouldBe(5);
        reloaded.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveByTokenHashAsync_FindsLinkAcrossTenants()
    {
        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, tenantId));

        DocumentPublicLinkCreationResult result = await _sut.CreateAsync(
            docId, PublicLinkScope.Download, TimeSpan.FromHours(2), null,
            TestContext.Current.CancellationToken);

        byte[] hash = PublicLinkTokenFactory.ComputeHash(result.Token.Value, SigningKey);
        DocumentPublicLink? resolved = await _store.ResolveByTokenHashAsync(
            hash, TestContext.Current.CancellationToken);

        resolved.ShouldNotBeNull();
        resolved.Id.ShouldBe(result.Link.Id);
        resolved.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task ListForDocumentAsync_ReturnsEveryLink()
    {
        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, tenantId));

        await _sut.CreateAsync(docId, PublicLinkScope.Download, TimeSpan.FromHours(1), null,
            TestContext.Current.CancellationToken);
        await _sut.CreateAsync(docId, PublicLinkScope.View, TimeSpan.FromHours(2), null,
            TestContext.Current.CancellationToken);

        IReadOnlyList<DocumentPublicLink> list = await _sut.ListForDocumentAsync(
            docId, TestContext.Current.CancellationToken);

        list.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RevokeAsync_SetsRevokedFields()
    {
        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, tenantId));

        DocumentPublicLinkCreationResult result = await _sut.CreateAsync(
            docId, PublicLinkScope.Download, TimeSpan.FromHours(2), null,
            TestContext.Current.CancellationToken);

        await _sut.RevokeAsync(result.Link.Id, "operator action", TestContext.Current.CancellationToken);

        await using DocumentsPublicLinksDbContext db = await _factory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        DocumentPublicLink reloaded = await db.DocumentPublicLinks
            .SingleAsync(l => l.Id == result.Link.Id, TestContext.Current.CancellationToken);

        reloaded.RevokedAt.ShouldBe(Now);
        reloaded.RevocationReason.ShouldBe("operator action");
    }

    [Fact]
    public async Task ResolveAndConsumeAsync_PublishesIntegrationEvent()
    {
        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, tenantId));

        DocumentPublicLinkCreationResult result = await _sut.CreateAsync(
            docId, PublicLinkScope.Download, TimeSpan.FromHours(2), maxUses: 5,
            TestContext.Current.CancellationToken);

        _eventBus.Captured.Clear();

        DocumentPublicLink? consumed = await _sut.ResolveAndConsumeAsync(
            result.Token.Value, clientIpMasked: "203.0.113.0", userAgent: "TestAgent/1.0",
            TestContext.Current.CancellationToken);

        consumed.ShouldNotBeNull();
        consumed.CurrentUses.ShouldBe(1);

        DocumentPublicLinkConsumedEto eto = _eventBus.Captured
            .OfType<DocumentPublicLinkConsumedEto>()
            .ShouldHaveSingleItem();
        eto.LinkId.ShouldBe(result.Link.Id);
        eto.DocumentId.ShouldBe(docId);
        eto.TenantId.ShouldBe(tenantId);
        eto.Scope.ShouldBe(PublicLinkScope.Download);
        eto.CurrentUses.ShouldBe(1);
        eto.ConsumedAt.ShouldBe(Now);
        eto.ClientIpMasked.ShouldBe("203.0.113.0");
        eto.UserAgent.ShouldBe("TestAgent/1.0");
    }

    [Fact]
    public async Task ResolveAndConsumeAsync_DoesNotPublishWhenLinkUnknown()
    {
        _eventBus.Captured.Clear();

        DocumentPublicLink? consumed = await _sut.ResolveAndConsumeAsync(
            "not-a-real-token", cancellationToken: TestContext.Current.CancellationToken);

        consumed.ShouldBeNull();
        _eventBus.Captured.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenSigningKeyEmpty()
    {
        DocumentPublicLinkService sut = BuildService(new GranitDocumentsPublicLinksOptions
        {
            SigningKey = [],
        });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.CreateAsync(Guid.NewGuid(), PublicLinkScope.Download,
                TimeSpan.FromHours(1), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TokenHash_UniqueIndex_PreventsCollision()
    {
        var docId = Guid.NewGuid();
        byte[] hash = [.. Enumerable.Repeat<byte>(0x7E, 32)];

        var first = DocumentPublicLink.Create(
            Guid.NewGuid(), docId, Guid.NewGuid(), hash,
            PublicLinkScope.Download, Now.AddDays(1), null, new FixedTime(Now));
        var duplicate = DocumentPublicLink.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), hash,
            PublicLinkScope.View, Now.AddDays(1), null, new FixedTime(Now));

        await _store.AddAsync(first, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<DbUpdateException>(() =>
            _store.AddAsync(duplicate, TestContext.Current.CancellationToken));
    }

    private static Document BuildDocument(Guid id, Guid? tenantId)
    {
        var doc = (Document)RuntimeHelpers.GetUninitializedObject(typeof(Document));
        Set(doc, nameof(Document.Id), id);
        Set(doc, nameof(Document.TenantId), tenantId);
        return doc;
    }

    private static void Set(object target, string propertyName, object? value)
    {
        PropertyInfo prop = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Property {propertyName} not found.");
        prop.SetValue(target, value);
    }

    private sealed class TestFactory(DbContextOptions<DocumentsPublicLinksDbContext> options)
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

    private sealed class CapturingEventBus : IDistributedEventBus
    {
        public List<IIntegrationEvent> Captured { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
            where TEvent : class, IIntegrationEvent
        {
            Captured.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
