using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataResultTests
{
    private sealed class TestEntity : ReferenceDataEntity;

    [Fact]
    public void Items_And_TotalCount_Are_Preserved()
    {
        TestEntity entity = new() { Code = "BE", LabelEn = "Belgium" };
        List<TestEntity> items = [entity];

        PagedResult<TestEntity> result = new(items, 42, HasMore: false);

        result.Items.ShouldBe(items);
        result.TotalCount.ShouldBe(42);
    }

    [Fact]
    public void Empty_Result_Has_Zero_TotalCount()
    {
        PagedResult<TestEntity> result = new([], 0, HasMore: false);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }
}
