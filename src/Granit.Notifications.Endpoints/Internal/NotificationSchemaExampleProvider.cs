using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Notifications.Endpoints.Dtos;

namespace Granit.Notifications.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for notification Request DTOs.
/// </summary>
internal sealed class NotificationSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(NotificationPreferenceUpdateRequest)] = new JsonObject
            {
                ["notificationTypeName"] = "NewMessage",
                ["channelName"] = "Email",
                ["isEnabled"] = true,
            },
        };
}
