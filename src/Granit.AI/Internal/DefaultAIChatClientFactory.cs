using Granit.AI.Exceptions;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.Internal;

/// <summary>
/// Default factory that resolves an <see cref="IChatClient"/> by workspace name.
/// </summary>
internal sealed class DefaultAIChatClientFactory(
    IAIWorkspaceProvider workspaceProvider,
    IEnumerable<IAIProviderFactory> providerFactories,
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

        return providerFactory.CreateChatClient(workspace);
    }
}
