using Granit.Authorization.Cache;
using Granit.Authorization.Events;
using Granit.Authorization.Services;
using Granit.Caching;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionCacheInvalidationHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_RemovesCacheEntryWithCorrectKey()
    {
        // Arrange
        ICacheService<PermissionGrantCacheItem> cache =
            Substitute.For<ICacheService<PermissionGrantCacheItem>>();

        var @event = new PermissionGrantChangedEvent("Invoices.Delete", "accountant", TenantId, IsGranted: true);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        // Assert
        string expectedKey = PermissionChecker.BuildCacheKey(TenantId, "accountant", "Invoices.Delete");
        await cache.Received(1).RemoveAsync(expectedKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GlobalScope_UsesGlobalCacheKey()
    {
        // Arrange
        ICacheService<PermissionGrantCacheItem> cache =
            Substitute.For<ICacheService<PermissionGrantCacheItem>>();

        var @event = new PermissionGrantChangedEvent("Invoices.Delete", "accountant", TenantId: null, IsGranted: false);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        // Assert
        string expectedKey = PermissionChecker.BuildCacheKey(null, "accountant", "Invoices.Delete");
        expectedKey.ShouldStartWith("perm:global:");
        await cache.Received(1).RemoveAsync(expectedKey, Arg.Any<CancellationToken>());
    }
}
