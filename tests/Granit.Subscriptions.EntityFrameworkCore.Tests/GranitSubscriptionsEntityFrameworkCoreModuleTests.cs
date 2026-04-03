using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.EntityFrameworkCore.Tests;

public sealed class GranitSubscriptionsEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitSubscriptionsEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitSubscriptionsEntityFrameworkCoreModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitSubscriptionsModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitSubscriptionsEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitSubscriptionsModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceEntityFrameworkCoreModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitSubscriptionsEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
    }
}
