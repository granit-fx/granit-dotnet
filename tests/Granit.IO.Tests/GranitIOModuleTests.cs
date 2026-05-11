using Granit.IO;
using Granit.Modularity;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

public sealed class GranitIOModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitIOModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitIOModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_DependsOn_MultiTenancy()
    {
        Type[] dependencies = typeof(GranitIOModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();

        dependencies.ShouldContain(typeof(GranitMultiTenancyModule));
    }
}
