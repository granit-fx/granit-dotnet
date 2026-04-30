using System.Text;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Adds human-readable descriptions to path and query parameters that ASP.NET Core
/// Minimal APIs cannot describe via attributes alone. Resolution order:
/// <list type="number">
///   <item>Existing description on the parameter — never overwritten.</item>
///   <item>Exact match in <see cref="s_descriptions"/> (well-known names).</item>
///   <item>Convention fallback: <c>*Id</c> / <c>*Name</c> / <c>*Type</c> / <c>page*</c> /
///         pagination tokens.</item>
/// </list>
/// </summary>
internal sealed class ParameterDescriptionOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly Dictionary<string, string> s_descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Identity
        ["userId"] = "External user identifier from the identity provider.",
        ["sessionId"] = "Session identifier — opaque token bound to a single browser/device session.",
        ["impersonationId"] = "Impersonation grant identifier.",

        // Authorization
        ["roleName"] = "Role name (e.g. 'admin', 'practitioner').",
        ["permissionName"] = "Permission identifier (e.g. 'Users.Read').",
        ["clientId"] = "OAuth/OIDC client identifier (registered application).",
        ["scopeName"] = "OAuth/OIDC scope name.",
        ["authorizationId"] = "OAuth/OIDC authorization grant identifier.",

        // Reference data
        ["code"] = "Reference data code (e.g. ISO 3166-1 Alpha-2 for countries: 'BE', 'FR').",

        // Background jobs
        ["name"] = "Unique registered name of the background job.",

        // Timeline & Workflow
        ["entityType"] = "Fully qualified entity type (e.g. 'Acme.Patients').",
        ["entityId"] = "Entity identifier (primary key).",
        ["entryId"] = "Timeline entry identifier (UUID).",
        ["entityName"] = "Entity definition name (matches the registered Granit.Entities entity).",

        // Data exchange
        ["jobId"] = "Import or export job identifier (UUID).",
        ["definitionName"] = "Import or export definition name.",
        ["presetName"] = "Export preset name.",

        // Notifications
        ["typeName"] = "Notification type name.",

        // QueryEngine / generic
        ["id"] = "Resource identifier (UUID).",
        ["correlationId"] = "Distributed tracing correlation identifier — groups events emitted in the same logical transaction.",
        ["tenantId"] = "Tenant identifier — scopes the request to a single tenant.",
        ["cultureName"] = "BCP 47 culture tag (e.g. 'fr', 'fr-BE', 'zh-Hant-TW').",
        ["resourceName"] = "Localization resource name (matches a registered LocalizationResource type).",
        ["key"] = "Translation key within the resource (e.g. 'Permission:Users.Read').",
        ["providerName"] = "External provider identifier (e.g. 'keycloak', 'auth0', 'stripe').",
        ["workspaceName"] = "Workspace identifier — isolates configuration and routing per AI workspace.",
        ["flagName"] = "Feature flag name (e.g. 'beta.passkeys').",
        ["meterId"] = "Metering meter identifier.",
        ["addressId"] = "Address identifier within the parent aggregate.",
        ["emailId"] = "Email identifier within the parent aggregate.",
        ["phoneId"] = "Phone identifier within the parent aggregate.",
        ["survivorId"] = "Identifier of the surviving record in a merge operation (the duplicate is merged into this one).",
        ["deliveryId"] = "Webhook delivery attempt identifier.",
        ["subscriptionId"] = "Subscription identifier.",
        ["seatId"] = "Subscription seat identifier.",
        ["planId"] = "Subscription plan identifier.",
        ["priceId"] = "Subscription price identifier.",
        ["transactionId"] = "Payment transaction identifier.",
        ["methodType"] = "Payment method type (e.g. 'card', 'sepa', 'paypal').",
        ["containerName"] = "Blob container name.",
        ["scheduleId"] = "Scheduled action identifier.",
        ["groupId"] = "Group identifier.",
        ["role"] = "Role name (e.g. 'owner', 'member').",
        ["currency"] = "ISO 4217 three-letter currency code (e.g. 'EUR', 'USD').",

        // Pagination — covers endpoints that don't go through MapGranitQuery
        ["page"] = "1-based page index.",
        ["pageSize"] = "Number of items per page.",
        ["cursor"] = "Opaque cursor for keyset pagination. When supplied, overrides page.",
        ["search"] = "Free-text search query.",
        ["status"] = "Filter by status value.",
        ["environment"] = "Filter by environment (e.g. 'production', 'staging', 'sandbox').",
        ["includerevoked"] = "When true, include revoked items in the response.",
        ["type"] = "Filter by type discriminator value.",
        ["token"] = "Single-use token from a confirmation/reset email link.",
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
            if (!string.IsNullOrEmpty(parameter.Description) || string.IsNullOrEmpty(parameter.Name))
            {
                continue;
            }

            string name = parameter.Name;

            if (s_descriptions.TryGetValue(name, out string? description))
            {
                parameter.Description = description;
                continue;
            }

            // Convention fallback — only for path parameters (query params are too varied).
            if (parameter.In == ParameterLocation.Path)
            {
                parameter.Description = DeriveDescription(name);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Derives a generic description from a camelCase parameter name following the
    /// <c>{noun}Id</c> / <c>{noun}Name</c> / <c>{noun}Type</c> conventions.
    /// </summary>
    private static string? DeriveDescription(string name)
    {
        if (name.EndsWith("Id", StringComparison.Ordinal) && name.Length > 2)
        {
            string noun = HumanizeCamelCase(name[..^2]);
            return $"Unique identifier of the {noun}.";
        }

        if (name.EndsWith("Name", StringComparison.Ordinal) && name.Length > 4)
        {
            string noun = HumanizeCamelCase(name[..^4]);
            return $"Name of the {noun}.";
        }

        if (name.EndsWith("Type", StringComparison.Ordinal) && name.Length > 4)
        {
            string noun = HumanizeCamelCase(name[..^4]);
            return $"Type of the {noun}.";
        }

        return null;
    }

    /// <summary>
    /// Converts a camelCase identifier to a lowercase space-separated phrase.
    /// <c>"survivor"</c> → <c>"survivor"</c>; <c>"externalLogin"</c> → <c>"external login"</c>.
    /// </summary>
    private static string HumanizeCamelCase(string camelCase)
    {
        StringBuilder sb = new(camelCase.Length + 4);
        foreach (char c in camelCase)
        {
            if (char.IsUpper(c) && sb.Length > 0)
            {
                sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }
}
