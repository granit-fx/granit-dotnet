using Granit.DataLookup.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Endpoints.Tests;

public sealed class DataLookupEndpointsOptionsTests
{
    [Fact]
    public void RoutePrefix_default_is_lookups() =>
        new DataLookupEndpointsOptions().RoutePrefix.ShouldBe("lookups");

    [Fact]
    public void TagName_default_is_data_lookup() =>
        new DataLookupEndpointsOptions().TagName.ShouldBe("Data Lookup");
}
