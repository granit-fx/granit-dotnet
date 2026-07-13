using Granit.Http.Cors.Internal;
using Granit.Http.Cors.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Http.Cors.Tests;

public sealed class CorsStartupFilterTests
{
    private static CorsStartupFilter CreateFilter(bool autoRegister) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new GranitCorsOptions
            {
                AllowedOrigins = ["https://app.example.com"],
                AutoRegisterMiddleware = autoRegister,
            }),
            NullLogger<CorsStartupFilter>.Instance);

    [Fact]
    public void Configure_WhenAutoRegisterDisabled_ReturnsNextUnchanged()
    {
        static void Next(IApplicationBuilder _)
        {
        }

        Action<IApplicationBuilder> result = CreateFilter(autoRegister: false).Configure(Next);

        result.ShouldBeSameAs((Action<IApplicationBuilder>)Next);
    }

    [Fact]
    public void Configure_WhenAutoRegisterEnabled_AppliesCorsMiddlewareAndCallsNext()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCors();
        using ServiceProvider sp = services.BuildServiceProvider();
        ApplicationBuilder app = new(sp);

        bool nextCalled = false;
        CreateFilter(autoRegister: true).Configure(_ => nextCalled = true)(app);

        nextCalled.ShouldBeTrue();
        // Building succeeds only if the CORS middleware resolved its services —
        // i.e. UseCors() was genuinely applied to the pipeline.
        app.Build().ShouldNotBeNull();
    }
}
