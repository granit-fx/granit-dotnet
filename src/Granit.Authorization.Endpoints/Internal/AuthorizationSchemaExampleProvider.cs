using System.Text.Json.Nodes;
using Granit.Authorization.Endpoints.Dtos;
using Granit.Http.ApiDocumentation;

namespace Granit.Authorization.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for authorization Response DTOs.
/// </summary>
internal sealed class AuthorizationSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(MyPermissionsResponse)] = new JsonObject
            {
                ["permissions"] = new JsonArray
                {
                    "Settings.Global.Read",
                    "BlobStorage.Read",
                    "BlobStorage.Write",
                },
            },
            [typeof(PermissionGroupResponse)] = new JsonObject
            {
                ["name"] = "BlobStorage",
                ["displayName"] = "Blob Storage",
                ["permissions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "BlobStorage.Read",
                        ["displayName"] = "Read files",
                    },
                    new JsonObject
                    {
                        ["name"] = "BlobStorage.Write",
                        ["displayName"] = "Upload and delete files",
                    },
                },
            },
            [typeof(PermissionGrantResponse)] = new JsonObject
            {
                ["roleName"] = "Administrator",
                ["permissions"] = new JsonArray
                {
                    "Settings.Global.Read",
                    "Settings.Global.Manage",
                    "BlobStorage.Read",
                    "BlobStorage.Write",
                },
            },
        };
}
