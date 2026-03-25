using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class PagedResultTests
{
    [Fact]
    public void Items_And_TotalCount_Are_Preserved()
    {
        List<string> items = ["Alice", "Bob"];

        PagedResult<string> result = new(items, 42, HasMore: false);

        result.Items.ShouldBe(items);
        result.TotalCount.ShouldBe(42);
    }

    [Fact]
    public void Default_NextCursor_Is_Null()
    {
        PagedResult<int> result = new([1, 2, 3], 10, HasMore: false);

        result.NextCursor.ShouldBeNull();
    }

    [Fact]
    public void NextCursor_Is_Preserved_When_Provided()
    {
        PagedResult<int> result = new([1, 2, 3], 100, HasMore: true, NextCursor: "eyJpZCI6M30=");

        result.NextCursor.ShouldBe("eyJpZCI6M30=");
    }

    [Fact]
    public void Empty_Result_Has_Zero_TotalCount()
    {
        PagedResult<string> result = new([], 0, HasMore: false);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public void HasMore_Is_Preserved()
    {
        PagedResult<string> result = new(["Alice"], 10, HasMore: true);

        result.HasMore.ShouldBeTrue();
    }

    [Fact]
    public void HasMore_False_Is_Preserved()
    {
        PagedResult<string> result = new(["Alice"], 1, HasMore: false);

        result.HasMore.ShouldBeFalse();
    }

    [Fact]
    public void Record_Equality_Works()
    {
        List<string> items = ["Alice"];

        PagedResult<string> a = new(items, 1, HasMore: false);
        PagedResult<string> b = new(items, 1, HasMore: false);

        a.ShouldBe(b);
    }
}
