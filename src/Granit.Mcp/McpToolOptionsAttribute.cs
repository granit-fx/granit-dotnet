namespace Granit.Mcp;

/// <summary>
/// Declarative tool metadata that maps to <c>McpServerToolCreateOptions</c>.
/// Provides icons and annotations without requiring imperative registration.
/// </summary>
/// <remarks>
/// For advanced cases (multiple icon sizes, dynamic tools), implement
/// <see cref="IMcpToolContributor"/> instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class McpToolOptionsAttribute : Attribute
{
    /// <summary>Light theme icon URL (e.g., SVG or PNG).</summary>
    public string? IconLight { get; init; }

    /// <summary>Dark theme icon URL.</summary>
    public string? IconDark { get; init; }

    /// <summary>Icon MIME type. Default: <c>"image/svg+xml"</c>.</summary>
    public string IconMimeType { get; init; } = "image/svg+xml";

    /// <summary>
    /// Maps to <c>Annotations.DestructiveHint</c>. When <see langword="true"/>,
    /// MCP clients should prompt for confirmation before invoking this tool.
    /// </summary>
    public bool Destructive { get; init; }

    /// <summary>
    /// Maps to <c>Annotations.ReadOnlyHint</c>. When <see langword="true"/>,
    /// indicates this tool has no side effects.
    /// </summary>
    public bool ReadOnly { get; init; }
}
