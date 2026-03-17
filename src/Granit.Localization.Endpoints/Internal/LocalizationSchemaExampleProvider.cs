using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Endpoints.Dtos;

namespace Granit.Localization.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for localization Request DTOs.
/// </summary>
internal sealed class LocalizationSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(SetLocalizationOverrideRequest)] = new JsonObject
            {
                ["value"] = "Tableau de bord",
            },
        };
}
