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
    public void RoutePrefix_CanBeOverridden()
    {
        ApiKeysEndpointsOptions options = new() { RoutePrefix = "keys" };

        options.RoutePrefix.ShouldBe("keys");
    }

    [Fact]
    public void TagName_CanBeOverridden()
    {
        ApiKeysEndpointsOptions options = new() { TagName = "Key Management" };

        options.TagName.ShouldBe("Key Management");
    }

    [Fact]
    public void AllowedEnvironments_CanBeOverridden()
    {
        ApiKeysEndpointsOptions options = new()
        {
            AllowedEnvironments = ["production", "staging"],
        };

        options.AllowedEnvironments.ShouldBe(["production", "staging"]);
    }

    [Fact]
    public void AllowedEnvironments_DefaultHasThreeEntries()
    {
        ApiKeysEndpointsOptions options = new();

        options.AllowedEnvironments.Count.ShouldBe(3);
    }
}
