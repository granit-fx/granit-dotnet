using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationPhaseTests
{
    [Fact]
    public void Enum_ContainsThreeValues()
    {
        MigrationPhase[] values = Enum.GetValues<MigrationPhase>();

        values.Length.ShouldBe(3);
    }

    [Fact]
    public void Expand_IsDefaultValue()
    {
        const MigrationPhase defaultValue = default;

        defaultValue.ShouldBe(MigrationPhase.Expand);
    }

    [Theory]
    [InlineData(MigrationPhase.Expand, 0)]
    [InlineData(MigrationPhase.Migrate, 1)]
    [InlineData(MigrationPhase.Contract, 2)]
    public void Values_HaveExpectedUnderlyingValues(MigrationPhase phase, int expected) => ((int)phase).ShouldBe(expected);
}
