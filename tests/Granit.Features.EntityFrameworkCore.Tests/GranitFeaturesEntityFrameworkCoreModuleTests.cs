using Granit.Features.EntityFrameworkCore.Entities;
using Granit.Features.EntityFrameworkCore.Extensions;
using Granit.Modularity;
using Granit.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class GranitFeaturesEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_DependsOn_GranitFeaturesModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitFeaturesEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitFeaturesModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitFeaturesEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceModule));
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitFeaturesEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        typeof(GranitFeaturesEntityFrameworkCoreModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();
    }
}
