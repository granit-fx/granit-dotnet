using Granit.AI.Tools.Search.Internal;
using Granit.AI.Tools.Search.Options;
using Granit.AI.VectorData;
using Granit.Indexing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.AI.Tools.Search;

/// <summary>
/// Opt-in surface for exposing search corpora as <c>search_{name}</c> tools. A corpus that is
/// never added is never exposed to the agent.
/// </summary>
public sealed class SearchToolBuilder(IServiceCollection services)
{
    /// <summary>
    /// Exposes a configured semantic collection as <c>search_{name}</c>. Results carry similarity
    /// scores. Requires <c>ISemanticSearchService</c> and the collection to be configured by a
    /// vector provider package.
    /// </summary>
    /// <param name="name">Tool-name-safe corpus identifier (e.g. <c>docs</c>).</param>
    /// <param name="collectionName">The vector collection to query.</param>
    /// <param name="description">Optional model-facing description.</param>
    public SearchToolBuilder AddSemantic(string name, string collectionName, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        services.AddScoped<IAITool>(sp => new SearchTool(
            new SemanticCorpusSearcher(name, description, collectionName,
                sp.GetRequiredService<ISemanticSearchService>()),
            sp.GetRequiredService<IOptions<GranitAIToolsSearchOptions>>().Value));

        return this;
    }

    /// <summary>
    /// Exposes a full-text index as <c>search_{name}</c>. Tenant isolation and per-record ACL are
    /// applied by the registered <c>ISearchService&lt;TKey, TResult&gt;</c>.
    /// </summary>
    /// <typeparam name="TKey">The index key type.</typeparam>
    /// <typeparam name="TResult">The index result type.</typeparam>
    /// <param name="name">Tool-name-safe corpus identifier.</param>
    /// <param name="textSelector">Extracts the snippet text from a result.</param>
    /// <param name="idSelector">Optional: extracts the source id from a result for citation.</param>
    /// <param name="description">Optional model-facing description.</param>
    public SearchToolBuilder AddFullText<TKey, TResult>(
        string name,
        Func<TResult, string> textSelector,
        Func<TResult, string>? idSelector = null,
        string? description = null)
        where TKey : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(textSelector);

        services.AddScoped<IAITool>(sp => new SearchTool(
            new FullTextCorpusSearcher<TKey, TResult>(name, description, textSelector, idSelector,
                sp.GetRequiredService<ISearchService<TKey, TResult>>()),
            sp.GetRequiredService<IOptions<GranitAIToolsSearchOptions>>().Value));

        return this;
    }
}
