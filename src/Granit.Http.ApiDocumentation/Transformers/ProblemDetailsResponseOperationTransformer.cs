using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Adds RFC 7807 ProblemDetails error responses to OpenAPI operations based on endpoint metadata.
/// <list type="bullet">
///   <item>401 + 403: added when <c>[Authorize]</c> is present (unless <c>[AllowAnonymous]</c>)</item>
///   <item>422: added when the operation has a request body and no existing 400 validation response</item>
///   <item>404: enriched with ProblemDetails schema when present (route parameters) but lacks content</item>
///   <item>500: added on all operations</item>
/// </list>
/// Also removes phantom 404 responses added by Wolverine on endpoints without route parameters.
/// </summary>
/// <remarks>
/// All error responses reference the shared <c>#/components/schemas/ProblemDetails</c> schema
/// registered by <see cref="ProblemDetailsSchemaDocumentTransformer"/>.
/// </remarks>
internal sealed class ProblemDetailsResponseOperationTransformer : IOpenApiOperationTransformer
{
    internal const string SchemaName = "ProblemDetails";
    internal const string ProblemDetailsMediaType = "application/problem+json";

    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool hasAuthorize = metadata.OfType<AuthorizeAttribute>().Any();
        bool hasAllowAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();
        bool isProtected = hasAuthorize && !hasAllowAnonymous;
        bool hasRequestBody = operation.RequestBody is not null;
        bool hasRouteParameter = operation.Parameters?
            .Any(p => p.In == ParameterLocation.Path) ?? false;

        operation.Responses ??= [];

        // Remove phantom 404 from Wolverine on endpoints without route parameters.
        if (!hasRouteParameter && operation.Responses.ContainsKey("404"))
        {
            operation.Responses.Remove("404");
        }

        OpenApiResponses responses = operation.Responses;

        if (isProtected)
        {
            EnsureResponse(responses, "401", "Unauthorized");
            EnsureResponse(responses, "403", "Forbidden");
        }

        // Add 422 only when there's a request body and no existing 400 (ASP.NET validation).
        // When ASP.NET adds a 400 with HttpValidationProblemDetails, a separate 422 is redundant.
        if (hasRequestBody && !responses.ContainsKey("400"))
        {
            EnsureResponse(responses, "422", "Unprocessable Entity");
        }

        // Enrich existing 404 responses (from TypedResults.NotFound) with ProblemDetails schema.
        EnrichResponseWithProblemDetails(responses, "404");

        EnsureResponse(responses, "500", "Internal Server Error");

        return Task.CompletedTask;
    }

    private static void EnsureResponse(
        OpenApiResponses responses,
        string statusCode,
        string description)
    {
        if (responses.ContainsKey(statusCode))
        {
            return;
        }

        responses[statusCode] = new OpenApiResponse
        {
            Description = description,
            Content = ProblemDetailsContent(),
        };
    }

    /// <summary>
    /// If the response exists but has no <c>application/problem+json</c> content, adds the schema.
    /// </summary>
    private static void EnrichResponseWithProblemDetails(
        OpenApiResponses responses,
        string statusCode)
    {
        if (!responses.TryGetValue(statusCode, out IOpenApiResponse? value) || value is not OpenApiResponse response)
        {
            return;
        }

        response.Content ??= new Dictionary<string, OpenApiMediaType>();

        if (!response.Content.ContainsKey(ProblemDetailsMediaType))
        {
            response.Content[ProblemDetailsMediaType] = new OpenApiMediaType
            {
                Schema = new OpenApiSchemaReference(SchemaName, null),
            };
        }
    }

    private static Dictionary<string, OpenApiMediaType> ProblemDetailsContent() =>
        new()
        {
            [ProblemDetailsMediaType] = new OpenApiMediaType
            {
                Schema = new OpenApiSchemaReference(SchemaName, null),
            },
        };
}
