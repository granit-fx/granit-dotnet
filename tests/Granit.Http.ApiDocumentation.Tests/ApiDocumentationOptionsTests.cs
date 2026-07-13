// =============================================================================
// Tests - ApiDocumentationOptions
// =============================================================================
// Vérifie les valeurs par défaut et la constante SectionName.
// =============================================================================

using Granit.Http.ApiDocumentation.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ApiDocumentationOptionsTests
{
    [Fact]
    public void SectionName_IsHttpApiDocumentation() =>
        ApiDocumentationOptions.SectionName.ShouldBe("Http:ApiDocumentation");

    [Fact]
    public void MajorVersions_DefaultsToListWithOne()
    {
        ApiDocumentationOptions options = new();

        options.MajorVersions.ShouldHaveSingleItem().ShouldBe(1);
    }

    [Fact]
    public void Title_DefaultsToApi()
    {
        ApiDocumentationOptions options = new();

        options.Title.ShouldBe("API");
    }

    [Fact]
    public void Description_DefaultsToNull()
    {
        ApiDocumentationOptions options = new();

        options.Description.ShouldBeNull();
    }

    [Fact]
    public void ContactEmail_DefaultsToNull()
    {
        ApiDocumentationOptions options = new();

        options.ContactEmail.ShouldBeNull();
    }

    [Fact]
    public void EnableInProduction_DefaultsToFalse()
    {
        ApiDocumentationOptions options = new();

        options.EnableInProduction.ShouldBeFalse();
    }
}
