using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests;

public sealed class GranitPersistenceEntityFrameworkCoreSqlServerModuleTests
{
    [Fact]
    public void DependsOn_DeclaresHostingModuleDependency()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitPersistenceEntityFrameworkCoreSqlServerModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        attributes.Length.ShouldBe(1);
        Type[] dependedTypes = attributes[0].DependedTypes;
        dependedTypes.ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreHostingModule));
    }
}
