using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataQueryTests
{
    [Fact]
    public void Default_ActiveOnly_Is_True()
    {
        ReferenceDataQuery query = new();

        query.ActiveOnly.ShouldBeTrue();
    }

    [Fact]
    public void Default_SearchTerm_Is_Null()
    {
        ReferenceDataQuery query = new();

        query.SearchTerm.ShouldBeNull();
    }

    [Fact]
    public void Default_SortBy_Is_SortOrder()
    {
        ReferenceDataQuery query = new();

        query.SortBy.ShouldBe("SortOrder");
    }

    [Fact]
    public void Default_Descending_Is_False()
    {
        ReferenceDataQuery query = new();

        query.Descending.ShouldBeFalse();
    }

    [Fact]
    public void Default_Page_Is_1()
    {
        ReferenceDataQuery query = new();

        query.Page.ShouldBe(1);
    }

    [Fact]
    public void Default_PageSize_Is_DefaultPageSize()
    {
        ReferenceDataQuery query = new();

        query.PageSize.ShouldBe(QueryEngineDefaults.DefaultPageSize);
    }

    [Fact]
    public void Custom_Values_Are_Preserved()
    {
        ReferenceDataQuery query = new(
            ActiveOnly: false,
            SearchTerm: "belg",
            SortBy: "Code",
            Descending: true,
            Page: 3,
            PageSize: 25);

        query.ActiveOnly.ShouldBeFalse();
        query.SearchTerm.ShouldBe("belg");
        query.SortBy.ShouldBe("Code");
        query.Descending.ShouldBeTrue();
        query.Page.ShouldBe(3);
        query.PageSize.ShouldBe(25);
    }
}
