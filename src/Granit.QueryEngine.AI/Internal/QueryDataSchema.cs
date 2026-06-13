using System.Text.Json;
using System.Text.Json.Nodes;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;

namespace Granit.QueryEngine.AI.Internal;

/// <summary>
/// Projects a <see cref="QueryMetadata"/> into the JSON Schema for a <c>query_data</c> tool and
/// maps the model-supplied arguments back into a validated <see cref="QueryRequest"/>. Only the
/// fields and operators the definition exposes are accepted — unknown ones are dropped and
/// reported, never forwarded.
/// </summary>
internal static class QueryDataSchema
{
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
                ["description"] = "Filter clauses, ANDed together. Use only the listed fields and operators.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["field"] = new JsonObject { ["type"] = "string", ["enum"] = fieldNames },
                        ["operator"] = new JsonObject { ["type"] = "string", ["enum"] = operators },
                        ["value"] = new JsonObject { ["type"] = "string" },
                    },
                    ["required"] = new JsonArray("field", "operator", "value"),
                    ["additionalProperties"] = false,
                },
            };
        }

        properties["search"] = new JsonObject
        {
            ["type"] = "string",
            ["description"] = "Free-text search across the definition's searchable fields, if any.",
        };

        if (metadata.SortableFields.Count > 0)
        {
            string sortable = string.Join(", ", metadata.SortableFields.Select(s => s.Name));
            properties["sort"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = $"Comma-separated sort fields; prefix '-' for descending. Sortable: {sortable}.",
            };
        }

        properties["page"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 };
        properties["pageSize"] = new JsonObject
        {
            ["type"] = "integer",
            ["minimum"] = 1,
            ["maximum"] = metadata.Pagination.MaxPageSize,
            ["description"] = $"Defaults to {metadata.Pagination.DefaultPageSize}.",
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
    /// keys that were rejected because they are not exposed by the definition.
    /// </summary>
    public static (QueryRequest Request, IReadOnlyList<string> IgnoredFilters) BuildRequest(
        JsonElement arguments, QueryMetadata metadata)
    {
        HashSet<string> allowedKeys = [.. metadata.FilterableFields
            .SelectMany(f => f.Operators.Select(op => $"{f.Name}.{OperatorCode(op)}"))];

        Dictionary<string, string> filter = new(StringComparer.Ordinal);
        List<string> ignored = [];

        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("filters", out JsonElement filters)
            && filters.ValueKind == JsonValueKind.Array)
        {
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
                if (allowedKeys.Contains(key))
                {
                    filter[key] = valueEl.GetString() ?? string.Empty;
                }
                else
                {
                    ignored.Add(key);
                }
            }
        }

        QueryRequest request = new()
        {
            Filter = filter.Count > 0 ? filter : null,
            Search = ReadString(arguments, "search"),
            Sort = ReadString(arguments, "sort"),
            Page = ReadInt(arguments, "page"),
            PageSize = ClampPageSize(ReadInt(arguments, "pageSize"), metadata.Pagination),
        };

        return (request, ignored);
    }

    private static int ClampPageSize(int? requested, PaginationMeta pagination)
    {
        int size = requested ?? pagination.DefaultPageSize;
        return Math.Clamp(size, 1, pagination.MaxPageSize);
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

    private static string OperatorCode(FilterOperator op) => op.ToString().ToLowerInvariant();
}
