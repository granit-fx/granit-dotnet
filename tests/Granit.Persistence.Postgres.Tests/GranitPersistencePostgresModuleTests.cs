using Granit.Modularity;
using Granit.Persistence.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Postgres.Tests;

public sealed class GranitPersistencePostgresModuleTests
{
    [Fact]
    public void DependsOn_DeclaresHostingModuleDependency()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitPersistencePostgresModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        attributes.Length.ShouldBe(1);
        Type[] dependedTypes = attributes[0].DependedTypes;
        dependedTypes.ShouldContain(typeof(GranitPersistenceHostingModule));
    }
}
