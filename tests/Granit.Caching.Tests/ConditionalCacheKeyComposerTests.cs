using Granit.Caching.Internal;
using Granit.Caching.Options;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class ConditionalCacheKeyComposerTests
{
    private readonly CurrentTenant _tenant = new();

    private ConditionalCacheKeyComposer CreateSut(string keyPrefix = "dd") => new(
        Microsoft.Extensions.Options.Options.Create(new CachingOptions { KeyPrefix = keyPrefix }),
        _tenant);

    [Fact]
    public void Compose_UsesHostSegment_WhenNoTenant()
    {
        ConditionalCacheKeyComposer sut = CreateSut();

        sut.Compose("idem:abc").ShouldBe("dd:cond:t:host:idem:abc");
    }

    [Fact]
    public void Compose_UsesTenantSegment_WhenTenantActive()
    {
        var tenantId = Guid.NewGuid();
        _tenant.Change(tenantId);
        ConditionalCacheKeyComposer sut = CreateSut();

        sut.Compose("idem:abc").ShouldBe($"dd:cond:t:{tenantId:N}:idem:abc");
    }

    [Fact]
    public void Compose_HonorsCustomKeyPrefix()
    {
        ConditionalCacheKeyComposer sut = CreateSut(keyPrefix: "myapp");

        sut.Compose("lock:1").ShouldBe("myapp:cond:t:host:lock:1");
    }

    [Fact]
    public void Compose_ReadsTenantAtCallTime_NotAtConstruction()
    {
        ConditionalCacheKeyComposer sut = CreateSut();
        var tenantId = Guid.NewGuid();

        string before = sut.Compose("k");
        _tenant.Change(tenantId);
        string after = sut.Compose("k");

        before.ShouldBe("dd:cond:t:host:k");
        after.ShouldBe($"dd:cond:t:{tenantId:N}:k");
    }

    [Fact]
    public async Task InMemoryConditionalCache_IsolatesTenants_OnSameLogicalKey()
    {
        var cache = new InMemoryConditionalCache(TimeProvider.System, CreateSut());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _tenant.Change(tenantA);
        bool addedA = await cache.SetIfAbsentAsync("shared-key", "value-a", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        _tenant.Change(tenantB);
        bool addedB = await cache.SetIfAbsentAsync("shared-key", "value-b", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        string? seenByB = await cache.GetAsync<string>("shared-key", TestContext.Current.CancellationToken);

        _tenant.Change(tenantA);
        string? seenByA = await cache.GetAsync<string>("shared-key", TestContext.Current.CancellationToken);

        addedA.ShouldBeTrue();
        addedB.ShouldBeTrue("tenant B must not observe tenant A's entry under the same logical key");
        seenByA.ShouldBe("value-a");
        seenByB.ShouldBe("value-b");
    }
}
