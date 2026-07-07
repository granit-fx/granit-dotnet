using System.Text.Json;
using System.Text.Json.Nodes;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;

namespace Granit.QueryEngine.AI.Tools.Internal;

/// <summary>
/// Projects a <see cref="QueryMetadata"/> into the JSON Schema for a <c>query_data</c> tool and
/// maps the model-supplied arguments back into a validated <see cref="QueryRequest"/>. Only the
/// fields and operators the definition exposes are accepted — unknown ones are dropped and
/// reported, never forwarded.
/// </summary>
internal static class QueryDataSchema
{
    private const string DescriptionKey = "description";
    private const string StringType = "string";

    /// <summary>Builds the tool parameter schema from a query definition's metadata.</summary>
    public static JsonElement Build(QueryMetadata metadata)
    {
        JsonObject properties = [];

        if (metadata.FilterableFields.Count > 0)
        {
            JsonArray fieldNames = [.. metadata.FilterableFields.Select(f => (JsonNode)f.Name)];
            JsonArray operators = [.. metadata.FilterableFields
                .SelectMany(f => f.Operators)
                .Distinct()
                .Select(op => (JsonNode)OperatorCode(op))];

            properties["filters"] = new JsonObject
            {
                ["type"] = "array",
                [DescriptionKey] = "Filter clauses, ANDed together. Use only the listed fields and operators.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["field"] = new JsonObject { ["type"] = StringType, ["enum"] = fieldNames },
                        ["operator"] = new JsonObject { ["type"] = StringType, ["enum"] = operators },
                        ["value"] = new JsonObject { ["type"] = StringType },
                    },
                    ["required"] = new JsonArray("field", "operator", "value"),
                    ["additionalProperties"] = false,
                },
            };
        }

        properties["search"] = new JsonObject
        {
            ["type"] = StringType,
            [DescriptionKey] = "Free-text search across the definition's searchable fields, if any.",
        };

        if (metadata.SortableFields.Count > 0)
        {
            string sortable = string.Join(", ", metadata.SortableFields.Select(s => s.Name));
            properties["sort"] = new JsonObject
            {
                ["type"] = StringType,
                [DescriptionKey] = $"Comma-separated sort fields; prefix '-' for descending. Sortable: {sortable}.",
            };
        }

        properties["page"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 };
        properties["pageSize"] = new JsonObject
        {
            ["type"] = "integer",
            ["minimum"] = 1,
            ["maximum"] = metadata.Pagination.MaxPageSize,
            [DescriptionKey] = $"Defaults to {metadata.Pagination.DefaultPageSize}.",
        };

        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    /// <summary>
    /// Maps the model arguments onto a validated <see cref="QueryRequest"/>, returning any filter
    /// keys that were rejected because they are not exposed by the definition. Whitelisting and
    /// pagination clamping are delegated to the shared <see cref="QueryRequestSanitizer"/> so this
    /// path cannot drift from the NLQ translator path.
    /// </summary>
    public static (QueryRequest Request, IReadOnlyList<string> IgnoredFilters) BuildRequest(
        JsonElement arguments, QueryMetadata metadata)
    {
        List<KeyValuePair<string, string>>? filter = null;

        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("filters", out JsonElement filters)
            && filters.ValueKind == JsonValueKind.Array)
        {
            filter = [];
            foreach (JsonElement clause in filters.EnumerateArray())
            {
                if (clause.ValueKind != JsonValueKind.Object
                    || !clause.TryGetProperty("field", out JsonElement fieldEl)
                    || !clause.TryGetProperty("operator", out JsonElement opEl)
                    || !clause.TryGetProperty("value", out JsonElement valueEl))
                {
                    continue;
                }

                string key = $"{fieldEl.GetString()}.{opEl.GetString()?.ToLowerInvariant()}";
                filter.Add(new KeyValuePair<string, string>(key, valueEl.GetString() ?? string.Empty));
            }
        }

        QueryRequestCandidate candidate = new()
        {
            Filter = filter,
            Search = ReadString(arguments, "search"),
            Sort = ReadString(arguments, "sort"),
            Page = ReadInt(arguments, "page"),
            PageSize = ReadInt(arguments, "pageSize") ?? metadata.Pagination.DefaultPageSize,
        };

        QueryRequestSanitizationResult result = QueryRequestSanitizer.Sanitize(candidate, metadata);
        return (result.Request, result.IgnoredFilters);
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

    private static string OperatorCode(FilterOperator op) => QueryRequestSanitizer.OperatorCode(op);
}
