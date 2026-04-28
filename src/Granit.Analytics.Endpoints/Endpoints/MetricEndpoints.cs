using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Endpoints.Internal;
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
            .WithName("EvaluateGranitMetric")
            .WithSummary("Evaluates a registered Granit.Analytics MetricDefinition.")
            .WithDescription(
                "Resolves the metric by name, applies the period (and optional comparison window) "
                + "through the same filter pipeline grids use, runs the aggregation through the EF Core "
                + "executor, caches the result via FusionCache (key includes tenant id), and returns the "
                + "{ snapshot, sequence, emittedAt, refreshHint } envelope. Returns 404 when the metric "
                + "name is unknown, 400 when the period spec is invalid or comparison is requested on a "
                + "metric without a PeriodSelector.")
            .Produces<MetricResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return group;
    }

    private static async Task<Results<Ok<MetricResponse>, ProblemHttpResult>> EvaluateAsync(
        [FromRoute] string name,
        [FromBody] MetricRequest request,
        [FromServices] MetricEndpointService service,
        CancellationToken cancellationToken)
    {
        if (!service.TryGetRunner(name, out IMetricRunner runner))
        {
            return TypedResults.Problem(
                detail: $"Metric '{name}' is not registered.",
                statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            MetricResponse response = await service.EvaluateAsync(runner, request, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
