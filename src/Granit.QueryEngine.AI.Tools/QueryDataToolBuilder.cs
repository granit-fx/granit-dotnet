using Granit.AI.Tools;
using Granit.QueryEngine.AI.Tools.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.QueryEngine.AI.Tools;

/// <summary>
/// Opt-in surface for exposing <c>QueryDefinition</c>s as ACL-bound <c>query_data</c> tools.
/// Each <see cref="Add{TEntity}"/> registers one tool; a definition that is never added is never
/// exposed to the agent and cannot be invoked.
/// </summary>
public sealed class QueryDataToolBuilder(IServiceCollection services)
{
    /// <summary>
    /// Exposes the query definition for <typeparamref name="TEntity"/> as a tool named
    /// <c>query_{name}</c>. Requires <c>IQueryEngine&lt;TEntity&gt;</c> and a caller-scoped
    /// <c>IQueryableSource&lt;TEntity&gt;</c> to be registered — the latter is what binds results
    /// to the current user's tenant and ACLs.
    /// </summary>
    /// <typeparam name="TEntity">The queried entity.</typeparam>
    /// <param name="name">Short, tool-name-safe identifier (e.g. <c>orders</c>).</param>
    /// <param name="description">Optional model-facing description; a default is generated.</param>
    public QueryDataToolBuilder Add<TEntity>(string name, string? description = null)
        where TEntity : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.AddScoped<IAITool>(sp => new QueryDataTool<TEntity>(
            name,
            description,
            sp.GetRequiredService<IQueryEngine<TEntity>>(),
            sp.GetRequiredService<IQueryableSource<TEntity>>()));

        return this;
    }
}
