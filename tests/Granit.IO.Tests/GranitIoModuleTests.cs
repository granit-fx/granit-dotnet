using Granit.IO;
using Granit.Modularity;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

public sealed class GranitIoModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitIoModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitIoModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_DependsOn_MultiTenancy()
    {
        Type[] dependencies = typeof(GranitIoModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();

        dependencies.ShouldContain(typeof(GranitMultiTenancyModule));
    }
}
