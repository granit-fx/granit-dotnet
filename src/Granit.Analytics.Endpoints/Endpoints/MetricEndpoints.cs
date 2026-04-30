using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Analytics.Endpoints.Endpoints;

internal static class MetricEndpoints
{
    internal static RouteGroupBuilder MapMetricEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/metrics/{name}", EvaluateAsync)
            .WithName("EvaluateMetric")
            .WithSummary("Evaluates a registered Granit.Analytics MetricDefinition.")
            .WithDescription(
                "Resolves the metric by name, applies the period (and optional comparison window) "
                + "through the same filter pipeline grids use, runs the aggregation through the EF Core "
                + "executor, caches the result via FusionCache (key includes tenant id), and returns the "
                + "{ snapshot, sequence, emittedAt, refreshHint } envelope. Returns 404 when the metric "
                + "name is unknown, 422 when the period spec is invalid or comparison is requested on a "
                + "metric without a PeriodSelector.")
            .Produces<MetricResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    private static async Task<Ok<MetricResponse>> EvaluateAsync(
        [FromRoute] string name,
        [FromBody] MetricRequest request,
        [FromServices] MetricEndpointService service,
        CancellationToken cancellationToken)
    {
        if (!service.TryGetRunner(name, out IMetricRunner runner))
        {
            throw new EntityNotFoundException(typeof(MetricDefinition<,>), name);
        }

        MetricResponse response = await service.EvaluateAsync(runner, request, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(response);
    }
}
