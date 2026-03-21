using Granit.Querying.AI.Internal;
using Shouldly;
using Xunit;

namespace Granit.Querying.AI.Tests;

public sealed class QueryRequestDtoTests
{
    [Fact]
    public void All_properties_default_to_null()
    {
        QueryRequestDto dto = new();

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
        QueryRequestDto dto = new()
        {
            Page = 2,
            PageSize = 25,
            Sort = "-name",
            Filter = new Dictionary<string, string> { ["name.eq"] = "test" },
            QuickFilters = ["Active"],
            GroupBy = "status",
        };

        dto.Page.ShouldBe(2);
        dto.PageSize.ShouldBe(25);
        dto.Sort.ShouldBe("-name");
        dto.Filter.ShouldNotBeNull();
        dto.Filter.Count.ShouldBe(1);
        dto.QuickFilters.ShouldNotBeNull();
        dto.QuickFilters.Count.ShouldBe(1);
        dto.GroupBy.ShouldBe("status");
    }
}
