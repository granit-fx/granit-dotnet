using Microsoft.Extensions.AI;

namespace Granit.AI;

/// <summary>
/// Factory that resolves an <see cref="IChatClient"/> configured for a specific workspace.
/// </summary>
/// <remarks>
/// The returned <c>IChatClient</c> is pre-configured with the workspace's provider, model,
/// system prompt, and parameters. Middleware is applied automatically: OpenTelemetry GenAI
/// tracing (provider-side <c>TracingChatClient</c>) and usage tracking — every model call
/// stamps an <see cref="AIUsageRecord"/> via <see cref="IAIUsageTracker"/>, enriched from the
/// scoped <see cref="AIUsageContext"/>. Callers must NOT stamp usage themselves.
/// </remarks>
public interface IAIChatClientFactory
{
    /// <summary>
    /// Creates an <see cref="IChatClient"/> for the specified workspace.
    /// </summary>
    /// <param name="workspaceName">
    /// Workspace name. If <c>null</c>, the default workspace from
    /// <see cref="Options.GranitAIOptions.DefaultWorkspace"/> is used.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fully configured <c>IChatClient</c>.</returns>
    /// <exception cref="Exceptions.AIWorkspaceNotFoundException">Workspace not found.</exception>
    /// <exception cref="Exceptions.AIProviderNotRegisteredException">No provider registered for the workspace's provider name.</exception>
    Task<IChatClient> CreateAsync(string? workspaceName = null, CancellationToken cancellationToken = default);
}
