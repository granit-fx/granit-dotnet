using System.Reflection;
using System.Runtime.CompilerServices;
using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Granit.Documents.PublicLinks.Options;
using Granit.Guids;
using Granit.Users;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Tests;

public sealed class DocumentPublicLinkServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly byte[] SigningKey = [.. Enumerable.Repeat<byte>(0x42, 32)];

    private readonly IDocumentPublicLinkStore _store = Substitute.For<IDocumentPublicLinkStore>();
    private readonly IDocumentService _documents = Substitute.For<IDocumentService>();
    private readonly IGuidGenerator _guids = Substitute.For<IGuidGenerator>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly GranitDocumentsPublicLinksOptions _options = new()
    {
        DefaultTtl = TimeSpan.FromDays(7),
        MaxTtl = TimeSpan.FromDays(30),
        SigningKey = SigningKey,
        DefaultMaxUses = null,
    };

    private DocumentPublicLinkService BuildSut(ICurrentUserService? user = null)
    {
        IOptionsMonitor<GranitDocumentsPublicLinksOptions> monitor = Substitute.For<IOptionsMonitor<GranitDocumentsPublicLinksOptions>>();
        monitor.CurrentValue.Returns(_options);
        _guids.Create().Returns(_ => Guid.NewGuid());
        return new DocumentPublicLinkService(_store, _documents, _guids, _time, monitor, distributedEventBus: null, currentUser: user);
    }

    [Fact]
    public async Task CreateAsync_DefaultsTtl_WhenZero()
    {
        var docId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, Guid.NewGuid()));

        DocumentPublicLinkService sut = BuildSut();

        DocumentPublicLinkCreationResult result = await sut.CreateAsync(
            docId, PublicLinkScope.Download, TimeSpan.Zero, maxUses: null,
            TestContext.Current.CancellationToken);

        result.Link.ExpiresAt.ShouldBe(Now.Add(_options.DefaultTtl));
    }

    [Fact]
    public async Task CreateAsync_CapsTtl_AtMaxTtl()
    {
        var docId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, Guid.NewGuid()));

        DocumentPublicLinkService sut = BuildSut();

        DocumentPublicLinkCreationResult result = await sut.CreateAsync(
            docId, PublicLinkScope.View, TimeSpan.FromDays(365), maxUses: 5,
            TestContext.Current.CancellationToken);

        result.Link.ExpiresAt.ShouldBe(Now.Add(_options.MaxTtl));
        result.Link.MaxUses.ShouldBe(5);
        result.Link.Scope.ShouldBe(PublicLinkScope.View);
    }

    [Fact]
    public async Task CreateAsync_AppliesDefaultMaxUses_WhenCallerLeavesNull()
    {
        var docId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, Guid.NewGuid()));
        _options.DefaultMaxUses = 3;

        DocumentPublicLinkService sut = BuildSut();

        DocumentPublicLinkCreationResult result = await sut.CreateAsync(
            docId, PublicLinkScope.Download, TimeSpan.FromHours(1), maxUses: null,
            TestContext.Current.CancellationToken);

        result.Link.MaxUses.ShouldBe(3);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenSigningKeyEmpty()
    {
        _options.SigningKey = [];
        DocumentPublicLinkService sut = BuildSut();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.CreateAsync(Guid.NewGuid(), PublicLinkScope.Download,
                TimeSpan.FromHours(1), null, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("SigningKey");
        await _store.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenDocumentMissing()
    {
        var docId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>()).Returns((Document?)null);

        DocumentPublicLinkService sut = BuildSut();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.CreateAsync(docId, PublicLinkScope.Download,
                TimeSpan.FromHours(1), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_PersistsTokenHashOnly_AndReturnsRawToken()
    {
        var docId = Guid.NewGuid();
        _documents.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(BuildDocument(docId, Guid.NewGuid()));

        DocumentPublicLinkService sut = BuildSut();

        DocumentPublicLinkCreationResult result = await sut.CreateAsync(
            docId, PublicLinkScope.Download, TimeSpan.FromHours(1), null,
            TestContext.Current.CancellationToken);

        result.Token.Value.Length.ShouldBe(PublicLinkToken.EncodedLength);
        byte[] expected = PublicLinkTokenFactory.ComputeHash(result.Token.Value, SigningKey);
        result.Link.TokenHash.ShouldBe(expected);
        await _store.Received(1).AddAsync(Arg.Any<DocumentPublicLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_LoadsLinkAndPersists()
    {
        var docId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var link = DocumentPublicLink.Create(
            linkId, docId, tenantId: Guid.NewGuid(),
            tokenHash: [.. Enumerable.Repeat<byte>(0x01, 32)],
            PublicLinkScope.Download,
            expiresAt: Now.AddDays(1), maxUses: null, _time);
        _store.FindByIdAsync(linkId, Arg.Any<CancellationToken>()).Returns(link);

        DocumentPublicLinkService sut = BuildSut();
        await sut.RevokeAsync(linkId, "abuse", TestContext.Current.CancellationToken);

        link.RevokedAt.ShouldNotBeNull();
        link.RevocationReason.ShouldBe("abuse");
        await _store.Received(1).UpdateAsync(link, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_ThrowsWhenLinkMissing()
    {
        _store.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DocumentPublicLink?)null);
        DocumentPublicLinkService sut = BuildSut();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.RevokeAsync(Guid.NewGuid(), null, TestContext.Current.CancellationToken));
    }

    private static Document BuildDocument(Guid id, Guid tenantId)
    {
        // Document.Create requires a Folder graph — bypass via uninitialized
        // object + reflection for unit-level isolation. The service only reads
        // Id + TenantId.
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

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
