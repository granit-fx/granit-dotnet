using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Querying.SavedViews;

namespace Granit.Querying.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for querying Request DTOs.
/// </summary>
internal sealed class QueryingSchemaExampleProvider : ISchemaExampleProvider
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
