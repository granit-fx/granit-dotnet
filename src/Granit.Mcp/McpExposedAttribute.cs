namespace Granit.Mcp;

/// <summary>
/// Marks a class as explicitly exposed via MCP. Required in
/// <see cref="Options.McpToolDiscoveryMode.Explicit"/> mode (default) alongside
/// the SDK's <c>[McpServerToolType]</c> attribute.
/// </summary>
/// <remarks>
/// In <see cref="Options.McpToolDiscoveryMode.Auto"/> mode, this attribute is
/// ignored — all <c>[McpServerToolType]</c> classes are discovered.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class McpExposedAttribute : Attribute;
