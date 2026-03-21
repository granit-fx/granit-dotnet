using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Granit.Validation;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests;

public sealed class GranitFeaturesEndpointsModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitFeaturesEndpointsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitFeaturesEndpointsModule)
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
    public void Module_DependsOn_GranitFeaturesModule()
    {
        DependsOnAttribute[] attrs = GetDependsOnAttributes();

        attrs.SelectMany(a => a.DependedTypes)
             .ShouldContain(typeof(GranitFeaturesModule));
    }

    [Fact]
    public void Module_DependsOn_GranitHttpApiDocumentationModule()
    {
        DependsOnAttribute[] attrs = GetDependsOnAttributes();

        attrs.SelectMany(a => a.DependedTypes)
             .ShouldContain(typeof(GranitHttpApiDocumentationModule));
    }

    [Fact]
    public void Module_DependsOn_GranitValidationModule()
    {
        DependsOnAttribute[] attrs = GetDependsOnAttributes();

        attrs.SelectMany(a => a.DependedTypes)
             .ShouldContain(typeof(GranitValidationModule));
    }

    private static DependsOnAttribute[] GetDependsOnAttributes() =>
        typeof(GranitFeaturesEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();
}
