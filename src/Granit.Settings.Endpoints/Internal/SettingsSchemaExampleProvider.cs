using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Settings.Endpoints.Dtos;

namespace Granit.Settings.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for settings Request DTOs.
/// </summary>
internal sealed class SettingsSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(UpdateSettingValueRequest)] = new JsonObject
            {
                ["value"] = "Europe/Brussels",
            },
            [typeof(SettingValueResponse)] = new JsonObject
            {
                ["name"] = "Granit.Timing.Timezone",
                ["value"] = "Europe/Brussels",
            },
        };
}
