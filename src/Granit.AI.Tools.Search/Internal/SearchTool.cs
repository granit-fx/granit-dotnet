using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Granit.AI.Tools.Search.Options;

namespace Granit.AI.Tools.Search.Internal;

/// <summary>
/// An <see cref="IAITool"/> over a single search corpus, named <c>search_{corpus}</c>. Returns
/// ranked snippets the agent can ground its answer on, bounded by the caller's scope.
/// </summary>
internal sealed class SearchTool(ICorpusSearcher corpus, GranitAIToolsSearchOptions options)
    : IAITool, IAIToolInstructions
{
    private static readonly JsonSerializerOptions ResultSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => $"search_{corpus.Name}";

    public string Description => corpus.Description
        ?? $"Search the '{corpus.Name}' corpus and return ranked snippets to ground your answer. "
        + "Results are limited to what the current user may access.";

    public JsonElement ParameterSchema { get; } = BuildSchema(options.MaxLimit, options.DefaultLimit);

    public string Instructions =>
        $"Use '{Name}' to retrieve supporting passages before answering questions about '{corpus.Name}'. "
        + "Quote or cite the returned snippets; do not invent content beyond them.";

    public async ValueTask<AIToolResult> InvokeAsync(
        AIToolInvocationContext context, CancellationToken cancellationToken = default)
    {
        string? query = ReadString(context.Arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
        {
            return AIToolResult.Error("The 'query' argument is required.");
        }

        int limit = Math.Clamp(
            ReadInt(context.Arguments, "limit") ?? options.DefaultLimit, 1, options.MaxLimit);

        IReadOnlyList<SearchSnippet> snippets = await corpus
            .SearchAsync(query, limit, cancellationToken)
            .ConfigureAwait(false);

        var payload = new
        {
            corpus = corpus.Name,
            mode = corpus.Mode,
            count = snippets.Count,
            snippets,
        };

        return AIToolResult.Success(JsonSerializer.Serialize(payload, ResultSerializerOptions));
    }

    private static JsonElement BuildSchema(int maxLimit, int defaultLimit)
    {
        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["query"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "What to search for, in natural language.",
                },
                ["limit"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["minimum"] = 1,
                    ["maximum"] = maxLimit,
                    ["description"] = $"Maximum snippets to return (defaults to {defaultLimit}).",
                },
            },
            ["required"] = new JsonArray("query"),
            ["additionalProperties"] = false,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string? ReadString(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int parsed)
            ? parsed
            : null;
}
