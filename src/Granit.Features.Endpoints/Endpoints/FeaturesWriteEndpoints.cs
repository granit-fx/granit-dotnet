using Granit.Features.Definitions;
using Granit.Features.Endpoints.Dtos;
using Granit.Features.Endpoints.Internal;
using Granit.Features.Endpoints.Permissions;
using Granit.Features.Exceptions;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Features.Endpoints.Endpoints;

/// <summary>
/// Write Minimal API endpoints for tenant-level feature overrides.
/// </summary>
internal static class FeaturesWriteEndpoints
{
    /// <summary>Maps all feature override write endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapFeaturesWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/overrides/{name}", HandleSetOverrideAsync)
             .RequireAuthorization(FeaturesPermissions.Flags.Manage)
             .WithName("SetFeatureOverride")
             .WithSummary("Sets a tenant-level feature override.")
             .WithDescription("Creates or updates a tenant-level override for the specified feature. The value is validated against the feature's value type (Toggle: true/false, Numeric: min/max bounds, Selection: allowed values). Returns 404 if the feature is not declared. Requires the Features.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/overrides/{name}", HandleDeleteOverrideAsync)
             .RequireAuthorization(FeaturesPermissions.Flags.Manage)
             .WithName("DeleteFeatureOverride")
             .WithSummary("Deletes a tenant-level feature override.")
             .WithDescription("Removes the tenant-level override for the specified feature, reverting to the Plan → Default cascade. Returns 404 if the feature is not declared. Requires the Features.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleSetOverrideAsync(
        string name,
        SetFeatureOverrideRequest body,
        [FromServices] IFeatureDefinitionStore definitionStore,
        [FromServices] IFeatureStoreWriter storeWriter,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        FeatureDefinition? definition = definitionStore.GetOrNull(name);

        if (definition is null)
        {
            return FeaturesResponseMapper.FeatureNotFound();
        }

        try
        {
            FeaturesResponseMapper.ValidateValueType(definition, body.Value);
        }
        catch (FeatureValueValidationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id!.Value.ToString() : null;

        await storeWriter
            .SetAsync(name, tenantId, body.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeleteOverrideAsync(
        string name,
        [FromServices] IFeatureDefinitionStore definitionStore,
        [FromServices] IFeatureStoreWriter storeWriter,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (definitionStore.GetOrNull(name) is null)
        {
            return FeaturesResponseMapper.FeatureNotFound();
        }

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id!.Value.ToString() : null;

        await storeWriter
            .DeleteAsync(name, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
