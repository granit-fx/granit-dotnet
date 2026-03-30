using Granit.Mcp.Server.Internal;
using Granit.Mcp.Server.Permissions;
using Shouldly;

namespace Granit.Mcp.Server.Tests;

public sealed class PermissionScopeMappingServiceTests
{
    [Theory]
    [InlineData("mcp:server:access", McpPermissions.Server.Access)]
    [InlineData("mcp:tools:read", McpPermissions.Tools.Read)]
    [InlineData("mcp:tools:execute", McpPermissions.Tools.Execute)]
    [InlineData("mcp:resources:read", McpPermissions.Resources.Read)]
    [InlineData("mcp:prompts:read", McpPermissions.Prompts.Read)]
    public void MapScopeToPermission_WithKnownScope_ShouldReturnPermission(string scope, string expected)
    {
        string? result = PermissionScopeMappingService.MapScopeToPermission(scope);

        result.ShouldBe(expected);
    }

    [Fact]
    public void MapScopeToPermission_WithUnknownScope_ShouldReturnNull()
    {
        string? result = PermissionScopeMappingService.MapScopeToPermission("unknown:scope");

        result.ShouldBeNull();
    }

    [Fact]
    public void MapScopeToPermission_IsCaseInsensitive()
    {
        string? result = PermissionScopeMappingService.MapScopeToPermission("MCP:TOOLS:READ");

        result.ShouldBe(McpPermissions.Tools.Read);
    }

    [Theory]
    [InlineData(McpPermissions.Server.Access, "mcp:server:access")]
    [InlineData(McpPermissions.Tools.Read, "mcp:tools:read")]
    [InlineData(McpPermissions.Tools.Execute, "mcp:tools:execute")]
    public void MapPermissionToScope_WithKnownPermission_ShouldReturnScope(string permission, string expected)
    {
        string? result = PermissionScopeMappingService.MapPermissionToScope(permission);

        result.ShouldBe(expected);
    }

    [Fact]
    public void MapPermissionToScope_WithUnknownPermission_ShouldReturnNull()
    {
        string? result = PermissionScopeMappingService.MapPermissionToScope("Unknown.Permission");

        result.ShouldBeNull();
    }

    [Fact]
    public void GetAllScopes_ShouldReturnFiveScopes()
    {
        IReadOnlyCollection<string> scopes = PermissionScopeMappingService.GetAllScopes();

        scopes.Count.ShouldBe(5);
        scopes.ShouldContain("mcp:server:access");
        scopes.ShouldContain("mcp:tools:read");
        scopes.ShouldContain("mcp:tools:execute");
        scopes.ShouldContain("mcp:resources:read");
        scopes.ShouldContain("mcp:prompts:read");
    }

    [Fact]
    public void MapScopeToPermission_WithWhitespace_ShouldThrow()
    {
        Should.Throw<ArgumentException>(
            () => PermissionScopeMappingService.MapScopeToPermission("   "));
    }
}
