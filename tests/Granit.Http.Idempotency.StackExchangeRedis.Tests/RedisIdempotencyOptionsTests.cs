using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests;

public sealed class RedisIdempotencyOptionsTests
{
    [Fact]
    public void SectionName_IsStable() =>
        RedisIdempotencyOptions.SectionName.ShouldBe("Http:Idempotency:Redis");

    [Fact]
    public void Defaults_AreProductionSafe()
    {
        RedisIdempotencyOptions options = new();

        options.IsEnabled.ShouldBeTrue();
        options.RequireTls.ShouldBeTrue("TLS must be enforced by default");
        options.Configuration.ShouldBe("localhost:6379");
        options.InstanceName.ShouldBe("dd:");
    }
}
