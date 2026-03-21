using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyAuthenticationDefaultsTests
{
    [Fact]
    public void AuthenticationScheme_IsApiKey() =>
        ApiKeyAuthenticationDefaults.AuthenticationScheme.ShouldBe("ApiKey");

    [Fact]
    public void KeyPrefix_IsGk() =>
        ApiKeyAuthenticationDefaults.KeyPrefix.ShouldBe("gk_");
}
