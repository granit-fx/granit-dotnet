using Granit.Localization.Endpoints.Dtos;
using Granit.Localization.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Localization.Endpoints.Endpoints;

/// <summary>
/// Write Minimal API endpoints for localization override management (PUT / DELETE).
/// </summary>
internal static class LocalizationWriteEndpoints
{
    // Column size constraints (must match LocalizationOverrideConfiguration).
    private const int MaxResourceNameLength = 200;
    private const int MaxKeyLength = 500;
    private const int MaxValueLength = 4000;

    /// <summary>Maps the PUT and DELETE override endpoints to the given route group.</summary>
    internal static RouteGroupBuilder MapLocalizationWriteEndpoints(this RouteGroupBuilder group)
    {
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
            return LocalizationResponseMapper.StoreNotRegistered();
        }

        ProblemHttpResult? error = LocalizationResponseMapper.ValidateMaxLength(resourceName, nameof(resourceName), MaxResourceNameLength)
            ?? LocalizationResponseMapper.ValidateBcp47(cultureName)
            ?? LocalizationResponseMapper.ValidateMaxLength(key, nameof(key), MaxKeyLength);

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
            return LocalizationResponseMapper.StoreNotRegistered();
        }

        ProblemHttpResult? error = LocalizationResponseMapper.ValidateMaxLength(resourceName, nameof(resourceName), MaxResourceNameLength)
            ?? LocalizationResponseMapper.ValidateBcp47(cultureName)
            ?? LocalizationResponseMapper.ValidateMaxLength(key, nameof(key), MaxKeyLength);

        if (error is not null)
        {
            return error;
        }

        await storeWriter.RemoveOverrideAsync(resourceName, cultureName, key, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
