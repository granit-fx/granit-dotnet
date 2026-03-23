using System.Text.Json.Nodes;
using Granit.AuditLog.Endpoints.Dtos;
using Granit.Http.ApiDocumentation;

namespace Granit.AuditLog.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for audit log Response DTOs.
/// </summary>
internal sealed class AuditLogSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(AuditLogEntryResponse)] = new JsonObject
            {
                ["id"] = "b3f7a1c2-9d4e-4f8a-b6c5-1e2d3f4a5b6c",
                ["timestamp"] = "2026-03-17T10:15:30+00:00",
                ["userId"] = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                ["userName"] = "Jane Doe",
                ["category"] = "DataMutation",
                ["ipAddress"] = "198.51.100.42",
                ["tenantId"] = "d4e5f6a7-b8c9-0123-4567-89abcdef0123",
                ["correlationId"] = "req-abc123def456",
                ["entityChangeCount"] = 2,
            },
            [typeof(AuditLogEntryDetailResponse)] = new JsonObject
            {
                ["id"] = "b3f7a1c2-9d4e-4f8a-b6c5-1e2d3f4a5b6c",
                ["timestamp"] = "2026-03-17T10:15:30+00:00",
                ["userId"] = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                ["userName"] = "Jane Doe",
                ["category"] = "DataMutation",
                ["ipAddress"] = "198.51.100.42",
                ["tenantId"] = "d4e5f6a7-b8c9-0123-4567-89abcdef0123",
                ["correlationId"] = "req-abc123def456",
                ["entityChanges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["entityType"] = "Patient",
                        ["entityId"] = "42",
                        ["changeType"] = "Modified",
                        ["propertyChanges"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["propertyName"] = "Email",
                                ["originalValue"] = "old@example.com",
                                ["newValue"] = "new@example.com",
                            },
                        },
                    },
                },
            },
        };
}
