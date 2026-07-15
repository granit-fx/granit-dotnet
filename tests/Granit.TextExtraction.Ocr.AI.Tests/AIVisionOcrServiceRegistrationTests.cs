using Granit.AI;
using Granit.TextExtraction.Ocr.AI.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Ocr.AI.Tests;

public sealed class AIVisionOcrServiceRegistrationTests
{
    [Fact]
    public void AddAIVisionOcrExtractor_passes_scope_validation_with_scoped_chat_client_factory()
    {
        // Regression: the extractor is registered as a singleton by the pipeline while
        // IAIChatClientFactory is scoped. Capturing the factory in the constructor was a
        // captive dependency that crashed ValidateScopes (the default in Development);
        // the extractor now resolves it through a per-call scope.
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddMetrics();
        services.AddScoped(_ => Substitute.For<IAIChatClientFactory>());

        services.AddAIVisionOcrExtractor();

        Should.NotThrow(() =>
        {
            using ServiceProvider provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            provider.GetRequiredService<ITextExtractionPipeline>().ShouldNotBeNull();
            provider.GetServices<ITextExtractor>()
                .OfType<AIVisionOcrExtractor>()
                .ShouldHaveSingleItem();
        });
    }
}
