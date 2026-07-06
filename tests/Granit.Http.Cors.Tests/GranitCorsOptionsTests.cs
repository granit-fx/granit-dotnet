using Granit.Http.Cors.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cors.Tests;

public sealed class GranitCorsOptionsTests
{
    [Fact]
    public void SectionName_IsCors() =>
        GranitCorsOptions.SectionName.ShouldBe("Http:Cors");

    [Fact]
    public void AllowedOrigins_DefaultsToEmpty() =>
        new GranitCorsOptions().AllowedOrigins.ShouldBeEmpty();

    [Fact]
    public void AllowCredentials_DefaultsToFalse() =>
        new GranitCorsOptions().AllowCredentials.ShouldBeFalse();
}
