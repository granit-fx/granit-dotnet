using Granit.Bulkhead.Wolverine.Attributes;
using Shouldly;
using Xunit;

namespace Granit.Bulkhead.Wolverine.Tests;

public sealed class BulkheadAttributeTests
{
    [Fact]
    public void AttributeUsage_AllowsClassAndStruct()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(BulkheadAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage!.ValidOn & AttributeTargets.Class).ShouldBe(AttributeTargets.Class);
        (usage.ValidOn & AttributeTargets.Struct).ShouldBe(AttributeTargets.Struct);
        usage.AllowMultiple.ShouldBeFalse();
        usage.Inherited.ShouldBeTrue();
    }

    [Fact]
    public void CanBeAppliedToType_AndRetrieved()
    {
        object[] attributes = typeof(TestMessage).GetCustomAttributes(typeof(BulkheadAttribute), inherit: true);

        attributes.ShouldHaveSingleItem();
        var attr = (BulkheadAttribute)attributes[0];
        attr.PolicyName.ShouldBe("test-policy");
    }

    [Bulkhead("test-policy")]
    private sealed class TestMessage;
}
