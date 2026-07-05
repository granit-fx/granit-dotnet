using Granit.Features.Definitions;
using Granit.Features.Endpoints.Dtos;
using Granit.Features.Endpoints.Internal;
using Granit.Features.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Features.Endpoints.Endpoints;

/// <summary>
/// Read-only Minimal API endpoints for feature definitions and resolved values.
/// </summary>
internal static class FeaturesReadEndpoints
{
    /// <summary>Maps all feature read endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapFeaturesReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/definitions", HandleGetDefinitions)
             .RequireAuthorization(FeaturesPermissions.Flags.Read)
             .WithName("GetFeatureDefinitions")
             .WithSummary("Returns all feature definitions grouped by name prefix.")
             .WithDescription("Returns all declared feature definitions, grouped by name prefix (the segment before the first dot in the feature name, e.g. 'Acme' for 'Acme.VideoConference'). Each group contains the features, their value types, defaults, and constraints. Requires the Features.Read permission.")
             .Produces<IReadOnlyList<FeatureGroupResponse>>();

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

        return group;
    }

    private static Ok<IReadOnlyList<FeatureGroupResponse>> HandleGetDefinitions(
        [FromServices] IFeatureDefinitionStore definitionStore)
    {
        IReadOnlyList<FeatureDefinition> all = definitionStore.GetAll();

        IReadOnlyList<FeatureGroupResponse> groups = all
            .GroupBy(d => FeaturesResponseMapper.ExtractGroupName(d.Name))
            .Select(g => new FeatureGroupResponse(
                g.Key,
                null,
                g.Select(FeaturesResponseMapper.MapDefinition).ToList()))
            .ToList();

        return TypedResults.Ok(groups);
    }

    private static async Task<Ok<IReadOnlyDictionary<string, string>>> HandleGetAllValuesAsync(
        [FromServices] IFeatureDefinitionStore definitionStore,
        [FromServices] IFeatureChecker featureChecker,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FeatureDefinition> definitions = definitionStore.GetAll();
        Dictionary<string, string> result = new(definitions.Count, StringComparer.Ordinal);

        foreach (string name in definitions.Select(d => d.Name))
        {
            string value = await featureChecker
                .GetValueAsync(name, cancellationToken)
                .ConfigureAwait(false);
            result[name] = value;
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
            return FeaturesResponseMapper.FeatureNotFound();
        }

        string value = await featureChecker
            .GetValueAsync(name, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new FeatureValueResponse(name, value));
    }
}
