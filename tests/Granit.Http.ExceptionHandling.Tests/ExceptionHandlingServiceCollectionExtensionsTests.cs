// =============================================================================
// Tests - ExceptionHandlingServiceCollectionExtensions
// =============================================================================
// Verifies the DI registration:
//   - IProblemDetailsService is registered
//   - IExceptionHandler (GranitExceptionHandler) is registered
//   - IExceptionStatusCodeMapper (DefaultExceptionStatusCodeMapper) is registered
//   - ExceptionHandlingOptions is configurable
//   - Custom mapper chain of responsibility
//   - Fluent API returns the same IServiceCollection
// =============================================================================

using Granit.Http.ExceptionHandling.Extensions;
using Granit.Http.ExceptionHandling.Internal;
using Granit.Http.ExceptionHandling.Options;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ExceptionHandling.Tests;

public sealed class ExceptionHandlingServiceCollectionExtensionsTests
{
    // -------------------------------------------------------------------------
    // Service registration
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitExceptionHandling_RegistersIProblemDetailsService()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitExceptionHandling();

        using ServiceProvider sp = services.BuildServiceProvider();

        sp.GetService<IProblemDetailsService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitExceptionHandling_RegistersIExceptionHandler()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitExceptionHandling();

        using ServiceProvider sp = services.BuildServiceProvider();

        IExceptionHandler? handler = sp.GetService<IExceptionHandler>();
        handler.ShouldNotBeNull();
        handler.ShouldBeOfType<GranitExceptionHandler>();
    }

    [Fact]
    public void AddGranitExceptionHandling_RegistersDefaultExceptionStatusCodeMapper()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitExceptionHandling();

        using ServiceProvider sp = services.BuildServiceProvider();

        IExceptionStatusCodeMapper mapper = sp.GetRequiredService<IExceptionStatusCodeMapper>();
        mapper.ShouldBeOfType<DefaultExceptionStatusCodeMapper>();
    }

    // -------------------------------------------------------------------------
    // Options configuration
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitExceptionHandling_DefaultOptions_ExposeInternalErrorDetailsIsFalse()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitExceptionHandling();

        using ServiceProvider sp = services.BuildServiceProvider();

        ExceptionHandlingOptions opts = sp.GetRequiredService<IOptions<ExceptionHandlingOptions>>().Value;
        opts.ExposeInternalErrorDetails.ShouldBeFalse("internal error details must never be exposed by default (ISO 27001 production rule)");
    }

    [Fact]
    public void AddGranitExceptionHandling_WithConfigure_SetsOptions()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitExceptionHandling(opts => opts.ExposeInternalErrorDetails = true);

        using ServiceProvider sp = services.BuildServiceProvider();

        ExceptionHandlingOptions opts = sp.GetRequiredService<IOptions<ExceptionHandlingOptions>>().Value;
        opts.ExposeInternalErrorDetails.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitExceptionHandling_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitExceptionHandling(configure: null);

        using ServiceProvider sp = services.BuildServiceProvider();

        ExceptionHandlingOptions opts = sp.GetRequiredService<IOptions<ExceptionHandlingOptions>>().Value;
        opts.ExposeInternalErrorDetails.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Fluent API
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitExceptionHandling_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        IServiceCollection returned = services.AddGranitExceptionHandling();

        returned.ShouldBeSameAs(services);
    }

    // -------------------------------------------------------------------------
    // Custom mapper registration (chain of responsibility)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitExceptionHandling_CustomMapper_RegisteredAlongDefault_BothResolvable()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitExceptionHandling();
        services.AddSingleton<IExceptionStatusCodeMapper, CustomPriorityMapper>();

        using ServiceProvider sp = services.BuildServiceProvider();

        IEnumerable<IExceptionStatusCodeMapper> mappers = sp.GetServices<IExceptionStatusCodeMapper>();
        mappers.Count().ShouldBe(2);
    }

    private sealed class CustomPriorityMapper : IExceptionStatusCodeMapper
    {
        public int? TryGetStatusCode(Exception exception) =>
            exception is ArgumentException ? StatusCodes.Status400BadRequest : null;
    }
}
