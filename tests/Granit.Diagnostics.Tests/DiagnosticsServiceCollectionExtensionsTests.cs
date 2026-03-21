// =============================================================================
// Tests - DiagnosticsServiceCollectionExtensions
// =============================================================================
// Vérifie les deux branches de AddGranitDiagnostics :
//   - Sans configure → enregistrement HealthChecks sans options custom
//   - Avec configure != null → options personnalisées enregistrées
// =============================================================================

using Granit.Diagnostics.Extensions;
using Granit.Diagnostics.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using HealthCheckService = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService;

namespace Granit.Diagnostics.Tests;

public sealed class DiagnosticsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitDiagnostics_WithoutConfigure_RegistersHealthChecks()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDiagnostics();

        // Assert — AddHealthChecks was called: HealthCheckService is registered
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(HealthCheckService));
        descriptor.ShouldNotBeNull("AddHealthChecks must register HealthCheckService");
    }

    [Fact]
    public void AddGranitDiagnostics_WithConfigure_AppliesCustomOptions()
    {
        // Arrange
        ServiceCollection services = new();

        // Act — pass a configure action (the non-null branch)
        services.AddGranitDiagnostics(opts =>
        {
            opts.LivenessPath = "/ping";
            opts.DefaultCacheDuration = TimeSpan.FromSeconds(30);
        });

        // Assert — DiagnosticsOptions reflects the customization
        using ServiceProvider sp = services.BuildServiceProvider();
        DiagnosticsOptions diagnosticsOptions = sp.GetRequiredService<IOptions<DiagnosticsOptions>>().Value;
        diagnosticsOptions.LivenessPath.ShouldBe("/ping");
        diagnosticsOptions.DefaultCacheDuration.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AddGranitDiagnostics_ReturnsServices_ForChaining()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection returned = services.AddGranitDiagnostics();

        // Assert
        returned.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitDiagnostics_RegistersHealthCheckAggregator_AsSingleton()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDiagnostics();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(Granit.Diagnostics.Abstractions.IHealthCheckAggregator));
        descriptor.ShouldNotBeNull("AddGranitDiagnostics must register IHealthCheckAggregator");
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitDiagnostics_DoesNotReplaceExistingAggregator()
    {
        // Arrange — register a custom aggregator first
        ServiceCollection services = new();
        Granit.Diagnostics.Abstractions.IHealthCheckAggregator custom =
            NSubstitute.Substitute.For<Granit.Diagnostics.Abstractions.IHealthCheckAggregator>();
        services.AddSingleton(custom);

        // Act
        services.AddGranitDiagnostics();

        // Assert — TryAddSingleton should not overwrite
        using ServiceProvider sp = services.BuildServiceProvider();
        Granit.Diagnostics.Abstractions.IHealthCheckAggregator resolved =
            sp.GetRequiredService<Granit.Diagnostics.Abstractions.IHealthCheckAggregator>();
        resolved.ShouldBeSameAs(custom);
    }

    [Fact]
    public void AddGranitDiagnostics_WithNullConfigure_DoesNotRegisterOptions()
    {
        // Arrange
        ServiceCollection services = new();

        // Act — pass null explicitly
        services.AddGranitDiagnostics(configure: null);

        // Assert — no IConfigureOptions<DiagnosticsOptions> registered
        ServiceDescriptor? configureDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<DiagnosticsOptions>));
        configureDescriptor.ShouldBeNull("Null configure should not register options configuration");
    }
}
