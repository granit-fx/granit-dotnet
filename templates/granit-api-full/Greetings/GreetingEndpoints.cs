using Granit.Timing;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GranitApiFull.Greetings;

/// <summary>Sample endpoints illustrating the Granit route-group pattern with auto-validation.</summary>
public static class GreetingEndpoints
{
    /// <summary>Maps the sample greeting endpoints under <c>api/greetings</c>.</summary>
    public static IEndpointRouteBuilder MapGreetingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGranitGroup("api/greetings");

        group.MapGet("/", Ok<GreetingResponse> (IClock clock) =>
                TypedResults.Ok(new GreetingResponse("Hello from Granit!", clock.Now)))
            .WithName("GetGreeting")
            .WithSummary("Returns a sample greeting with the current server time.")
            .WithDescription("Sample read endpoint. Replace with your own queries — typically one per use case.")
            .Produces<GreetingResponse>();

        group.MapPost("/", Ok<GreetingResponse> (CreateGreetingRequest request, IClock clock) =>
                TypedResults.Ok(new GreetingResponse($"Hello, {request.Name}!", clock.Now)))
            .WithName("CreateGreeting")
            .WithSummary("Produces a personalized greeting.")
            .WithDescription("Sample write endpoint. The request body is auto-validated by MapGranitGroup against CreateGreetingRequestValidator.")
            .Produces<GreetingResponse>()
            .ProducesValidationProblem();

        return endpoints;
    }
}
