using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests;

public sealed class GranitReferenceDataEndpointsModuleTests
{
    [Fact]
    public void Inherits_GranitModule()
    {
        GranitReferenceDataEndpointsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Has_DependsOn_ReferenceDataModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitReferenceDataEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();

        dependedTypes.ShouldContain(typeof(GranitReferenceDataModule));
    }
}
