using Granit.AI.Tools;

namespace Granit.QueryEngine.AI.Tools.Extensions;

/// <summary>
/// Registers <c>query_data</c> tools on the Granit AI tool registry.
/// </summary>
public static class QueryDataToolRegistrationExtensions
{
    /// <summary>
    /// Opts query definitions in as ACL-bound <c>query_data</c> tools the chat agent can call.
    /// </summary>
    /// <param name="tools">The AI tool registration builder.</param>
    /// <param name="configure">Selects which definitions to expose.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddGranitAITools(tools => tools.AddQueryData(q =>
    /// {
    ///     q.Add&lt;Order&gt;("orders", "Customer orders with status and totals");
    ///     q.Add&lt;Product&gt;("products");
    /// }));
    /// </code>
    /// </example>
    public static AIToolRegistrationBuilder AddQueryData(
        this AIToolRegistrationBuilder tools,
        Action<QueryDataToolBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(configure);

        configure(new QueryDataToolBuilder(tools.Services));
        return tools;
    }
}
