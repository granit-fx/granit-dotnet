using Granit.Caching.StackExchangeRedis.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.StackExchangeRedis.Tests;

public sealed class RedisCachingOptionsTests
{
    [Fact]
    public void SectionName_IsCacheRedis() => RedisCachingOptions.SectionName.ShouldBe("Cache:Redis");

    [Fact]
    public void Defaults_IsEnabled_IsTrue()
    {
        RedisCachingOptions options = new();

        options.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Defaults_Configuration_IsLocalhost()
    {
        RedisCachingOptions options = new();

        options.Configuration.ShouldBe("localhost:6379");
    }

    [Fact]
    public void Defaults_InstanceName_IsDd()
    {
        RedisCachingOptions options = new();

        options.InstanceName.ShouldBe("dd:");
    }

    [Fact]
    public void Configuration_CanBeOverridden()
    {
        RedisCachingOptions options = new() { Configuration = "redis-cluster:6380,ssl=true" };

        options.Configuration.ShouldBe("redis-cluster:6380,ssl=true");
    }

    [Fact]
    public void InstanceName_CanBeOverridden()
    {
        RedisCachingOptions options = new() { InstanceName = "myapp:" };

        options.InstanceName.ShouldBe("myapp:");
    }

    [Fact]
    public void IsEnabled_CanBeDisabled()
    {
        RedisCachingOptions options = new() { IsEnabled = false };

        options.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_ConnectionStringName_IsCache()
    {
        RedisCachingOptions options = new();

        options.ConnectionStringName.ShouldBe("cache");
    }

    [Fact]
    public void ConnectionStringName_CanBeSetToNull()
    {
        RedisCachingOptions options = new() { ConnectionStringName = null };

        options.ConnectionStringName.ShouldBeNull();
    }

    [Fact]
    public void ConnectionStringName_CanBeOverridden()
    {
        RedisCachingOptions options = new() { ConnectionStringName = "my-redis" };

        options.ConnectionStringName.ShouldBe("my-redis");
    }
}
