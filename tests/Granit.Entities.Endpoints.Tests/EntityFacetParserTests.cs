using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class EntityFacetParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_falls_back_to_All_when_blank(string? raw)
    {
        EntityFacetParser.Parse(raw).ShouldBe(EntityFacets.All);
    }

    [Theory]
    [InlineData("identity", EntityFacets.Identity)]
    [InlineData("Permissions", EntityFacets.Permissions)]
    [InlineData("form", EntityFacets.Forms)]
    [InlineData("forms", EntityFacets.Forms)]
    [InlineData("detail,details", EntityFacets.Details)]
    [InlineData("collection", EntityFacets.Collections)]
    [InlineData("collections", EntityFacets.Collections)]
    [InlineData("list", EntityFacets.Collections)]
    [InlineData("dashboard", EntityFacets.Dashboards)]
    [InlineData("export", EntityFacets.Exports)]
    [InlineData("view,saved-views", EntityFacets.Views)]
    public void Parse_maps_kebab_aliases(string raw, EntityFacets expected)
    {
        EntityFacetParser.Parse(raw).ShouldBe(expected);
    }

    [Fact]
    public void Parse_combines_multiple_facets()
    {
        EntityFacets parsed = EntityFacetParser.Parse(" identity , forms , collections ");
        parsed.ShouldBe(EntityFacets.Identity | EntityFacets.Forms | EntityFacets.Collections);
    }

    [Fact]
    public void Parse_falls_back_to_All_when_every_token_is_unknown()
    {
        EntityFacetParser.Parse("garbage,nonsense").ShouldBe(EntityFacets.All);
    }
}
