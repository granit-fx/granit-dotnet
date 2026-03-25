using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Adds human-readable descriptions to well-known path and query parameters
/// that ASP.NET Core Minimal APIs cannot describe via attributes alone.
/// Descriptions are only set when not already present.
/// </summary>
internal sealed class ParameterDescriptionOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly Dictionary<string, string> s_descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Identity
        ["userId"] = "External user identifier from the identity provider.",

        // Authorization
        ["roleName"] = "Role name (e.g. 'admin', 'practitioner').",
        ["permissionName"] = "Permission identifier (e.g. 'Users.Read').",

        // Reference data
        ["code"] = "Reference data code (e.g. ISO 3166-1 Alpha-2 for countries: 'BE', 'FR').",

        // Background jobs
        ["name"] = "Unique registered name of the background job.",

        // Timeline & Workflow
        ["entityType"] = "Fully qualified entity type (e.g. 'Acme.Patients').",
        ["entityId"] = "Entity identifier (primary key).",
        ["entryId"] = "Timeline entry identifier (UUID).",

        // Data exchange
        ["jobId"] = "Import or export job identifier (UUID).",
        ["definitionName"] = "Import or export definition name.",
        ["presetName"] = "Export preset name.",

        // Notifications
        ["typeName"] = "Notification type name.",

        // QueryEngine
        ["id"] = "Resource identifier (UUID).",
    };

    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        foreach (IOpenApiParameter parameter in operation.Parameters)
        {
            if (!string.IsNullOrEmpty(parameter.Description))
            {
                continue;
            }

            if (parameter.Name is not null
                && s_descriptions.TryGetValue(parameter.Name, out string? description))
            {
                parameter.Description = description;
            }
        }

        return Task.CompletedTask;
    }
}
