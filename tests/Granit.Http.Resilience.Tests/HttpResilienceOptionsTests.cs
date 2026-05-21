using Granit.Http.Resilience.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Resilience.Tests;

public sealed class HttpResilienceOptionsTests
{
    [Fact]
    public void SectionName_IsHttpResilience() =>
        HttpResilienceOptions.SectionName.ShouldBe("Http:Resilience");
}
