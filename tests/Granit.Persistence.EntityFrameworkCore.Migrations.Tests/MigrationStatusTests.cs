using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationStatusTests
{
    [Fact]
    public void Enum_ContainsFourValues()
    {
        MigrationStatus[] values = Enum.GetValues<MigrationStatus>();

        values.Length.ShouldBe(4);
    }

    [Fact]
    public void Pending_IsDefaultValue()
    {
        MigrationStatus defaultValue = default;

        defaultValue.ShouldBe(MigrationStatus.Pending);
    }

    [Theory]
    [InlineData(MigrationStatus.Pending, 0)]
    [InlineData(MigrationStatus.InProgress, 1)]
    [InlineData(MigrationStatus.Completed, 2)]
    [InlineData(MigrationStatus.Failed, 3)]
    public void Values_HaveExpectedUnderlyingValues(MigrationStatus status, int expected) => ((int)status).ShouldBe(expected);
}
