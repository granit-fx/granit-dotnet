using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban.Extensions;
using Granit.Templating.Scriban.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    // -------------------------------------------------------------------------
    // AddGranitTemplatingWithScriban
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitTemplatingWithScriban_Registers_ITemplateEngine_Singleton()
    {
        ServiceCollection services = new();
        services.AddGranitTemplatingWithScriban();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitTemplatingWithScriban_Registers_ITextTemplateRenderer_Scoped()
    {
        ServiceCollection services = new();
        services.AddGranitTemplatingWithScriban();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITextTemplateRenderer) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitTemplatingWithScriban_Registers_Three_ITemplateGlobalContexts()
    {
        ServiceCollection services = new();
        services.AddGranitTemplatingWithScriban();

        // NowGlobalContext + ExecutionContextGlobalContext + AppGlobalContext
        services.Count(d => d.ServiceType == typeof(ITemplateGlobalContext))
                .ShouldBe(3, "NowGlobalContext, ExecutionContextGlobalContext and AppGlobalContext must be registered");
    }

    [Fact]
    public void AddGranitTemplatingWithScriban_KeepsScriban_WhenAnotherEngineAlreadyRegistered()
    {
        // Reproduces the regression: when another ITemplateEngine is registered first
        // (e.g. Granit.DocumentGeneration.Excel's ClosedXmlTemplateEngine), a plain
        // TryAddSingleton<ITemplateEngine> would no-op and silently drop Scriban — leaving
        // HTML/text templates with no engine. TryAddEnumerable must keep both.
        ServiceCollection services = new();
        services.AddSingleton<ITemplateEngine, FakeTemplateEngine>();
        services.AddGranitTemplatingWithScriban();

        using ServiceProvider provider = services.BuildServiceProvider();
        List<ITemplateEngine> engines = [.. provider.GetServices<ITemplateEngine>()];

        engines.ShouldContain(e => e is ScribanTemplateEngine,
            "Scriban must register additively so it survives alongside other engines.");
        engines.ShouldContain(e => e is FakeTemplateEngine);
    }

    [Fact]
    public void AddGranitTemplatingWithScriban_CalledTwice_RegistersScribanOnce()
    {
        ServiceCollection services = new();
        services.AddGranitTemplatingWithScriban();
        services.AddGranitTemplatingWithScriban();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetServices<ITemplateEngine>().Count(e => e is ScribanTemplateEngine)
                .ShouldBe(1, "TryAddEnumerable must dedupe Scriban on repeat registration.");
    }

    private sealed class FakeTemplateEngine : ITemplateEngine
    {
        public bool CanRender(TemplateDescriptor descriptor) => false;

        public Task<RenderedContent> RenderAsync<TData>(
            TemplateDescriptor descriptor,
            TData data,
            DocumentFormat targetFormat,
            IReadOnlyList<ITemplateGlobalContext> globalContexts,
            CancellationToken cancellationToken = default)
            where TData : notnull =>
            throw new NotSupportedException();
    }
}
