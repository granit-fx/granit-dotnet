using Granit.Guids;
using Granit.Identity.Domain;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Internal;
using Granit.MultiTenancy;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Internal;

/// <summary>
/// The single joint-hydration write path (ADR-051 B-step 3.5): every ingestion route funnels
/// through <see cref="FederatedIdentityWriter"/> so a <see cref="FederatedIdentity"/> row always
/// has a matching canonical <see cref="User"/> with the aligned identifier triple
/// <c>FederatedIdentity.Id == FederatedIdentity.UserId == User.Id</c>. These tests moved here from
/// <c>CachedUserLookupServiceTests</c> when the logic was extracted into the shared writer.
/// </summary>
public sealed class FederatedIdentityWriterTests
{
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly IUserDirectoryWriter _userDirectoryWriter = Substitute.For<IUserDirectoryWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    private FederatedIdentityWriter CreateWriter() =>
        new(_store, _userDirectoryWriter, _guidGenerator, _tenant, _timeProvider);

    private static FederatedIdentityUser CreateUser(string id = "user-1") => new(
        UserId: id, Username: "jdoe", Email: "jdoe@test.com",
        FirstName: "John", LastName: "Doe", Enabled: true);

    public FederatedIdentityWriterTests()
    {
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(Guid.NewGuid());
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        // Default: the store persists our row and returns our own Id (no insert race).
        _store.UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<FederatedIdentity>().Id);
    }

    [Fact]
    public async Task WriteAsync_OnInsert_CreatesCanonicalUser_BeforeFederatedIdentity_WithAlignedIds()
    {
        var generatedId = Guid.NewGuid();
        _guidGenerator.Create().Returns(generatedId);

        FederatedIdentityWriter writer = CreateWriter();
        await writer.WriteAsync(CreateUser(), existing: null, TestContext.Current.CancellationToken);

        await _userDirectoryWriter.Received(1).CreateAsync(
            Arg.Is<User>(u =>
                u.Id == generatedId
                && u.Email == "jdoe@test.com"
                && u.DisplayName == "John Doe"),
            Arg.Any<CancellationToken>());
        await _store.Received(1).UpsertAsync(
            Arg.Is<FederatedIdentity>(e => e.Id == generatedId && e.UserId == generatedId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_OnUpdate_PreservesExistingIdPair_AndSkipsUserCreate()
    {
        var existingId = Guid.NewGuid();
        FederatedIdentity existing = new()
        {
            Id = existingId,
            UserId = existingId,
            ExternalUserId = "user-1",
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-2),
        };

        FederatedIdentityWriter writer = CreateWriter();
        await writer.WriteAsync(CreateUser(), existing, TestContext.Current.CancellationToken);

        await _userDirectoryWriter.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _store.Received(1).UpsertAsync(
            Arg.Is<FederatedIdentity>(e => e.Id == existingId && e.UserId == existingId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_CompensatesUserDelete_WhenFederatedInsertFails()
    {
        var generatedId = Guid.NewGuid();
        _guidGenerator.Create().Returns(generatedId);
        _store.UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("store offline"));

        FederatedIdentityWriter writer = CreateWriter();

        // The canonical User is created first; if the cache insert throws (a genuine store
        // failure), the freshly-created user must be hard-deleted and the exception surfaces.
        await Should.ThrowAsync<InvalidOperationException>(() =>
            writer.WriteAsync(CreateUser(), existing: null, TestContext.Current.CancellationToken));

        await _userDirectoryWriter.Received(1).CreateAsync(
            Arg.Is<User>(u => u.Id == generatedId), Arg.Any<CancellationToken>());
        await _userDirectoryWriter.Received(1).DeleteAsync(generatedId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_CompensatesUserDelete_WithoutThrowing_OnBenignInsertRace()
    {
        // A concurrent first-login won the insert race: the store resolved the conflict by
        // updating the winner's row (which points at the winner's own User) and returns the
        // winner's Id — different from ours. Our canonical User is now orphaned; the writer must
        // delete it silently (no throw — the row exists and carries our data).
        var ourId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        _guidGenerator.Create().Returns(ourId);
        _store.UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>())
            .Returns(winnerId);

        FederatedIdentityWriter writer = CreateWriter();
        await writer.WriteAsync(CreateUser(), existing: null, TestContext.Current.CancellationToken);

        await _userDirectoryWriter.Received(1).CreateAsync(
            Arg.Is<User>(u => u.Id == ourId), Arg.Any<CancellationToken>());
        await _userDirectoryWriter.Received(1).DeleteAsync(ourId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_DoesNotCompensate_OnSuccessfulInsert()
    {
        var generatedId = Guid.NewGuid();
        _guidGenerator.Create().Returns(generatedId);

        FederatedIdentityWriter writer = CreateWriter();
        await writer.WriteAsync(CreateUser(), existing: null, TestContext.Current.CancellationToken);

        await _userDirectoryWriter.DidNotReceiveWithAnyArgs().DeleteAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WriteAsync_FallsBackToEmail_AsDisplayName_WhenNamesAreMissing()
    {
        var generatedId = Guid.NewGuid();
        _guidGenerator.Create().Returns(generatedId);

        FederatedIdentityUser nameless = new(
            UserId: "user-2", Username: "anon", Email: "anon@test.com",
            FirstName: null, LastName: null, Enabled: true);

        FederatedIdentityWriter writer = CreateWriter();
        await writer.WriteAsync(nameless, existing: null, TestContext.Current.CancellationToken);

        await _userDirectoryWriter.Received(1).CreateAsync(
            Arg.Is<User>(u => u.DisplayName == "anon@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_FindsExistingTenantScopedRow_ThenWrites()
    {
        var existingId = Guid.NewGuid();
        FederatedIdentity existing = new()
        {
            Id = existingId,
            UserId = existingId,
            ExternalUserId = "user-1",
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-2),
        };
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        FederatedIdentityWriter writer = CreateWriter();
        await writer.SyncAsync(CreateUser(), TestContext.Current.CancellationToken);

        // Existing row found → update path preserves the id pair, no canonical User created.
        await _userDirectoryWriter.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _store.Received(1).UpsertAsync(
            Arg.Is<FederatedIdentity>(e => e.Id == existingId && e.UserId == existingId),
            Arg.Any<CancellationToken>());
    }
}
