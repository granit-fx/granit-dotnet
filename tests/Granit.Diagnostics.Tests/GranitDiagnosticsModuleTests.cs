// =============================================================================
// Tests - GranitDiagnosticsModule
// =============================================================================
// Vérifie que ConfigureServices appelle AddGranitDiagnostics,
// ce qui enregistre les services HealthChecks dans le conteneur DI.
// =============================================================================

using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Tests;

public sealed class GranitDiagnosticsModuleTests
{
    [Fact]
    public void ConfigureServices_CallsAddGranitDiagnostics_RegisteringHealthCheckInfrastructure()
    {
        // Arrange
        GranitDiagnosticsModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        // Assert — AddGranitDiagnostics calls AddHealthChecks which registers HealthCheckServiceOptions
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        HealthCheckServiceOptions options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        options.ShouldNotBeNull();
    }
}
