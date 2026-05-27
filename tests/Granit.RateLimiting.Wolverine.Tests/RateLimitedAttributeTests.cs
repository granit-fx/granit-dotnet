using Granit.RateLimiting.Wolverine.Attributes;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Wolverine.Tests;

public sealed class RateLimitedAttributeTests
{
    [RateLimited("api")]
    private sealed class DecoratedMessage;

    private sealed class PlainMessage;

    [Fact]
    public void PolicyName_ReturnsConfiguredValue()
    {
        var attr = new RateLimitedAttribute("api");

        attr.PolicyName.ShouldBe("api");
    }

    [Fact]
    public void Attribute_CanBeReadFromType()
    {
        var attrs = typeof(DecoratedMessage)
            .GetCustomAttributes(typeof(RateLimitedAttribute), true)
            .Cast<RateLimitedAttribute>()
            .ToList();

        attrs.ShouldHaveSingleItem();
        attrs[0].PolicyName.ShouldBe("api");
    }

    [Fact]
    public void Attribute_AbsentOnPlainType()
    {
        object[] attrs = typeof(PlainMessage)
            .GetCustomAttributes(typeof(RateLimitedAttribute), true);

        attrs.ShouldBeEmpty();
    }
}
