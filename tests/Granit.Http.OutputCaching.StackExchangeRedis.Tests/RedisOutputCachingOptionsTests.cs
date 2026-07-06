using Granit.Http.OutputCaching.StackExchangeRedis.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.StackExchangeRedis.Tests;

public sealed class RedisOutputCachingOptionsTests
{
    [Fact]
    public void SectionName_IsOutputCachingRedis() =>
        RedisOutputCachingOptions.SectionName.ShouldBe("Http:OutputCaching:Redis");

    [Fact]
    public void IsEnabled_DefaultsToTrue() =>
        new RedisOutputCachingOptions().IsEnabled.ShouldBeTrue();

    [Fact]
    public void Configuration_DefaultsToLocalhost() =>
        new RedisOutputCachingOptions().Configuration.ShouldBe("localhost:6379");

    [Fact]
    public void InstanceName_DefaultsToDdOc() =>
        new RedisOutputCachingOptions().InstanceName.ShouldBe("dd:oc:");

    [Fact]
    public void RequireTls_DefaultsToTrue() =>
        new RedisOutputCachingOptions().RequireTls.ShouldBeTrue();
}
