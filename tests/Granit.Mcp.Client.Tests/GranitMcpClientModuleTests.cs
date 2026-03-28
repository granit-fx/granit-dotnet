using Granit.Mcp;
using Granit.Mcp.Client;
using Granit.Modularity;
using Shouldly;

namespace Granit.Mcp.Client.Tests;

public sealed class GranitMcpClientModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitMcpClientModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitMcpClientModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_DependsOn_GranitMcpModule()
    {
        Type[] dependencies = typeof(GranitMcpClientModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();

        dependencies.ShouldContain(typeof(GranitMcpModule));
    }
}
