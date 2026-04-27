using System.Text.Json.Nodes;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.Http.ApiDocumentation;

namespace Granit.DataExchange.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for data exchange Request DTOs.
/// </summary>
internal sealed class DataExchangeSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(ConfirmMappingsRequest)] = new JsonObject
            {
                ["mappings"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["sourceColumn"] = "Nom",
                        ["targetProperty"] = "LastName",
                        ["confidence"] = 0.95,
                    },
                },
            },
            [typeof(CreateExportJobRequest)] = new JsonObject
            {
                ["definitionName"] = "Acme.PatientExport",
                ["format"] = "xlsx",
                ["selectedFields"] = new JsonArray { "LastName", "FirstName", "Email" },
                ["includeIdForImport"] = false,
                ["sort"] = "-createdAt,lastName",
            },
            [typeof(SaveExportPresetRequest)] = new JsonObject
            {
                ["definitionName"] = "Acme.PatientExport",
                ["presetName"] = "Party details",
                ["selectedFields"] = new JsonArray { "LastName", "FirstName", "Email", "Phone" },
                ["format"] = "xlsx",
                ["includeIdForImport"] = false,
            },
        };
}
