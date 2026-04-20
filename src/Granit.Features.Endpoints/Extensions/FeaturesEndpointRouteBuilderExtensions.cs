using Granit.Features.Definitions;
using Granit.Features.Endpoints.Dtos;
using Granit.Features.Endpoints.Options;
using Granit.Features.Endpoints.Permissions;
using Granit.Features.Exceptions;
using Granit.Features.ValueTypes;
using Granit.MultiTenancy;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Features.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping feature management endpoints.
/// </summary>
public static class FeaturesEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps feature management endpoints under <c>/{prefix}/features</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="FeaturesEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitFeatures(
        this IEndpointRouteBuilder endpoints,
        Action<FeaturesEndpointsOptions>? configure = null)
    {
        FeaturesEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        MapDefinitionEndpoints(group);
        MapValueEndpoints(group);
        MapOverrideEndpoints(group);

        return group;
    }

    // -------------------------------------------------------------------------
    // Definitions (read-only)
    // -------------------------------------------------------------------------

    private static void MapDefinitionEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/definitions", HandleGetDefinitionsAsync)
             .RequireAuthorization(FeaturesPermissions.Flags.Read)
             .WithName("GetFeatureDefinitions")
             .WithSummary("Returns all feature definitions grouped by name prefix.")
             .WithDescription("Returns all declared feature definitions, grouped by name prefix (the segment before the first dot in the feature name, e.g. 'Acme' for 'Acme.VideoConference'). Each group contains the features, their value types, defaults, and constraints. Requires the Features.Read permission.")
             .Produces<IReadOnlyList<FeatureGroupResponse>>();
    }

    // -------------------------------------------------------------------------
    // Values (resolved for current context)
    // -------------------------------------------------------------------------

    private static void MapValueEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/values", HandleGetAllValuesAsync)
             .WithName("GetAllFeatureValues")
             .WithSummary("Returns all resolved feature values for the current context.")
             .WithDescription("Returns a dictionary of all feature values resolved through the Tenant → Plan → Default cascade for the current request context. Any authenticated user can read the features that apply to them.")
             .Produces<IReadOnlyDictionary<string, string>>();

        group.MapGet("/values/{name}", HandleGetValueAsync)
             .WithName("GetFeatureValue")
             .WithSummary("Returns the resolved value for a single feature.")
             .WithDescription("Returns the resolved value for the specified feature in the current context. Returns 404 if the feature is not declared in any definition provider.")
             .Produces<FeatureValueResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // -------------------------------------------------------------------------
    // Overrides (tenant CRUD)
    // -------------------------------------------------------------------------

    private static void MapOverrideEndpoints(RouteGroupBuilder group)
    {
        group.MapPut("/overrides/{name}", HandleSetOverrideAsync)
             .RequireAuthorization(FeaturesPermissions.Flags.Manage)
             .WithName("SetFeatureOverride")
             .WithSummary("Sets a tenant-level feature override.")
             .WithDescription("Creates or updates a tenant-level override for the specified feature. The value is validated against the feature's value type (Toggle: true/false, Numeric: min/max bounds, Selection: allowed values). Returns 404 if the feature is not declared. Requires the Features.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/overrides/{name}", HandleDeleteOverrideAsync)
             .RequireAuthorization(FeaturesPermissions.Flags.Manage)
             .WithName("DeleteFeatureOverride")
             .WithSummary("Deletes a tenant-level feature override.")
             .WithDescription("Removes the tenant-level override for the specified feature, reverting to the Plan → Default cascade. Returns 404 if the feature is not declared. Requires the Features.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // -------------------------------------------------------------------------
    // Definition handlers
    // -------------------------------------------------------------------------

    private static Ok<IReadOnlyList<FeatureGroupResponse>> HandleGetDefinitionsAsync(
        [FromServices] IFeatureDefinitionStore definitionStore)
    {
        IReadOnlyList<FeatureDefinition> all = definitionStore.GetAll();

        IReadOnlyList<FeatureGroupResponse> groups = all
            .GroupBy(d => ExtractGroupName(d.Name))
            .Select(g => new FeatureGroupResponse(
                g.Key,
                null,
                g.Select(MapDefinition).ToList()))
            .ToList();

        return TypedResults.Ok(groups);
    }

    // -------------------------------------------------------------------------
    // Value handlers
    // -------------------------------------------------------------------------

    private static async Task<Ok<IReadOnlyDictionary<string, string>>> HandleGetAllValuesAsync(
        [FromServices] IFeatureDefinitionStore definitionStore,
        [FromServices] IFeatureChecker featureChecker,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FeatureDefinition> definitions = definitionStore.GetAll();
        Dictionary<string, string> result = new(definitions.Count, StringComparer.Ordinal);

        foreach (FeatureDefinition definition in definitions)
        {
            string value = await featureChecker
                .GetValueAsync(definition.Name, cancellationToken)
                .ConfigureAwait(false);
            result[definition.Name] = value;
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string>>(result);
    }

    private static async Task<Results<Ok<FeatureValueResponse>, ProblemHttpResult>> HandleGetValueAsync(
        string name,
        [FromServices] IFeatureDefinitionStore definitionStore,
        [FromServices] IFeatureChecker featureChecker,
        CancellationToken cancellationToken)
    {
        if (definitionStore.GetOrNull(name) is null)
        {
            return FeatureNotFound(name);
        }

        string value = await featureChecker
            .GetValueAsync(name, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new FeatureValueResponse(name, value));
    }

    // -------------------------------------------------------------------------
    // Override handlers
    // -------------------------------------------------------------------------

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
            return FeatureNotFound(name);
        }

        try
        {
            ValidateValueType(definition, body.Value);
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
            return FeatureNotFound(name);
        }

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id!.Value.ToString() : null;

        await storeWriter
            .DeleteAsync(name, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    internal static void ValidateValueType(FeatureDefinition definition, string value)
    {
        switch (definition.ValueType)
        {
            case FeatureValueType.Toggle:
                if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FeatureValueValidationException(
                        definition.Name,
                        value,
                        "value must be 'true' or 'false'.");
                }

                break;

            case FeatureValueType.Numeric:
                definition.NumericConstraint?.Validate(definition.Name, value);

                if (definition.NumericConstraint is null && !long.TryParse(value, out _))
                {
                    throw new FeatureValueValidationException(
                        definition.Name,
                        value,
                        "value must be a valid integer.");
                }

                break;

            case FeatureValueType.Selection:
                definition.SelectionValues?.Validate(definition.Name, value);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static ProblemHttpResult FeatureNotFound(string name) =>
        TypedResults.Problem(
            detail: "The requested feature is not declared in any definition provider.",
            statusCode: StatusCodes.Status404NotFound);

    private static FeatureDefinitionResponse MapDefinition(FeatureDefinition definition) =>
        new(
            definition.Name,
            definition.DefaultValue,
            definition.ValueType.ToString(),
            definition.NumericConstraint is { } nc
                ? new FeatureNumericConstraintResponse(nc.Min, nc.Max)
                : null,
            definition.SelectionValues?.AllowedValues,
            definition.DisplayName,
            definition.Description);

    /// <summary>
    /// Extracts the group name from a feature name following the <c>"Group.Feature"</c>
    /// naming convention. Returns the full name if no dot is present.
    /// </summary>
    private static string ExtractGroupName(string featureName)
    {
        int dotIndex = featureName.IndexOf('.');
        return dotIndex > 0 ? featureName[..dotIndex] : featureName;
    }
}
