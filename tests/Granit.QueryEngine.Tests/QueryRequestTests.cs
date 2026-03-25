using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class QueryRequestTests
{
    [Fact]
    public void Default_Page_Is_Null()
    {
        QueryRequest request = new();

        request.Page.ShouldBeNull();
    }

    [Fact]
    public void Default_PageSize_Is_Null()
    {
        QueryRequest request = new();

        request.PageSize.ShouldBeNull();
    }

    [Fact]
    public void Default_Cursor_Is_Null()
    {
        QueryRequest request = new();

        request.Cursor.ShouldBeNull();
    }

    [Fact]
    public void Default_Search_Is_Null()
    {
        QueryRequest request = new();

        request.Search.ShouldBeNull();
    }

    [Fact]
    public void Default_Sort_Is_Null()
    {
        QueryRequest request = new();

        request.Sort.ShouldBeNull();
    }

    [Fact]
    public void Default_Filter_Is_Null()
    {
        QueryRequest request = new();

        request.Filter.ShouldBeNull();
    }

    [Fact]
    public void Default_Presets_Is_Null()
    {
        QueryRequest request = new();

        request.Presets.ShouldBeNull();
    }

    [Fact]
    public void Default_GroupBy_Is_Null()
    {
        QueryRequest request = new();

        request.GroupBy.ShouldBeNull();
    }

    [Fact]
    public void Default_SkipTotalCount_Is_False()
    {
        QueryRequest request = new();

        request.SkipTotalCount.ShouldBeFalse();
    }

    [Fact]
    public void Custom_Values_Are_Preserved()
    {
        Dictionary<string, string> filter = new()
        {
            ["name.contains"] = "Alice",
            ["age.gte"] = "18",
        };
        Dictionary<string, string> presets = new()
        {
            ["status"] = "Active,Pending",
        };

        QueryRequest request = new()
        {
            Page = 2,
            PageSize = 25,
            Search = "test",
            Sort = "-createdAt,lastName",
            Filter = filter,
            Presets = presets,
            GroupBy = "status",
        };

        request.Page.ShouldBe(2);
        request.PageSize.ShouldBe(25);
        request.Cursor.ShouldBeNull();
        request.Search.ShouldBe("test");
        request.Sort.ShouldBe("-createdAt,lastName");
        request.Filter.ShouldBe(filter);
        request.Presets.ShouldBe(presets);
        request.GroupBy.ShouldBe("status");
    }

    [Fact]
    public void Cursor_Pagination_Values_Are_Preserved()
    {
        QueryRequest request = new()
        {
            Cursor = "eyJpZCI6NDJ9",
            PageSize = 10,
        };

        request.Page.ShouldBeNull();
        request.Cursor.ShouldBe("eyJpZCI6NDJ9");
        request.PageSize.ShouldBe(10);
    }

    [Fact]
    public void Record_Equality_Works()
    {
        QueryRequest a = new() { Page = 1, PageSize = 20 };
        QueryRequest b = new() { Page = 1, PageSize = 20 };

        a.ShouldBe(b);
    }

    [Fact]
    public void With_Expression_Creates_Modified_Copy()
    {
        QueryRequest original = new() { Page = 1, PageSize = 20 };

        QueryRequest modified = original with { Page = 2 };

        modified.Page.ShouldBe(2);
        modified.PageSize.ShouldBe(20);
        original.Page.ShouldBe(1);
    }
}
