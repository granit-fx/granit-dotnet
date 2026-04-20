// ---------------------------------------------------------------------------
// LocalizationEndpointRouteBuilderExtensions.cs
// Minimal API extensions for Granit localization:
//   - MapGranitLocalization: GET /{prefix}/localization (SPA bootstrapping, anonymous)
//   - MapGranitLocalizationOverrides: CRUD /{prefix}/localization/overrides
//     (admin, requires Localization.Overrides.Manage permission)
// ---------------------------------------------------------------------------

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Granit.Localization;
using Granit.Localization.Endpoints.Dtos;
using Granit.Localization.Endpoints.Options;
using Granit.Localization.Endpoints.Permissions;
using Granit.Localization.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Granit.Localization.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit localization endpoints.
/// </summary>
public static partial class LocalizationEndpointRouteBuilderExtensions
{
    // BCP 47 language tag: 2-8 alpha primary subtag, optional hyphen-separated subtags (1-8 alphanumeric).
    [GeneratedRegex(@"^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$")]
    private static partial Regex Bcp47Pattern();

    // Column size constraints (must match LocalizationOverrideConfiguration).
    private const int MaxResourceNameLength = 200;
    private const int MaxKeyLength = 500;
    private const int MaxValueLength = 4000;

    /// <summary>
    /// Maps <c>GET /{prefix}/localization</c> — returns all registered localization
    /// resources for the requested culture, plus the list of available languages.
    /// </summary>
    /// <remarks>
    /// <para>The endpoint is anonymous: translation strings are public UI data.</para>
    /// <para>
    /// Response headers include <c>Cache-Control: public, max-age=3600</c> and
    /// <c>Vary: Accept-Language</c> to allow browser and CDN caching per culture.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="LocalizationEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitLocalization(
        this IEndpointRouteBuilder endpoints,
        Action<LocalizationEndpointsOptions>? configure = null)
    {
        LocalizationEndpointsOptions options = new();
        configure?.Invoke(options);

        endpoints
            .MapGet(options.RoutePrefix, HandleGetLocalization)
            .AllowAnonymous()
            .WithName("GetGranitLocalization")
            .WithTags(options.TagName)
            .WithSummary("Returns all localization resources for the requested culture.")
            .WithDescription("Returns all localization resources (key-value pairs) for the requested culture, grouped by resource name. Accepts an optional cultureName query parameter (BCP 47 format); defaults to the Accept-Language header culture. Also returns the list of supported languages. Response is cached for 1 hour (Cache-Control: public, max-age=3600, Vary: Accept-Language). Anonymous — no authentication required.")
            .Produces<ApplicationLocalizationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AddOpenApiOperationTransformer(DescribeCultureNameParam);

        return endpoints;
    }

    private static Task MarkOverridesQueryParamsRequired(
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
            if (parameter.In != ParameterLocation.Query)
            {
                continue;
            }

            if (parameter is OpenApiParameter concrete && parameter.Name is "resourceName" or "cultureName")
            {
                concrete.Required = true;
            }

            if (parameter.Name == "cultureName" && parameter.Schema is OpenApiSchema cultureSchema)
            {
                cultureSchema.Pattern ??= "^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$";
                cultureSchema.Example ??= System.Text.Json.Nodes.JsonValue.Create("fr-BE");
            }
        }

        return Task.CompletedTask;
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

        foreach (IOpenApiParameter parameter in operation.Parameters)
        {
            if (parameter.Name == "cultureName"
                && parameter.In == ParameterLocation.Query
                && parameter.Schema is OpenApiSchema schema)
            {
                parameter.Description ??= "Optional BCP 47 culture tag (e.g. 'fr', 'fr-BE', 'zh-Hant-TW'). When omitted, the Accept-Language header is used.";
                schema.Pattern ??= "^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$";
                schema.Example ??= System.Text.Json.Nodes.JsonValue.Create("fr-BE");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps the localization override management endpoints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers 3 endpoints under <c>/{prefix}/localization/overrides</c>:
    /// <list type="bullet">
    /// <item><c>GET ?resourceName=X&amp;cultureName=fr</c> — list all overrides for a resource/culture</item>
    /// <item><c>PUT /{resourceName}/{cultureName}/{key}</c> — create or update an override</item>
    /// <item><c>DELETE /{resourceName}/{cultureName}/{key}</c> — remove an override</item>
    /// </list>
    /// </para>
    /// <para>
    /// All endpoints require the <c>Localization.Overrides.Manage</c> permission.
    /// If <see cref="ILocalizationOverrideStoreReader"/>/<see cref="ILocalizationOverrideStoreWriter"/> is not registered (no EF Core or other
    /// persistence module loaded), all endpoints return <c>501 Not Implemented</c>.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="LocalizationEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitLocalizationOverrides(
        this IEndpointRouteBuilder endpoints,
        Action<LocalizationEndpointsOptions>? configure = null)
    {
        LocalizationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup($"{options.RoutePrefix}/overrides")
            .RequireAuthorization(LocalizationOverridesPermissions.Overrides.Manage)
            .WithTags(options.TagName);

        group.MapGet("", HandleGetOverridesAsync)
             .WithName("GetLocalizationOverrides")
             .WithSummary("Returns all translation overrides for a resource and culture.")
             .WithDescription("Returns all active translation overrides for the specified resource and culture as a key-value dictionary. Both resourceName and cultureName query parameters are required. Returns 501 if no override store is registered.")
             .Produces<IReadOnlyDictionary<string, string>>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented)
             .AddOpenApiOperationTransformer(MarkOverridesQueryParamsRequired);

        group.MapPut("/{resourceName}/{cultureName}/{key}", HandlePutOverrideAsync)
             .WithName("PutLocalizationOverride")
             .WithSummary("Creates or updates a translation override.")
             .WithDescription("Sets a translation override for a specific resource, culture, and key. If an override already exists, it is replaced. The culture name must be a valid BCP 47 tag. Returns 501 if no override store is registered.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapDelete("/{resourceName}/{cultureName}/{key}", HandleDeleteOverrideAsync)
             .WithName("DeleteLocalizationOverride")
             .WithSummary("Removes a translation override.")
             .WithDescription("Removes the translation override for the specified resource, culture, and key. The original value from the resource file becomes effective again. No-op if the override does not exist. Returns 501 if no override store is registered.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers — GET /{prefix}/localization
    // -------------------------------------------------------------------------

    private static Results<Ok<ApplicationLocalizationResponse>, ProblemHttpResult> HandleGetLocalization(
        HttpContext context,
        string? cultureName = null)
    {
        IOptions<GranitLocalizationOptions> options =
            context.RequestServices.GetRequiredService<IOptions<GranitLocalizationOptions>>();
        IStringLocalizerFactory localizerFactory =
            context.RequestServices.GetRequiredService<IStringLocalizerFactory>();

        if (!string.IsNullOrWhiteSpace(cultureName) && !Bcp47Pattern().IsMatch(cultureName))
        {
            return TypedResults.Problem(
                detail: $"Culture name '{cultureName}' is not supported.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        CultureInfo culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.CurrentUICulture
            : CultureInfo.GetCultureInfo(cultureName);

        CultureInfo previousCulture = CultureInfo.CurrentUICulture;
        Dictionary<string, IReadOnlyDictionary<string, string>> resources;

        try
        {
            CultureInfo.CurrentUICulture = culture;
            resources = BuildResources(options.Value, localizerFactory);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }

        List<LanguageInfoResponse> languages = [.. options.Value.Languages
            .Select(l => new LanguageInfoResponse(l.CultureName, l.DisplayName, l.FlagIcon, l.IsDefault))];

        context.Response.Headers.CacheControl = "public, max-age=3600";
        context.Response.Headers.Vary = "Accept-Language";

        return TypedResults.Ok(new ApplicationLocalizationResponse(culture.Name, resources, languages));
    }

    // -------------------------------------------------------------------------
    // Handlers — CRUD /{prefix}/localization/overrides
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<IReadOnlyDictionary<string, string>>, ProblemHttpResult>> HandleGetOverridesAsync(
        HttpContext context,
        string? resourceName,
        string? cultureName,
        CancellationToken cancellationToken)
    {
        ILocalizationOverrideStoreReader? storeReader =
            context.RequestServices.GetService<ILocalizationOverrideStoreReader>();

        if (storeReader is null)
        {
            return StoreNotRegistered();
        }

        if (string.IsNullOrWhiteSpace(resourceName) || string.IsNullOrWhiteSpace(cultureName))
        {
            return TypedResults.Problem(
                detail: "Query parameters 'resourceName' and 'cultureName' are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ProblemHttpResult? error = ValidateMaxLength(resourceName, nameof(resourceName), MaxResourceNameLength)
            ?? ValidateBcp47(cultureName);

        if (error is not null)
        {
            return error;
        }

        IReadOnlyDictionary<string, string> overrides =
            await storeReader.GetOverridesAsync(resourceName, cultureName, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(overrides);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandlePutOverrideAsync(
        HttpContext context,
        string resourceName,
        string cultureName,
        string key,
        SetLocalizationOverrideRequest body,
        CancellationToken cancellationToken)
    {
        ILocalizationOverrideStoreWriter? storeWriter =
            context.RequestServices.GetService<ILocalizationOverrideStoreWriter>();

        if (storeWriter is null)
        {
            return StoreNotRegistered();
        }

        ProblemHttpResult? error = ValidateMaxLength(resourceName, nameof(resourceName), MaxResourceNameLength)
            ?? ValidateBcp47(cultureName)
            ?? ValidateMaxLength(key, nameof(key), MaxKeyLength);

        if (error is not null)
        {
            return error;
        }

        if (string.IsNullOrWhiteSpace(body.Value))
        {
            return TypedResults.Problem(
                detail: "Override value must not be empty.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body.Value.Length > MaxValueLength)
        {
            return TypedResults.Problem(
                detail: $"value must not exceed {MaxValueLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await storeWriter.SetOverrideAsync(resourceName, cultureName, key, body.Value, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeleteOverrideAsync(
        HttpContext context,
        string resourceName,
        string cultureName,
        string key,
        CancellationToken cancellationToken)
    {
        ILocalizationOverrideStoreWriter? storeWriter =
            context.RequestServices.GetService<ILocalizationOverrideStoreWriter>();

        if (storeWriter is null)
        {
            return StoreNotRegistered();
        }

        ProblemHttpResult? error = ValidateMaxLength(resourceName, nameof(resourceName), MaxResourceNameLength)
            ?? ValidateBcp47(cultureName)
            ?? ValidateMaxLength(key, nameof(key), MaxKeyLength);

        if (error is not null)
        {
            return error;
        }

        await storeWriter.RemoveOverrideAsync(resourceName, cultureName, key, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // Shared validation helpers
    // -------------------------------------------------------------------------

    private static ProblemHttpResult StoreNotRegistered() =>
        TypedResults.Problem(
            detail: "No localization override store is registered. Add GranitLocalizationDatabaseSourceEntityFrameworkCoreModule.",
            statusCode: StatusCodes.Status501NotImplemented);

    private static ProblemHttpResult? ValidateBcp47(string cultureName) =>
        Bcp47Pattern().IsMatch(cultureName)
            ? null
            : TypedResults.Problem(
                detail: $"Culture name '{cultureName}' is not a valid BCP 47 tag.",
                statusCode: StatusCodes.Status400BadRequest);

    private static ProblemHttpResult? ValidateMaxLength(string value, string paramName, int maxLength) =>
        value.Length <= maxLength
            ? null
            : TypedResults.Problem(
                detail: $"{paramName} must not exceed {maxLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static Dictionary<string, IReadOnlyDictionary<string, string>> BuildResources(
        GranitLocalizationOptions options,
        IStringLocalizerFactory localizerFactory)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> resources = new(StringComparer.Ordinal);

        foreach (Type resourceType in options.Resources.GetAll().Select(resourceInfo => resourceInfo.ResourceType))
        {
            string name = resourceType
                .GetCustomAttribute<LocalizationResourceNameAttribute>()?.Name
                ?? resourceType.Name;

            IStringLocalizer localizer = localizerFactory.Create(resourceType);

            var translations = localizer
                .GetAllStrings(includeParentCultures: true)
                .ToDictionary(s => s.Name, s => s.Value, StringComparer.Ordinal);

            resources[name] = translations;
        }

        return resources;
    }
}
