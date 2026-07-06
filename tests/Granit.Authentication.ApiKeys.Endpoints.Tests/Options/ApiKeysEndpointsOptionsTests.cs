using Granit.Authentication.ApiKeys.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests.Options;

public sealed class ApiKeysEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsApiKeysEndpoints() =>
        ApiKeysEndpointsOptions.SectionName.ShouldBe("Authentication:ApiKeys:Endpoints");

    [Fact]
    public void Defaults_AreCorrect()
    {
        ApiKeysEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("authentication");
        options.TagName.ShouldBe("API Keys");
        options.AllowedEnvironments.ShouldBe(["live", "test", "dev"]);
    }

    [Fact]
    public void AllowedEnvironments_DefaultHasThreeEntries()
    {
        ApiKeysEndpointsOptions options = new();

        options.AllowedEnvironments.Count.ShouldBe(3);
    }
}
