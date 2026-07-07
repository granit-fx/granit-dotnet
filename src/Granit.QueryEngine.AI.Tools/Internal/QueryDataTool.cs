using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.AI.Tools;
using Granit.QueryEngine.Meta;

namespace Granit.QueryEngine.AI.Tools.Internal;

/// <summary>
/// An <see cref="IAITool"/> over a single opted-in <c>QueryDefinition</c>. Exposes the
/// definition's filterable structure as a tool schema and executes a structured
/// <see cref="QueryRequest"/> against the caller-scoped <see cref="IQueryableSource{TEntity}"/> —
/// so results are bounded by the same tenant and ACL filters the user is subject to everywhere
/// else (ADR-067). Only definitions the application registers are exposed.
/// </summary>
internal sealed class QueryDataTool<TEntity>(
    string name,
    string? description,
    IQueryEngine<TEntity> engine,
    IQueryableSource<TEntity> source) : IAITool, IAIToolInstructions
    where TEntity : class
{
    private readonly QueryMetadata _metadata = engine.GetMetadata();

    public string Name => $"query_{name}";

    public string Description => description
        ?? $"Query '{name}' records the current user is allowed to see. Returns a page of matching records as JSON.";

    public JsonElement ParameterSchema { get; } = QueryDataSchema.Build(engine.GetMetadata());

    public string Instructions =>
        $"Use '{Name}' to read '{name}' data. Filter with the listed fields/operators only, keep "
        + "pageSize small, and sort to surface the most relevant rows. Results are already "
        + "restricted to what the current user may access — never claim otherwise.";

    public async ValueTask<AIToolResult> InvokeAsync(
        AIToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        (QueryRequest request, IReadOnlyList<string> ignored) =
            QueryDataSchema.BuildRequest(context.Arguments, _metadata);

        IQueryable<TEntity> queryable = source.GetQueryable();
        PagedResult<TEntity> result = await engine
            .ExecuteAsync(queryable, request, cancellationToken)
            .ConfigureAwait(false);

        var payload = new
        {
            entity = name,
            totalCount = result.TotalCount,
            hasMore = result.HasMore,
            count = result.Items.Count,
            ignoredFilters = ignored.Count > 0 ? ignored : null,
            items = result.Items,
        };

        return AIToolResult.Success(JsonSerializer.Serialize(payload, QueryDataToolSerialization.ResultSerializerOptions));
    }
}

/// <summary>
/// Holds the JSON options shared by every closed <see cref="QueryDataTool{TEntity}"/>. A non-generic holder so
/// the options are allocated once, not once per <c>TEntity</c> (a static field in a generic type is per close
/// constructed type).
/// </summary>
file static class QueryDataToolSerialization
{
    public static readonly JsonSerializerOptions ResultSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
