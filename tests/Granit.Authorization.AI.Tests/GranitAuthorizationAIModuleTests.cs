using Granit.Modularity;
using Shouldly;

namespace Granit.Authorization.AI.Tests;

public sealed class GranitAuthorizationAIModuleTests
{
    [Fact]
    public void InheritsFromGranitModule()
    {
        GranitAuthorizationAIModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void DependsOn_GranitAuthorizationModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(Granit.Authorization.GranitAuthorizationModule));
    }

    [Fact]
    public void DependsOn_GranitAIModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitAuthorizationAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(Granit.AI.GranitAIModule));
    }
}
