using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates the OpenAPI tag-name conventions from CLAUDE.md across all framework modules in
/// <c>granit-dotnet/src/</c>. Logic lives in <c>Granit.ArchitectureTests.Abstractions</c>
/// so consuming repos (granit-business, Showcase, …) can run the same checks against their
/// own <c>src/</c> without duplicating the implementation.
/// </summary>
public sealed class OpenApiTagNameConventionTests
{
    private static readonly string SrcDir = Path.Join(
        OpenApiTagConventionRules.FindRepoRoot(typeof(OpenApiTagNameConventionTests).Assembly),
        "src");

    [Fact]
    public void No_Endpoints_file_should_declare_a_hardcoded_TagName_constant()
        => OpenApiTagConventionRules.NoEndpointsFileShouldDeclareHardcodedTagNameConstant(SrcDir);

    [Fact]
    public void All_TagName_defaults_should_follow_TitleCase_or_module_subgroup_format()
        => OpenApiTagConventionRules.AllTagNameDefaultsShouldFollowConvention(SrcDir);

    [Fact]
    public void Every_Endpoints_package_should_have_an_EndpointsOptions_class()
        => OpenApiTagConventionRules.EveryEndpointsPackageShouldHaveEndpointsOptions(
            SrcDir,
            // Uses dynamic per-frontend tags ($"BFF - {frontend.Name}") — no static options default.
            "Granit.Bff.Endpoints");

}
