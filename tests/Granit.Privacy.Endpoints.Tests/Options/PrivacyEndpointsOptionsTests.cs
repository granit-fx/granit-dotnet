using Granit.Privacy.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Options;

public sealed class PrivacyEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        PrivacyEndpointsOptions.SectionName.ShouldBe("Privacy:Endpoints");

    [Fact]
    public void Defaults_RoutePrefix_IsPrivacy()
    {
        PrivacyEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("privacy");
    }

    [Fact]
    public void Defaults_TagName_IsPrivacy()
    {
        PrivacyEndpointsOptions options = new();

        options.TagName.ShouldBe("Privacy");
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(PrivacyEndpointsOptions).IsSealed.ShouldBeTrue();
}
