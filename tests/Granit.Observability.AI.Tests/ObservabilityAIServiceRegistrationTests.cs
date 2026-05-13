using Granit.AI;
using Granit.MultiTenancy;
using Granit.Observability.AI.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace Granit.Observability.AI.Tests;

public sealed class ObservabilityAIServiceRegistrationTests
{
    [Fact]
    public void AddGranitObservabilityAI_should_pass_scope_validation_with_scoped_chat_client_factory()
    {
        // Regression: LlmLogAnalyzer captures IAIChatClientFactory (scoped). Registering
        // the analyzer as singleton breaks ValidateScopes (default in Development).
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
        });
        builder.Configuration.AddInMemoryCollection();
        builder.Services.AddLogging();
        builder.Services.AddMetrics();

        builder.Services.TryAddScoped(_ => Substitute.For<IAIChatClientFactory>());
        builder.Services.TryAddSingleton<ICurrentTenant>(new NullTenantContext());

        builder.AddGranitObservabilityAI();

        Should.NotThrow(() =>
        {
            using ServiceProvider provider = builder.Services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            using IServiceScope scope = provider.CreateScope();
            IAILogAnalyzer analyzer = scope.ServiceProvider.GetRequiredService<IAILogAnalyzer>();
            analyzer.ShouldNotBeNull();
        });
    }
}
