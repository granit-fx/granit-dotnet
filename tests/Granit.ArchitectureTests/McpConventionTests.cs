using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates MCP conventions:
/// <list type="bullet">
/// <item><c>[McpServerTool]</c> methods must have <c>[Description]</c></item>
/// <item><c>McpPermissions</c> constants follow three-segment format</item>
/// </list>
/// </summary>
public sealed class McpConventionTests
{
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
