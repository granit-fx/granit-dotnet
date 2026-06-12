using Granit.IpGeolocation.Options;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.Tests;

public sealed class GranitIpGeolocationOptionsTests
{
    [Fact]
    public void SectionName_IsTopLevelModulePath() =>
        GranitIpGeolocationOptions.SectionName.ShouldBe("IpGeolocation");
}
