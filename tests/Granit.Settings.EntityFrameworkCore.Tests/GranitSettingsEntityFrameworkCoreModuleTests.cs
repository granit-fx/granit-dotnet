using Granit.Core.Modularity;
using Granit.Persistence;
using Shouldly;
using Xunit;

namespace Granit.Settings.EntityFrameworkCore.Tests;

public sealed class GranitSettingsEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_DependsOn_GranitSettingsModule()
    {
        var attributes = (DependsOnAttribute[])typeof(GranitSettingsEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();

        dependedTypes.ShouldContain(typeof(GranitSettingsModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceModule()
    {
        var attributes = (DependsOnAttribute[])typeof(GranitSettingsEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();

        dependedTypes.ShouldContain(typeof(GranitPersistenceModule));
    }

    [Fact]
    public void Module_IsSealed() => typeof(GranitSettingsEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        typeof(GranitSettingsEntityFrameworkCoreModule)
            .IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }
}
