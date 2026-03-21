using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

public sealed class GranitReferenceDataEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Inherits_GranitModule()
    {
        GranitReferenceDataEntityFrameworkCoreModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Has_DependsOn_ReferenceDataModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitReferenceDataEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();

        dependedTypes.ShouldContain(typeof(GranitReferenceDataModule));
    }
}
