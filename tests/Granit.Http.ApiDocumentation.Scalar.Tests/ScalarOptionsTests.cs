// =============================================================================
// Tests - ScalarOptions
// =============================================================================
// Vérifie les valeurs par défaut et la constante SectionName.
// =============================================================================

using Granit.Http.ApiDocumentation.Scalar.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Scalar.Tests;

public sealed class ScalarOptionsTests
{
    [Fact]
    public void SectionName_IsHttpApiDocumentationScalar() =>
        ScalarOptions.SectionName.ShouldBe("Http:ApiDocumentation:Scalar");

    [Fact]
    public void FaviconUrl_DefaultsToNull()
    {
        ScalarOptions options = new();

        options.FaviconUrl.ShouldBeNull();
    }

    [Fact]
    public void EnableInProduction_DefaultsToFalse()
    {
        ScalarOptions options = new();

        options.EnableInProduction.ShouldBeFalse();
    }

    [Fact]
    public void AuthorizationPolicy_DefaultsToNull()
    {
        ScalarOptions options = new();

        options.AuthorizationPolicy.ShouldBeNull();
    }

    [Fact]
    public void OAuth2_Defaults_EnablePkceTrue_ClientIdAndRedirectUriNull()
    {
        ScalarOptions options = new();

        options.OAuth2.EnablePkce.ShouldBeTrue();
        options.OAuth2.ClientId.ShouldBeNull();
        // null means "let Scalar apply its (currently broken) default";
        // consumers must set this explicitly until upstream #8165/#8187 ship.
        options.OAuth2.RedirectUri.ShouldBeNull();
    }
}
