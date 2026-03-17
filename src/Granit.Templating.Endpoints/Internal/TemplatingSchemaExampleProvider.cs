using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Templating.Endpoints.Dtos;

namespace Granit.Templating.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for templating Request DTOs.
/// </summary>
internal sealed class TemplatingSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(SaveTemplateRequest)] = new JsonObject
            {
                ["name"] = "Billing.Invoice",
                ["culture"] = "fr",
                ["content"] = "<h1>{{ model.title }}</h1><p>{{ model.description }}</p>",
                ["mimeType"] = "text/html",
            },
        };
}
