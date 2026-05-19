using Granit.Caching.MultiTenancy;
using Granit.MultiTenancy;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Caching.Tests;

public sealed class TenantAwareFusionCacheTests
{
    private readonly IFusionCache _inner = Substitute.For<IFusionCache>();
    private readonly CurrentTenant _tenant = new();

    private TenantAwareFusionCache CreateSut() => new(_inner, _tenant);

    [Fact]
    public async Task SetAsync_PrefixesKeyWithTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.Change(tenantId);
        TenantAwareFusionCache sut = CreateSut();

        await sut.SetAsync("settings", "value", token: TestContext.Current.CancellationToken);

        await _inner.Received(1).SetAsync(
            $"t:{tenantId:N}:settings",
            "value",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_UsesHostPrefix_WhenNoTenant()
    {
        TenantAwareFusionCache sut = CreateSut();

        await sut.SetAsync("global-config", "value", token: TestContext.Current.CancellationToken);

        await _inner.Received(1).SetAsync(
            "t:host:global-config",
            "value",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpireAsync_PrefixesKey()
    {
        var tenantId = Guid.NewGuid();
        _tenant.Change(tenantId);
        TenantAwareFusionCache sut = CreateSut();

        await sut.ExpireAsync("cache-key", token: TestContext.Current.CancellationToken);

        await _inner.Received(1).ExpireAsync(
            $"t:{tenantId:N}:cache-key",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveByTagAsync_PrefixesTag()
    {
        var tenantId = Guid.NewGuid();
        _tenant.Change(tenantId);
        TenantAwareFusionCache sut = CreateSut();

        await sut.RemoveByTagAsync("products", token: TestContext.Current.CancellationToken);

        await _inner.Received(1).RemoveByTagAsync(
            $"t:{tenantId:N}:products",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DifferentTenants_ProduceDifferentKeys()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _tenant.Change(tenantA);
        TenantAwareFusionCache sutA = CreateSut();
        await sutA.SetAsync("data", "A", token: TestContext.Current.CancellationToken);

        _tenant.Change(tenantB);
        TenantAwareFusionCache sutB = CreateSut();
        await sutB.SetAsync("data", "B", token: TestContext.Current.CancellationToken);

        await _inner.Received(1).SetAsync(
            $"t:{tenantA:N}:data",
            "A",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());

        await _inner.Received(1).SetAsync(
            $"t:{tenantB:N}:data",
            "B",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_DoesNotDisposeInnerSingleton()
    {
        TenantAwareFusionCache sut = CreateSut();

        sut.Dispose();

        _inner.DidNotReceive().Dispose();
    }
}
