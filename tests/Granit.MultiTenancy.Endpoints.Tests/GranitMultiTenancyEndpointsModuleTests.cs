using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests;

public sealed class GranitMultiTenancyEndpointsModuleTests
{
    [Fact]
    public void Module_DependsOn_GranitMultiTenancyModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMultiTenancyEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitMultiTenancyModule));
    }

    [Fact]
    public void Module_DependsOn_GranitAuthorizationModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMultiTenancyEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitAuthorizationModule));
    }

    [Fact]
    public void Module_DependsOn_GranitValidationModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMultiTenancyEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitValidationModule));
    }

    [Fact]
    public void Module_DependsOn_GranitHttpApiDocumentationModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMultiTenancyEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitHttpApiDocumentationModule));
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitMultiTenancyEndpointsModule).IsSealed.ShouldBeTrue();
}
