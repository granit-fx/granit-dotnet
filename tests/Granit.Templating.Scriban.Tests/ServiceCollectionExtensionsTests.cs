using Granit.Templating.GlobalContext;
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
    public void AddGranitTemplatingWithScriban_ITemplateEngine_IsReplaceable_WithTryAdd()
    {
        ServiceCollection services = new();
        services.AddSingleton<ITemplateEngine>(_ => null!); // pre-register custom engine
        services.AddGranitTemplatingWithScriban();           // TryAdd must not replace

        services.Count(d => d.ServiceType == typeof(ITemplateEngine))
                .ShouldBe(1, "TryAddSingleton must not add a duplicate");
    }
}
