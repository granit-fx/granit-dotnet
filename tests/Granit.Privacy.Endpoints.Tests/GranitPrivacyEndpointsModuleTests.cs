using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests;

public sealed class GranitPrivacyEndpointsModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitPrivacyEndpointsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitPrivacyEndpointsModule)
            .IsSubclassOf(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitAuthorizationModule()
    {
        DependsOnAttribute[] attrs = GetDependsOnAttributes();

        attrs.SelectMany(a => a.DependedTypes)
             .ShouldContain(typeof(GranitAuthorizationModule));
    }

    [Fact]
    public void Module_DependsOn_GranitHttpApiDocumentationModule()
    {
        DependsOnAttribute[] attrs = GetDependsOnAttributes();

        attrs.SelectMany(a => a.DependedTypes)
             .ShouldContain(typeof(GranitHttpApiDocumentationModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPrivacyModule()
    {
        DependsOnAttribute[] attrs = GetDependsOnAttributes();

        attrs.SelectMany(a => a.DependedTypes)
             .ShouldContain(typeof(GranitPrivacyModule));
    }

    [Fact]
    public void Module_DependsOn_GranitValidationModule()
    {
        DependsOnAttribute[] attrs = GetDependsOnAttributes();

        attrs.SelectMany(a => a.DependedTypes)
             .ShouldContain(typeof(GranitValidationModule));
    }

    private static DependsOnAttribute[] GetDependsOnAttributes() =>
        typeof(GranitPrivacyEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();
}
