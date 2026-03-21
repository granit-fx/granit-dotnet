using Granit.Http.OutputCaching.Policies;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class GranitOutputCachePolicyNamesTests
{
    [Fact]
    public void Default_IsGranitDefault() =>
        GranitOutputCachePolicyNames.Default.ShouldBe("GranitDefault");

    [Fact]
    public void NoCache_IsGranitNoCache() =>
        GranitOutputCachePolicyNames.NoCache.ShouldBe("GranitNoCache");
}
