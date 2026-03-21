using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelinedAttributeTests
{
    [Fact]
    public void Constructor_SetsEntityTypeName()
    {
        TimelinedAttribute attribute = new("CustomEntityName");

        attribute.EntityTypeName.ShouldBe("CustomEntityName");
    }

    [Fact]
    public void AttributeUsage_TargetsClassOnly()
    {
        AttributeUsageAttribute? usage = typeof(TimelinedAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .SingleOrDefault();

        usage.ShouldNotBeNull();
        usage!.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.Inherited.ShouldBeFalse();
    }
}
