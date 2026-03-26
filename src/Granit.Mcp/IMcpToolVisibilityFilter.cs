namespace Granit.Mcp;

/// <summary>
/// Filters MCP tool visibility in <c>tools/list</c> responses.
/// Implementations are invoked via the SDK's <c>AddListToolsFilter</c> pipeline.
/// </summary>
/// <remarks>
/// This controls <em>visibility</em> (whether a tool appears in the listing),
/// not <em>authorization</em> (whether a tool can be invoked). For authorization,
/// use standard <c>[Authorize(Policy = "...")]</c> on tool methods.
/// </remarks>
public interface IMcpToolVisibilityFilter
{
    /// <summary>
    /// Determines whether a tool should be visible in the current request context.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="toolType">The CLR type of the tool class (for attribute inspection).</param>
    /// <param name="services">Request-scoped service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the tool should be visible; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> IsVisibleAsync(
        string toolName,
        Type? toolType,
        IServiceProvider services,
        CancellationToken cancellationToken = default);
}
