using Granit.DataLookup.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Endpoints.Tests.Options;

public sealed class DataLookupEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        DataLookupEndpointsOptions.SectionName.ShouldBe("DataLookup:Endpoints");
}
