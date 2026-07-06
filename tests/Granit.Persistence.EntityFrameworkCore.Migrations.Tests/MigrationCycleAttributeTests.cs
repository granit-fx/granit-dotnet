using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationCycleAttributeTests
{
    [Fact]
    public void Constructor_SetsPhaseAndCycleId()
    {
        MigrationCycleAttribute attr = new(MigrationPhase.Expand, "patient-fullname-v2");

        attr.Phase.ShouldBe(MigrationPhase.Expand);
        attr.CycleId.ShouldBe("patient-fullname-v2");
    }

    [Fact]
    public void Attribute_IsNotAllowedMultipleTimes()
    {
        AttributeUsageAttribute? usage = typeof(MigrationCycleAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void Attribute_TargetsClassOnly()
    {
        AttributeUsageAttribute? usage = typeof(MigrationCycleAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        usage.ShouldNotBeNull();
        usage!.ValidOn.ShouldBe(AttributeTargets.Class);
    }
}
