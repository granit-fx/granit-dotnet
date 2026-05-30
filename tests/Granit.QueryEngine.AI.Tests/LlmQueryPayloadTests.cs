using Granit.QueryEngine.AI.Internal;
using Shouldly;

namespace Granit.QueryEngine.AI.Tests;

public sealed class LlmQueryPayloadTests
{
    [Fact]
    public void All_properties_default_to_null()
    {
        LlmQueryPayload dto = new();

        dto.Page.ShouldBeNull();
        dto.PageSize.ShouldBeNull();
        dto.Sort.ShouldBeNull();
        dto.Filter.ShouldBeNull();
        dto.QuickFilters.ShouldBeNull();
        dto.GroupBy.ShouldBeNull();
    }

    [Fact]
    public void Properties_can_be_set()
    {
        LlmQueryPayload dto = new()
        {
            Page = 2,
            PageSize = 25,
            Sort = "-name",
            Filter = [new LlmFilterClause { Key = "name.eq", Value = "test" }],
            QuickFilters = ["Active"],
            GroupBy = "status",
        };

        dto.Page.ShouldBe(2);
        dto.PageSize.ShouldBe(25);
        dto.Sort.ShouldBe("-name");
        dto.Filter.ShouldNotBeNull();
        dto.Filter.Count.ShouldBe(1);
        dto.Filter[0].Key.ShouldBe("name.eq");
        dto.Filter[0].Value.ShouldBe("test");
        dto.QuickFilters.ShouldNotBeNull();
        dto.QuickFilters.Count.ShouldBe(1);
        dto.GroupBy.ShouldBe("status");
    }

    [Fact]
    public void FilterClause_defaults_to_empty_strings()
    {
        LlmFilterClause clause = new();

        clause.Key.ShouldBe(string.Empty);
        clause.Value.ShouldBe(string.Empty);
    }
}
