using Granit.Authorization.Cache;
using Granit.Authorization.Events;
using Granit.Authorization.Services;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Tests;

public sealed class PermissionCacheInvalidationHandlerTests
{
    private const string R = PermissionGrantProviderNames.Role;
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ExpiresCacheEntryWithCorrectKey()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();

        var @event = new PermissionGrantChangedEvent("Invoices.Delete", R, "accountant", TenantId, IsGranted: true);

        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        string expectedKey = PermissionChecker.BuildCacheKey(TenantId, R, "accountant", "Invoices.Delete");
        await cache.Received(1).ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GlobalScope_UsesGlobalCacheKey()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();

        var @event = new PermissionGrantChangedEvent("Invoices.Delete", R, "accountant", TenantId: null, IsGranted: false);

        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        string expectedKey = PermissionChecker.BuildCacheKey(null, R, "accountant", "Invoices.Delete");
        expectedKey.ShouldStartWith("perm:global:");
        await cache.Received(1).RemoveAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Grant vs Revoke invalidation strategy
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GrantCreated_CallsExpireAsync()
    {
        // Grants use soft-expire (stale "denied" is safe).
        IFusionCache cache = Substitute.For<IFusionCache>();
        var @event = new PermissionGrantChangedEvent("Orders.Create", R, "editor", TenantId, IsGranted: true);

        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        string expectedKey = PermissionChecker.BuildCacheKey(TenantId, R, "editor", "Orders.Create");
        await cache.Received(1).ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().RemoveAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GrantRevoked_CallsRemoveAsync()
    {
        // Revocations use hard-remove to prevent stale-while-revalidate.
        IFusionCache cache = Substitute.For<IFusionCache>();
        var @event = new PermissionGrantChangedEvent("Orders.Create", R, "editor", TenantId, IsGranted: false);

        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        string expectedKey = PermissionChecker.BuildCacheKey(TenantId, R, "editor", "Orders.Create");
        await cache.Received(1).RemoveAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Cache key correctness
    // =========================================================================

    [Fact]
    public async Task HandleAsync_DifferentPermissions_UseDifferentCacheKeys()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        var event1 = new PermissionGrantChangedEvent("Invoices.Read", R, "editor", TenantId, IsGranted: true);
        var event2 = new PermissionGrantChangedEvent("Invoices.Delete", R, "editor", TenantId, IsGranted: true);

        await PermissionCacheInvalidationHandler.HandleAsync(event1, cache, TestContext.Current.CancellationToken);
        await PermissionCacheInvalidationHandler.HandleAsync(event2, cache, TestContext.Current.CancellationToken);

        string key1 = PermissionChecker.BuildCacheKey(TenantId, R, "editor", "Invoices.Read");
        string key2 = PermissionChecker.BuildCacheKey(TenantId, R, "editor", "Invoices.Delete");
        key1.ShouldNotBe(key2);

        await cache.Received(1).ExpireAsync(key1, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.Received(1).ExpireAsync(key2, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DifferentGrantees_UseDifferentCacheKeys()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        var event1 = new PermissionGrantChangedEvent("Invoices.Delete", R, "accountant", TenantId, IsGranted: false);
        var event2 = new PermissionGrantChangedEvent("Invoices.Delete", R, "editor", TenantId, IsGranted: false);

        await PermissionCacheInvalidationHandler.HandleAsync(event1, cache, TestContext.Current.CancellationToken);
        await PermissionCacheInvalidationHandler.HandleAsync(event2, cache, TestContext.Current.CancellationToken);

        string key1 = PermissionChecker.BuildCacheKey(TenantId, R, "accountant", "Invoices.Delete");
        string key2 = PermissionChecker.BuildCacheKey(TenantId, R, "editor", "Invoices.Delete");
        key1.ShouldNotBe(key2);

        await cache.Received(1).RemoveAsync(key1, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync(key2, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DifferentProviders_UseDifferentCacheKeys()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        var roleEvent = new PermissionGrantChangedEvent(
            "Invoices.Delete", PermissionGrantProviderNames.Role, "alice", TenantId, IsGranted: true);
        var userEvent = new PermissionGrantChangedEvent(
            "Invoices.Delete", PermissionGrantProviderNames.User, "alice", TenantId, IsGranted: true);

        await PermissionCacheInvalidationHandler.HandleAsync(roleEvent, cache, TestContext.Current.CancellationToken);
        await PermissionCacheInvalidationHandler.HandleAsync(userEvent, cache, TestContext.Current.CancellationToken);

        string roleKey = PermissionChecker.BuildCacheKey(TenantId, PermissionGrantProviderNames.Role, "alice", "Invoices.Delete");
        string userKey = PermissionChecker.BuildCacheKey(TenantId, PermissionGrantProviderNames.User, "alice", "Invoices.Delete");
        roleKey.ShouldNotBe(userKey);

        await cache.Received(1).ExpireAsync(roleKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.Received(1).ExpireAsync(userKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }
}
