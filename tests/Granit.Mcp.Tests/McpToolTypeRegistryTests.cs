using Shouldly;

namespace Granit.Mcp.Tests;

public sealed class McpToolTypeRegistryTests
{
    private readonly McpToolTypeRegistry _registry = new();

    [Fact]
    public void Resolve_UnknownName_ReturnsNull() =>
        _registry.Resolve("unknown").ShouldBeNull();

    [Fact]
    public void Register_ThenResolve_ReturnsType()
    {
        _registry.Register("MyTool", typeof(string));

        _registry.Resolve("MyTool").ShouldBe(typeof(string));
    }

    [Fact]
    public void Register_Overwrites_ExistingEntry()
    {
        _registry.Register("Tool", typeof(int));
        _registry.Register("Tool", typeof(string));

        _registry.Resolve("Tool").ShouldBe(typeof(string));
    }

    [Fact]
    public void Resolve_IsCaseSensitive()
    {
        _registry.Register("MyTool", typeof(string));

        _registry.Resolve("mytool").ShouldBeNull();
    }

    [Fact]
    public void RegisterFromAssemblies_RegistersMarkedTypes()
    {
        _registry.RegisterFromAssemblies([typeof(McpToolTypeRegistryTests).Assembly]);

        // No [McpServerToolType] classes in this test assembly — registry unchanged
        _registry.Resolve("McpToolTypeRegistryTests").ShouldBeNull();
    }
}
