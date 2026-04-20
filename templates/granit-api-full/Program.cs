using Granit.Bundle.Api;
using Granit.Bundle.Notifications;
using Granit.Extensions;
using GranitApiFull;
using GranitApiFull.Greetings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

await builder.AddGranitAsync(granit => granit
    .AddApi()
    .AddNotifications()
    .AddModule<AppHostModule>());

WebApplication app = builder.Build();

await app.UseGranitAsync();

app.MapGreetingEndpoints();

await app.RunAsync();
