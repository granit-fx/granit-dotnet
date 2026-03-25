using Granit.QueryEngine;
using Granit.ReferenceData.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests;

public sealed class ReferenceDataQueryParametersTests
{
    [Fact]
    public void Default_ActiveOnly_Is_True()
    {
        ReferenceDataQueryParameters parameters = new();

        parameters.ActiveOnly.ShouldBeTrue();
    }

    [Fact]
    public void Default_Search_Is_Null()
    {
        ReferenceDataQueryParameters parameters = new();

        parameters.Search.ShouldBeNull();
    }

    [Fact]
    public void Default_SortBy_Is_Null()
    {
        ReferenceDataQueryParameters parameters = new();

        parameters.SortBy.ShouldBeNull();
    }

    [Fact]
    public void Default_Descending_Is_False()
    {
        ReferenceDataQueryParameters parameters = new();

        parameters.Descending.ShouldBeFalse();
    }

    [Fact]
    public void Default_Page_Is_1()
    {
        ReferenceDataQueryParameters parameters = new();

        parameters.Page.ShouldBe(1);
    }

    [Fact]
    public void Default_PageSize_Is_DefaultPageSize()
    {
        ReferenceDataQueryParameters parameters = new();

        parameters.PageSize.ShouldBe(QueryEngineDefaults.DefaultPageSize);
    }

    [Fact]
    public void Custom_Values_Are_Preserved()
    {
        ReferenceDataQueryParameters parameters = new(
            ActiveOnly: false,
            Search: "test",
            SortBy: "Code",
            Descending: true,
            Page: 5,
            PageSize: 50);

        parameters.ActiveOnly.ShouldBeFalse();
        parameters.Search.ShouldBe("test");
        parameters.SortBy.ShouldBe("Code");
        parameters.Descending.ShouldBeTrue();
        parameters.Page.ShouldBe(5);
        parameters.PageSize.ShouldBe(50);
    }
}
