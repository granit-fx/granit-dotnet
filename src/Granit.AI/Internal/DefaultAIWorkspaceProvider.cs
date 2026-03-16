using Granit.AI.Workspaces;

namespace Granit.AI.Internal;

/// <summary>
/// Default workspace provider that merges system workspaces (from code) with dynamic workspaces (from store).
/// System workspaces take precedence over dynamic ones with the same name.
/// </summary>
internal sealed class DefaultAIWorkspaceProvider(
    IEnumerable<IAIWorkspaceDefinitionProvider> definitionProviders,
    IAIWorkspaceStoreReader storeReader) : IAIWorkspaceProvider
{
    private readonly Lazy<IReadOnlyDictionary<string, AIWorkspace>> _systemWorkspaces = new(() =>
    {
        AIWorkspaceDefinitionContext context = new();
        foreach (IAIWorkspaceDefinitionProvider provider in definitionProviders)
        {
            provider.Define(context);
        }

        return context.Workspaces;
    });

    public async Task<AIWorkspace?> GetAsync(string workspaceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceName);

        if (_systemWorkspaces.Value.TryGetValue(workspaceName, out AIWorkspace? systemWorkspace))
        {
            return systemWorkspace;
        }

        return await storeReader.FindAsync(workspaceName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AIWorkspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AIWorkspace> dynamicWorkspaces = await storeReader.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var systemNames = _systemWorkspaces.Value.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<AIWorkspace> merged = new(_systemWorkspaces.Value.Values);
        merged.AddRange(dynamicWorkspaces.Where(w => !systemNames.Contains(w.Name)));

        return merged;
    }
}
