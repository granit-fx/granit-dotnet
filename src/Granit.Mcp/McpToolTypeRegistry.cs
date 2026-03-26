using System.Collections.Concurrent;
using ModelContextProtocol.Server;

namespace Granit.Mcp;

/// <summary>
/// Maintains a mapping from MCP tool names to their declaring CLR types.
/// Populated during assembly scanning and consumed by visibility filters
/// to resolve <c>toolType</c> (which the SDK's <see cref="ModelContextProtocol.Protocol.Tool"/>
/// protocol object does not carry).
/// </summary>
public sealed class McpToolTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _toolTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a tool name → CLR type mapping. Called during assembly discovery.
    /// </summary>
    public void Register(string toolName, Type toolType) =>
        _toolTypes[toolName] = toolType;

    /// <summary>
    /// Resolves the CLR type for a given tool name, or <c>null</c> if not registered.
    /// </summary>
    public Type? Resolve(string toolName) =>
        _toolTypes.TryGetValue(toolName, out Type? type) ? type : null;

    /// <summary>
    /// Registers all <c>[McpServerToolType]</c> classes from the given assemblies.
    /// </summary>
    internal void RegisterFromAssemblies(IEnumerable<System.Reflection.Assembly> assemblies)
    {
        Type markerAttribute = typeof(McpServerToolTypeAttribute);

        foreach (System.Reflection.Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type is { IsAbstract: true } or { IsInterface: true })
                {
                    continue;
                }

                if (!type.IsDefined(markerAttribute, inherit: false))
                {
                    continue;
                }

                // Tool name convention: the SDK uses the class name or the [McpServerTool] Name property.
                // We register the class name — the visibility filter will try both.
                Register(type.Name, type);

                // Also register the full name for namespace-qualified lookups.
                if (type.FullName is not null)
                {
                    Register(type.FullName, type);
                }
            }
        }
    }
}
