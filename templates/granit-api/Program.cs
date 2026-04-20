using Granit.Bundle.Essentials;
using Granit.Extensions;
using Granit.Timing;
using GranitApi;
using Microsoft.AspNetCore.Http.HttpResults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

await builder.AddGranitAsync(granit => granit
    .AddEssentials()
    .AddModule<AppHostModule>());

WebApplication app = builder.Build();

await app.UseGranitAsync();

app.MapGet("/", Ok<GreetingResponse> (IClock clock) =>
        TypedResults.Ok(new GreetingResponse("Hello from Granit!", clock.Now)))
    .WithName("GetGreeting")
    .WithSummary("Returns a sample greeting with the current server time.")
    .WithDescription("Sample endpoint wired up by the granit-api template. Replace with your own endpoints — typically grouped under a MapGranitGroup for automatic validation.")
    .Produces<GreetingResponse>();

await app.RunAsync();
