using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Domain;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Internal;

public sealed class CachedUserLookupServiceTests
{
    private readonly IUserCacheStore _store = Substitute.For<IUserCacheStore>();
    private readonly IIdentityProvider _provider = Substitute.For<IIdentityProvider>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IUserDirectoryWriter _userDirectoryWriter = Substitute.For<IUserDirectoryWriter>();
    private readonly IOptions<UserCacheOptions> _options = Microsoft.Extensions.Options.Options.Create(new UserCacheOptions
    {
        StalenessThreshold = TimeSpan.FromHours(24)
    });

    private CachedUserLookupService CreateService() => new(
        _store, _provider, _tenant, _timeProvider, _guidGenerator, _userDirectoryWriter, _options,
        NullLogger<CachedUserLookupService>.Instance);

    private static FederatedIdentityUser CreateUser(string id = "user-1") => new(
        UserId: id, Username: "jdoe", Email: "jdoe@test.com",
        FirstName: "John", LastName: "Doe", Enabled: true);

    private static FederatedIdentity CreateCacheEntry(
        string externalUserId = "user-1",
        DateTimeOffset? lastSyncedAt = null) => new()
        {
            Id = Guid.NewGuid(),
            ExternalUserId = externalUserId,
            Username = "jdoe",
            Email = "jdoe@test.com",
            FirstName = "John",
            LastName = "Doe",
            Enabled = true,
            LastSyncedAt = lastSyncedAt ?? DateTimeOffset.UtcNow
        };

    public CachedUserLookupServiceTests()
    {
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(Guid.NewGuid());
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsFreshCacheEntry()
    {
        FederatedIdentity entry = CreateCacheEntry(lastSyncedAt: DateTimeOffset.UtcNow);
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(entry);

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Username.ShouldBe("jdoe");
        await _provider.DidNotReceive().GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindByIdAsync_FetchesFromProvider_WhenStale()
    {
        FederatedIdentity staleEntry = CreateCacheEntry(lastSyncedAt: DateTimeOffset.UtcNow.AddDays(-2));
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(staleEntry);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(CreateUser()));

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await _provider.Received(1).GetUserAsync("user-1", Arg.Any<CancellationToken>());
        await _store.Received(1).UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsStaleCacheEntry_WhenProviderDown()
    {
        FederatedIdentity staleEntry = CreateCacheEntry(lastSyncedAt: DateTimeOffset.UtcNow.AddDays(-2));
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(staleEntry);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Provider down"));

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Username.ShouldBe("jdoe");
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenNoCacheAndProviderDown()
    {
        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Provider down"));

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_UsesHostContextLookup_WhenNoTenant()
    {
        _tenant.IsAvailable.Returns(false);
        FederatedIdentity entry = CreateCacheEntry();
        _store.FindFirstByExternalIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(entry);

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await _store.Received(1).FindFirstByExternalIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindByIdsAsync_ReturnsEmptyForEmptyInput()
    {
        CachedUserLookupService service = CreateService();
        IReadOnlyList<IIdentityUser> result = await service.FindByIdsAsync(
            [], TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshByIdAsync_ForcesProviderFetch()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(CreateUser()));

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.RefreshByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await _store.Received(1).UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshByIdAsync_ReturnsNull_WhenProviderReturnsNull()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.RefreshByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _store.DidNotReceive().UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAllAsync_PaginatesThroughProvider()
    {
        IReadOnlyList<IIdentityUser> page1 = Enumerable.Range(0, 100).Select(i => (IIdentityUser)CreateUser($"user-{i}")).ToList();
        IReadOnlyList<IIdentityUser> page2 = Enumerable.Range(100, 50).Select(i => (IIdentityUser)CreateUser($"user-{i}")).ToList();

        _provider.GetUsersAsync(null, 0, 100, Arg.Any<CancellationToken>())
            .Returns(page1);
        _provider.GetUsersAsync(null, 100, 100, Arg.Any<CancellationToken>())
            .Returns(page2);

        // Joint hydration drives one UpsertAsync per provider row instead
        // of a single UpsertManyAsync: per ADR-051 B-step 3.5 each new
        // row materialises a canonical User first, so the batch path
        // unfolds into per-row sequencing.
        CachedUserLookupService service = CreateService();
        int synced = await service.RefreshAllAsync(TestContext.Current.CancellationToken);

        synced.ShouldBe(150);
        await _store.Received(150).UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteByIdAsync_DelegatesToStore()
    {
        CachedUserLookupService service = CreateService();
        await service.DeleteByIdAsync("user-1", TestContext.Current.CancellationToken);

        await _store.Received(1).DeleteByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PseudonymizeByIdAsync_DelegatesToStore()
    {
        CachedUserLookupService service = CreateService();
        await service.PseudonymizeByIdAsync("user-1", TestContext.Current.CancellationToken);

        await _store.Received(1).PseudonymizeAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_DelegatesToStore()
    {
        _store.SearchAsync("john", Arg.Any<Guid?>(), 1, 20, Arg.Any<CancellationToken>())
            .Returns((new List<FederatedIdentity> { CreateCacheEntry() } as IReadOnlyList<FederatedIdentity>, 1));

        CachedUserLookupService service = CreateService();
        PagedResult<IIdentityUser> result = await service.SearchAsync(
            "john", 1, 20, TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(1);
    }

    // ──── Joint User + FederatedIdentity hydration (ADR-051 B-step 3.5) ────

    [Fact]
    public async Task FindByIdAsync_OnCacheMiss_CreatesCanonicalUser_BeforeFederatedIdentity()
    {
        // No existing cache row. Provider returns the user. The joint
        // hydration helper must create the canonical User row first
        // (mirroring B-step 2.5) so admin grids see it immediately.
        var generatedId = Guid.NewGuid();
        _guidGenerator.Create().Returns(generatedId);

        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(CreateUser()));

        CachedUserLookupService service = CreateService();
        await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

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
    public async Task FindByIdAsync_OnExistingStaleEntry_DoesNotCreateUser_OnRefresh()
    {
        // Existing entry exists (with its own Id/UserId already aligned).
        // The provider returns a fresh copy. The update path must
        // preserve the existing identifier pair and SKIP the User create.
        var existingId = Guid.NewGuid();
        FederatedIdentity stale = CreateCacheEntry(lastSyncedAt: DateTimeOffset.UtcNow.AddDays(-2));
        stale.Id = existingId;
        stale.UserId = existingId;

        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(stale);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(CreateUser()));

        CachedUserLookupService service = CreateService();
        await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        await _userDirectoryWriter.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _store.Received(1).UpsertAsync(
            Arg.Is<FederatedIdentity>(e => e.Id == existingId && e.UserId == existingId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindByIdAsync_CompensatesUserDelete_WhenFederatedInsertFails()
    {
        // The User row is created first; if the FederatedIdentity insert
        // throws, the canonical row would orphan — the helper must
        // hard-delete it to keep the directory consistent.
        var generatedId = Guid.NewGuid();
        _guidGenerator.Create().Returns(generatedId);

        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(CreateUser()));
        _store.UpsertAsync(Arg.Any<FederatedIdentity>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("store offline"));

        CachedUserLookupService service = CreateService();

        // FindByIdAsync wraps the cache-miss block in a try/catch that
        // swallows non-cancellation exceptions and returns the stale
        // entry — so the throw is absorbed by the caller, not surfaced.
        // We just verify the compensation ran.
        await service.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        await _userDirectoryWriter.Received(1).CreateAsync(
            Arg.Is<User>(u => u.Id == generatedId), Arg.Any<CancellationToken>());
        await _userDirectoryWriter.Received(1).DeleteAsync(generatedId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindByIdAsync_FallsBackToEmail_AsDisplayName_WhenNamesAreMissing()
    {
        var generatedId = Guid.NewGuid();
        _guidGenerator.Create().Returns(generatedId);

        FederatedIdentityUser nameless = new(
            UserId: "user-2", Username: "anon", Email: "anon@test.com",
            FirstName: null, LastName: null, Enabled: true);

        _store.FindByExternalIdAsync("user-2", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);
        _provider.GetUserAsync("user-2", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(nameless));

        CachedUserLookupService service = CreateService();
        await service.FindByIdAsync("user-2", TestContext.Current.CancellationToken);

        await _userDirectoryWriter.Received(1).CreateAsync(
            Arg.Is<User>(u => u.DisplayName == "anon@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshByIdAsync_FetchesExistingFirst_BeforeJointHydration()
    {
        // RefreshByIdAsync didn't previously do an existence check — the
        // joint hydration adds one so updates correctly preserve the
        // existing Id/UserId pair instead of generating fresh ones.
        var existingId = Guid.NewGuid();
        FederatedIdentity existing = CreateCacheEntry();
        existing.Id = existingId;
        existing.UserId = existingId;

        _store.FindByExternalIdAsync("user-1", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(CreateUser()));

        CachedUserLookupService service = CreateService();
        await service.RefreshByIdAsync("user-1", TestContext.Current.CancellationToken);

        await _userDirectoryWriter.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _store.Received(1).UpsertAsync(
            Arg.Is<FederatedIdentity>(e => e.Id == existingId && e.UserId == existingId),
            Arg.Any<CancellationToken>());
    }
}
