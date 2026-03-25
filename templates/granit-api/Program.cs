using Granit.Bundle.Essentials;
using Granit.Extensions;
using Granit.Timing;
using GranitApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

await builder.AddGranitAsync(granit => granit
    .AddEssentials()
    .AddModule<AppHostModule>());

WebApplication app = builder.Build();

await app.UseGranitAsync();

app.MapGet("/", (IClock clock) => new
{
    Message = "Hello from Granit!",
    Time = clock.Now,
});

await app.RunAsync();
