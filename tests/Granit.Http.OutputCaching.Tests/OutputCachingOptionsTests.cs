using Granit.Http.OutputCaching.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class OutputCachingOptionsTests
{
    [Fact]
    public void SectionName_IsOutputCaching() =>
        OutputCachingOptions.SectionName.ShouldBe("Http:OutputCaching");

    [Fact]
    public void DefaultExpiration_DefaultsTo60Seconds() =>
        new OutputCachingOptions().DefaultExpiration.ShouldBe(TimeSpan.FromSeconds(60));

    [Fact]
    public void VaryByQueryKeys_HasDefaultSet()
    {
        OutputCachingOptions options = new();

        options.VaryByQueryKeys.ShouldContain("page");
        options.VaryByQueryKeys.ShouldContain("pageSize");
        options.VaryByQueryKeys.ShouldContain("sort");
        options.VaryByQueryKeys.ShouldContain("filter");
        options.VaryByQueryKeys.ShouldContain("q");
        options.VaryByQueryKeys.ShouldContain("include");
        options.VaryByQueryKeys.ShouldContain("expand");
    }

    [Fact]
    public void EnableTenantIsolation_DefaultsToTrue() =>
        new OutputCachingOptions().EnableTenantIsolation.ShouldBeTrue();

    [Fact]
    public void ExcludeAuthenticatedResponses_DefaultsToTrue() =>
        new OutputCachingOptions().ExcludeAuthenticatedResponses.ShouldBeTrue();
}
