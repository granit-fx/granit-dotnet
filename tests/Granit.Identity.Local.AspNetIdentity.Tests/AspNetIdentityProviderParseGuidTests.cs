using Granit.Identity.Local.AspNetIdentity.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

/// <summary>
/// Unit tests for <see cref="AspNetIdentityProvider.ParseGuid"/>.
/// </summary>
public sealed class AspNetIdentityProviderParseGuidTests
{
    [Fact]
    public void ParseGuid_valid_guid_returns_parsed_value()
    {
        var expected = Guid.NewGuid();
        Guid result = AspNetIdentityProvider.ParseGuid(expected.ToString(), "userId");
        result.ShouldBe(expected);
    }

    [Fact]
    public void ParseGuid_valid_guid_without_dashes()
    {
        var expected = Guid.NewGuid();
        Guid result = AspNetIdentityProvider.ParseGuid(expected.ToString("N"), "userId");
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")]
    public void ParseGuid_invalid_value_throws_ArgumentException(string invalidValue)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(
            () => AspNetIdentityProvider.ParseGuid(invalidValue, "userId"));

        ex.ParamName.ShouldBe("userId");
        ex.Message.ShouldContain(invalidValue);
    }
}
