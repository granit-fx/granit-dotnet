using Granit.ReferenceData.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests;

public sealed class ReferenceDataEndpointsOptionsTests
{
    [Fact]
    public void Default_RoutePrefix_Is_ReferenceData()
    {
        ReferenceDataEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("reference-data");
    }

    [Fact]
    public void Default_TagName_Is_ReferenceData()
    {
        ReferenceDataEndpointsOptions options = new();

        options.TagName.ShouldBe("Reference Data");
    }

    [Fact]
    public void Default_EntitySegment_Is_Null()
    {
        ReferenceDataEndpointsOptions options = new();

        options.EntitySegment.ShouldBeNull();
    }

    [Fact]
    public void Default_IncludeMetaEndpoint_Is_True()
    {
        ReferenceDataEndpointsOptions options = new();

        options.IncludeMetaEndpoint.ShouldBeTrue();
    }
}
