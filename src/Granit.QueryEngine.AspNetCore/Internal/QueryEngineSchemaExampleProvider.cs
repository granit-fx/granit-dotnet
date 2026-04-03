using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.QueryEngine.SavedViews;

namespace Granit.QueryEngine.AspNetCore.Internal;

/// <summary>
/// Provides OpenAPI schema examples for QueryEngine request DTOs.
/// </summary>
internal sealed class QueryEngineSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(CreateSavedViewRequest)] = new JsonObject
            {
                ["name"] = "Active patients",
                ["isShared"] = false,
                ["isDefault"] = true,
                ["filterJson"] = """{"status":{"eq":"Active"}}""",
                ["sortJson"] = """[{"field":"lastName","desc":false}]""",
                ["visibleColumnsJson"] = """["LastName","FirstName","Email"]""",
            },
            [typeof(UpdateSavedViewRequest)] = new JsonObject
            {
                ["name"] = "Active patients (updated)",
                ["isShared"] = true,
                ["filterJson"] = """{"status":{"eq":"Active"}}""",
                ["sortJson"] = """[{"field":"lastName","desc":false}]""",
            },
        };
}
