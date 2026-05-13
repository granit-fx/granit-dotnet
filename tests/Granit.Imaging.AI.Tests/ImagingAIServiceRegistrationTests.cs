using Granit.AI;
using Granit.Imaging.AI.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class ImagingAIServiceRegistrationTests
{
    [Fact]
    public void AddGranitImagingAI_should_pass_scope_validation_with_scoped_chat_client_factory()
    {
        // Regression: LlmImageAnalyzer captures IAIChatClientFactory (scoped). Registering
        // the analyzer as singleton breaks ValidateScopes (default in Development).
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
        });
        builder.Configuration.AddInMemoryCollection();
        builder.Services.AddLogging();
        builder.Services.AddMetrics();

        // Minimal stand-in for Granit.AI: register the scoped factory contract used by the analyzer.
        builder.Services.TryAddScoped(_ => Substitute.For<IAIChatClientFactory>());

        builder.AddGranitImagingAI();

        Should.NotThrow(() =>
        {
            using ServiceProvider provider = builder.Services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            using IServiceScope scope = provider.CreateScope();
            IAIImageAnalyzer analyzer = scope.ServiceProvider.GetRequiredService<IAIImageAnalyzer>();
            analyzer.ShouldNotBeNull();
        });
    }
}
