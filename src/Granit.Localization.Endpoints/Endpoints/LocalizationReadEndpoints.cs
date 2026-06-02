using Granit.Localization.Endpoints.Dtos;
using Granit.Localization.Endpoints.Internal;
using Granit.Localization.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Granit.Localization.Endpoints.Endpoints;

/// <summary>
/// Read-only Minimal API endpoint for localization bootstrapping (SPA / anonymous).
/// </summary>
internal static class LocalizationReadEndpoints
{
    /// <summary>Maps the <c>GET /{prefix}/localization</c> endpoint to the given route builder.</summary>
    internal static IEndpointRouteBuilder MapLocalizationReadEndpoints(
        this IEndpointRouteBuilder endpoints,
        string routePrefix,
        string tagName)
    {
        endpoints
            .MapGet(routePrefix, HandleGetLocalization)
            .AllowAnonymous()
            .WithName("GetLocalization")
            .WithTags(tagName)
            .WithSummary("Returns all localization resources for the requested culture.")
            .WithDescription("Returns all localization resources (key-value pairs) for the requested culture, grouped by resource name. Accepts an optional cultureName query parameter (BCP 47 format); defaults to the Accept-Language header culture. Also returns the list of supported languages. Response is cached for 1 hour (Cache-Control: public, max-age=3600, Vary: Accept-Language). Anonymous — no authentication required.")
            .Produces<ApplicationLocalizationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AddOpenApiOperationTransformer(DescribeCultureNameParam);

        return endpoints;
    }

    private static Task DescribeCultureNameParam(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        IEnumerable<IOpenApiParameter> cultureNameQueryParams = operation.Parameters
            .Where(p => p.Name == "cultureName"
                && p.In == ParameterLocation.Query
                && p.Schema is OpenApiSchema);

        foreach (IOpenApiParameter parameter in cultureNameQueryParams)
        {
            var schema = (OpenApiSchema)parameter.Schema!;
            parameter.Description ??= "Optional BCP 47 culture tag (e.g. 'fr', 'fr-BE', 'zh-Hant-TW'). When omitted, the Accept-Language header is used.";
            schema.Pattern ??= "^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$";
            schema.Example ??= System.Text.Json.Nodes.JsonValue.Create("fr-BE");
        }

        return Task.CompletedTask;
    }

    private static Results<Ok<ApplicationLocalizationResponse>, ProblemHttpResult> HandleGetLocalization(
        HttpContext context,
        string? cultureName = null)
    {
        IOptions<GranitLocalizationOptions> options =
            context.RequestServices.GetRequiredService<IOptions<GranitLocalizationOptions>>();
        IStringLocalizerFactory localizerFactory =
            context.RequestServices.GetRequiredService<IStringLocalizerFactory>();

        if (!string.IsNullOrWhiteSpace(cultureName) && !LocalizationResponseMapper.Bcp47Pattern().IsMatch(cultureName))
        {
            return TypedResults.Problem(
                detail: $"Culture name '{cultureName}' is not supported.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ApplicationLocalizationResponse response = LocalizationResponseMapper.BuildLocalizationResponse(
            options.Value, localizerFactory, cultureName, context.Response);

        return TypedResults.Ok(response);
    }
}
