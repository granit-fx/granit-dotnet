using Granit.AI.Tools.Search.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Tools.Search.Extensions;

/// <summary>
/// Registers <c>search</c> (RAG) tools on the Granit AI tool registry.
/// </summary>
public static class SearchToolRegistrationExtensions
{
    /// <summary>
    /// Opts search corpora in as ACL-bound <c>search_{name}</c> tools the chat agent can call.
    /// </summary>
    /// <param name="tools">The AI tool registration builder.</param>
    /// <param name="configure">Selects which corpora to expose.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddGranitAITools(tools => tools.AddSearch(s =>
    /// {
    ///     s.AddSemantic("docs", collectionName: "documentation", description: "Product docs");
    ///     s.AddFullText&lt;Guid, ArticleHit&gt;("articles", hit => hit.Snippet, hit => hit.Id.ToString());
    /// }));
    /// </code>
    /// </example>
    public static AIToolRegistrationBuilder AddSearch(
        this AIToolRegistrationBuilder tools,
        Action<SearchToolBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(configure);

        tools.Services.AddOptions<GranitAIToolsSearchOptions>()
            .BindConfiguration(GranitAIToolsSearchOptions.SectionName);

        configure(new SearchToolBuilder(tools.Services));
        return tools;
    }
}
