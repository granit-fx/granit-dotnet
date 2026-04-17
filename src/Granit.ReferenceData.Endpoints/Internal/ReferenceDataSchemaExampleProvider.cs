using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.ReferenceData.Endpoints.Dtos;

namespace Granit.ReferenceData.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for reference data Request DTOs.
/// </summary>
internal sealed class ReferenceDataSchemaExampleProvider : ISchemaExampleProvider
{
    private const string Europe = "Europe";
    private const string Europa = "Europa";

    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(ReferenceDataCreateRequest)] = new JsonObject
            {
                ["code"] = "EUR",
                ["labelEn"] = Europe,
                ["labelFr"] = Europe,
                ["labelNl"] = Europa,
                ["labelDe"] = Europa,
                ["labelEs"] = Europa,
                ["labelIt"] = Europa,
                ["labelPt"] = Europa,
                ["sortOrder"] = 1,
            },
            [typeof(ReferenceDataUpdateRequest)] = new JsonObject
            {
                ["labelEn"] = Europe,
                ["labelFr"] = Europe,
                ["labelNl"] = Europa,
                ["labelDe"] = Europa,
                ["labelEs"] = Europa,
                ["labelIt"] = Europa,
                ["labelPt"] = Europa,
                ["sortOrder"] = 1,
                ["activated"] = true,
            },
        };
}
