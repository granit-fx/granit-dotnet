// =============================================================================
// Tests - GranitApiVersioningOptions
// =============================================================================
// Vérifie les valeurs par défaut et la constante SectionName.
// =============================================================================

using Granit.Http.ApiVersioning.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiVersioning.Tests;

public sealed class GranitApiVersioningOptionsTests
{
    [Fact]
    public void SectionName_IsApiVersioning() =>
        GranitApiVersioningOptions.SectionName.ShouldBe("Http:ApiVersioning");

    [Fact]
    public void DefaultMajorVersion_DefaultsToOne()
    {
        GranitApiVersioningOptions options = new();

        options.DefaultMajorVersion.ShouldBe(1);
    }

    [Fact]
    public void ReportApiVersions_DefaultsToTrue()
    {
        GranitApiVersioningOptions options = new();

        options.ReportApiVersions.ShouldBeTrue();
    }
}
