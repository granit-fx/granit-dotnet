using Granit.Http.Cors.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cors.Tests;

public sealed class GranitCorsOptionsTests
{
    [Fact]
    public void SectionName_IsCors() =>
        GranitCorsOptions.SectionName.ShouldBe("Cors");

    [Fact]
    public void AllowedOrigins_DefaultsToEmpty() =>
        new GranitCorsOptions().AllowedOrigins.ShouldBeEmpty();

    [Fact]
    public void AllowCredentials_DefaultsToFalse() =>
        new GranitCorsOptions().AllowCredentials.ShouldBeFalse();

    [Fact]
    public void AllowedOrigins_CanBeSet()
    {
        GranitCorsOptions options = new()
        {
            AllowedOrigins = ["https://app.example.com", "https://admin.example.com"],
        };

        options.AllowedOrigins.Length.ShouldBe(2);
        options.AllowedOrigins.ShouldContain("https://app.example.com");
    }

    [Fact]
    public void AllowCredentials_CanBeSet()
    {
        GranitCorsOptions options = new() { AllowCredentials = true };

        options.AllowCredentials.ShouldBeTrue();
    }
}
