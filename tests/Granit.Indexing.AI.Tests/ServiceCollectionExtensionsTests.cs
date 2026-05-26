using Granit.Indexing.AI.Extensions;
using Granit.Indexing.AI.Internal;
using Granit.Indexing.AI.Prompts;
using Granit.Indexing.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Indexing.AI.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitIndexingAISummarizer_registers_AISummarizer_as_ISummarizer()
    {
        ServiceCollection services = BuildBaseServices();

        services.AddGranitIndexingAISummarizer();

        ServiceDescriptor[] summarizerDescriptors =
        [.. services.Where(d => d.ServiceType == typeof(ISummarizer)
                                && d.ImplementationType == typeof(AISummarizer))];

        summarizerDescriptors.Length.ShouldBe(1);
    }

    [Fact]
    public void AddGranitIndexingAISummarizer_uses_TryAdd_so_pre_registered_ISummarizer_wins()
    {
        // Hosts that ship a custom summarizer (e.g. domain-specific extractive model) want
        // it to remain the registered impl even after the AI module wires itself in. The
        // extension uses TryAddScoped to honour that — pinned by this test so a future
        // refactor to AddScoped would be caught immediately.
        ServiceCollection services = BuildBaseServices();
        services.AddScoped<ISummarizer, FakeCustomSummarizer>();

        services.AddGranitIndexingAISummarizer();
        ServiceProvider sp = services.BuildServiceProvider();

        using IServiceScope scope = sp.CreateScope();
        ISummarizer resolved = scope.ServiceProvider.GetRequiredService<ISummarizer>();
        resolved.ShouldBeOfType<FakeCustomSummarizer>();
    }

    [Fact]
    public void AddGranitIndexingAISummarizer_registers_default_prompt_builder()
    {
        ServiceCollection services = BuildBaseServices();

        services.AddGranitIndexingAISummarizer();
        ServiceProvider sp = services.BuildServiceProvider();

        sp.GetRequiredService<IAIAutoSummaryPromptBuilder>()
            .ShouldBeOfType<DefaultAIAutoSummaryPromptBuilder>();
    }

    private static ServiceCollection BuildBaseServices()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = [];
        services.AddSingleton(configuration);
        services.AddOptions();
        services.AddLogging();
        services.AddMetrics();
        services.AddGranitIndexing();
        return services;
    }

    private sealed class FakeCustomSummarizer : ISummarizer
    {
        public Task<string?> SummarizeAsync(string content, string? language = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("custom");
    }
}
