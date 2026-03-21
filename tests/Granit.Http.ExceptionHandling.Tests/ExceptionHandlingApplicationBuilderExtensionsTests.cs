// =============================================================================
// Tests - ExceptionHandlingApplicationBuilderExtensions
// =============================================================================
// Verifies that UseGranitExceptionHandling adds the exception handler middleware.
// =============================================================================

using Granit.Http.ExceptionHandling.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.ExceptionHandling.Tests;

public sealed class ExceptionHandlingApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseGranitExceptionHandling_ReturnsSameApplicationBuilder()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddGranitExceptionHandling();
        ServiceProvider sp = services.BuildServiceProvider();

        ApplicationBuilder appBuilder = new(sp);

        IApplicationBuilder returned = appBuilder.UseGranitExceptionHandling();

        returned.ShouldBeSameAs(appBuilder);
    }

    [Fact]
    public void UseGranitExceptionHandling_DoesNotThrow()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddGranitExceptionHandling();
        ServiceProvider sp = services.BuildServiceProvider();

        ApplicationBuilder appBuilder = new(sp);

        Should.NotThrow(() => appBuilder.UseGranitExceptionHandling());
    }
}
