using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates MCP conventions:
/// <list type="bullet">
/// <item><c>[McpServerTool]</c> methods must have <c>[Description]</c></item>
/// <item><c>McpPermissions</c> constants follow three-segment format</item>
/// <item>every <c>[McpServerTool]</c> carries explicit authorization, or the
/// framework deny-by-default call-tool gate is registered (VULN-100-AI)</item>
/// </list>
/// </summary>
public sealed class McpConventionTests
{
    /// <summary>
    /// Anti-regression guard for the MCP <c>tools/call</c> fail-open (VULN-100-AI): a principal
    /// with <c>Mcp.Server.Access</c> must not be able to invoke an un-annotated tool. Either every
    /// framework <c>[McpServerTool]</c> carries explicit authorization metadata (an
    /// <see cref="IAuthorizeData"/> such as <c>[Authorize]</c>/<c>[Permission]</c>), or the
    /// deny-by-default <c>CallToolAuthorizationFilter</c> gate must be wired into the server
    /// pipeline so un-annotated tools require <c>Mcp.Tools.Execute</c>.
    /// </summary>
    [Fact]
    public void McpServerTools_are_gated_by_explicit_authorization_or_deny_by_default_filter()
    {
        List<string> unGatedTools = [];

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => a.GetName().Name?.StartsWith("Granit.", StringComparison.Ordinal) == true))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (Type type in types
                         .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null))
            {
                bool typeAuthorized = HasExplicitAuthorization(type);

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                             .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null))
                {
                    if (!typeAuthorized && !HasExplicitAuthorization(method))
                    {
                        unGatedTools.Add($"{type.FullName}.{method.Name}");
                    }
                }
            }
        }

        // Un-annotated tools are only safe because the deny-by-default gate covers them.
        if (unGatedTools.Count > 0)
        {
            DenyByDefaultFilterIsRegistered().ShouldBeTrue(
                "MCP tools without explicit authorization rely on the deny-by-default call-tool gate, "
                + "but 'CallToolAuthorizationFilter.Wrap' is not registered in "
                + "'AddGranitMcpServer'. Un-annotated tools: " + string.Join(", ", unGatedTools));
        }

        // Regardless of whether any tools ship, the fail-open fence itself must stay in place.
        DenyByDefaultFilterIsRegistered().ShouldBeTrue(
            "The MCP deny-by-default call-tool gate ('CallToolAuthorizationFilter.Wrap') must be "
            + "registered in 'AddGranitMcpServer' — see VULN-100-AI.");
    }

    private static bool HasExplicitAuthorization(MemberInfo member) =>
        member.GetCustomAttributes(inherit: false).Any(a => a is IAuthorizeData);

    private static bool DenyByDefaultFilterIsRegistered()
    {
        string repoRoot = FindRepoRoot();
        string extensionsFile = Path.Join(
            repoRoot, "src", "Granit.Mcp.Server", "Extensions", "McpServerServiceCollectionExtensions.cs");

        return File.Exists(extensionsFile)
            && File.ReadAllText(extensionsFile).Contains(
                "CallToolAuthorizationFilter.Wrap", StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(McpConventionTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory).");
    }

    /// <summary>
    /// Every method annotated with <c>[McpServerTool]</c> must also have
    /// <c>[Description]</c> so AI agents understand what the tool does.
    /// </summary>
    [Fact]
    public void McpServerTool_methods_must_have_Description()
    {
        List<string> violations = [];

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => a.GetName().Name?.StartsWith("Granit.", StringComparison.Ordinal) == true))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (Type type in types
                         .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null))
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                             .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null))
                {
                    if (method.GetCustomAttribute<DescriptionAttribute>() is null)
                    {
                        violations.Add($"{type.FullName}.{method.Name} has [McpServerTool] but no [Description]");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            $"MCP tool methods must have [Description]:\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// <c>McpPermissions</c> constants must follow the three-segment
    /// <c>[Group].[Resource].[Action]</c> format.
    /// </summary>
    [Fact]
    public void McpPermissions_must_follow_three_segment_format()
    {
        Type permissionsType = typeof(Granit.Mcp.Server.Permissions.McpPermissions);
        List<string> violations = [];

        foreach (Type nested in permissionsType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (FieldInfo field in nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                         .Where(f => f is { IsLiteral: true, FieldType.Name: "String" }))
            {
                if (field.GetValue(null) is not string value)
                {
                    continue;
                }

                int segments = value.Split('.').Length;
                if (segments != 3)
                {
                    violations.Add($"{nested.Name}.{field.Name} = \"{value}\" (expected 3 segments, got {segments})");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Permission constants must have 3 segments (Group.Resource.Action):\n{string.Join("\n", violations)}");
    }
}
