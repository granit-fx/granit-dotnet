using Granit.AI.Diagnostics;
using Granit.AI.Exceptions;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Internal;

/// <summary>
/// Default factory that resolves an <see cref="IChatClient"/> by workspace name and wraps it
/// in the usage-tracking middleware (<see cref="UsageTrackingChatClient"/>), so every model
/// call stamps an <see cref="AIUsageRecord"/> without caller involvement.
/// </summary>
internal sealed class DefaultAIChatClientFactory(
    IAIWorkspaceProvider workspaceProvider,
    IEnumerable<IAIProviderFactory> providerFactories,
    IAIUsageTracker usageTracker,
    IAIUsageRecordFactory usageRecordFactory,
    AIMetrics metrics,
    TimeProvider timeProvider,
    ILogger<UsageTrackingChatClient> usageLogger,
    IOptions<GranitAIOptions> options) : IAIChatClientFactory
{
    private readonly Dictionary<string, IAIProviderFactory> _providers =
        providerFactories.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);

    public async Task<IChatClient> CreateAsync(
        string? workspaceName = null,
        CancellationToken cancellationToken = default)
    {
        string name = workspaceName ?? options.Value.DefaultWorkspace;

        AIWorkspace workspace = await workspaceProvider.GetAsync(name, cancellationToken).ConfigureAwait(false)
            ?? throw new AIWorkspaceNotFoundException(name);

        if (!workspace.Activated)
        {
            throw new AIWorkspaceNotActiveException(name);
        }

        if (!_providers.TryGetValue(workspace.Provider, out IAIProviderFactory? providerFactory))
        {
            throw new AIProviderNotRegisteredException(workspace.Provider);
        }

        IChatClient inner = await providerFactory
            .CreateChatClientAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        return new UsageTrackingChatClient(
            inner, name, workspace, usageTracker, usageRecordFactory, metrics, timeProvider, usageLogger);
    }
}
