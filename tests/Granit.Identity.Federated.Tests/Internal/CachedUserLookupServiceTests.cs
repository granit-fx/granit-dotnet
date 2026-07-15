using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.MultiTenancy;
using Granit.QueryEngine;
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
    private readonly IFederatedIdentityWriter _writer = Substitute.For<IFederatedIdentityWriter>();
    private readonly IOptions<UserCacheOptions> _options = Microsoft.Extensions.Options.Options.Create(new UserCacheOptions
    {
        StalenessThreshold = TimeSpan.FromHours(24)
    });

    private CachedUserLookupService CreateService() => new(
        _store, _provider, _tenant, _timeProvider, _writer, _options,
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
        _writer.WriteAsync(Arg.Any<IIdentityUser>(), Arg.Any<FederatedIdentity?>(), Arg.Any<CancellationToken>())
            .Returns(ci => new FederatedIdentity { ExternalUserId = ci.Arg<IIdentityUser>().UserId });
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
        await _writer.DidNotReceiveWithAnyArgs().WriteAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FindByIdAsync_FetchesFromProvider_AndWrites_WhenStale()
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
        // Delegates the write (joint hydration) to the shared writer, passing the existing row.
        await _writer.Received(1).WriteAsync(
            Arg.Is<IIdentityUser>(u => u.UserId == "user-1"), staleEntry, Arg.Any<CancellationToken>());
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
    public async Task RefreshByIdAsync_ForcesProviderFetch_AndWrites()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(CreateUser()));

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.RefreshByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await _writer.Received(1).WriteAsync(
            Arg.Is<IIdentityUser>(u => u.UserId == "user-1"), Arg.Any<FederatedIdentity?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshByIdAsync_ReturnsNull_WhenProviderReturnsNull()
    {
        _provider.GetUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IIdentityUser?>(null));

        CachedUserLookupService service = CreateService();
        IIdentityUser? result = await service.RefreshByIdAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _writer.DidNotReceiveWithAnyArgs().WriteAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RefreshAllAsync_PaginatesThroughProvider_WritingEachRow()
    {
        IReadOnlyList<IIdentityUser> page1 = Enumerable.Range(0, 100).Select(i => (IIdentityUser)CreateUser($"user-{i}")).ToList();
        IReadOnlyList<IIdentityUser> page2 = Enumerable.Range(100, 50).Select(i => (IIdentityUser)CreateUser($"user-{i}")).ToList();

        _provider.GetUsersAsync(null, 0, 100, Arg.Any<CancellationToken>())
            .Returns(page1);
        _provider.GetUsersAsync(null, 100, 100, Arg.Any<CancellationToken>())
            .Returns(page2);

        // Joint hydration drives one writer call per provider row (per ADR-051 each new row
        // materialises a canonical User first), so the batch path unfolds into per-row sequencing.
        CachedUserLookupService service = CreateService();
        int synced = await service.RefreshAllAsync(TestContext.Current.CancellationToken);

        synced.ShouldBe(150);
        await _writer.Received(150).WriteAsync(
            Arg.Any<IIdentityUser>(), Arg.Any<FederatedIdentity?>(), Arg.Any<CancellationToken>());
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
}
