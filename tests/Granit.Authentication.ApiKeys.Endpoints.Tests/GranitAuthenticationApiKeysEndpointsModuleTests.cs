using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests;

public sealed class GranitAuthenticationApiKeysEndpointsModuleTests
{
    [Fact]
    public void Module_Is_GranitModule() =>
        typeof(GranitAuthenticationApiKeysEndpointsModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_Depends_On_ApiKeys_Module()
    {
        DependsOnAttribute[] deps = typeof(GranitAuthenticationApiKeysEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        deps.ShouldContain(d => d.DependedTypes.Contains(typeof(GranitAuthenticationApiKeysModule)));
    }
}
