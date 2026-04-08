using Granit.Templating.Mjml.Extensions;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Templating.Mjml.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitTemplatingWithMjml_Registers_MjmlTransformer()
    {
        ServiceCollection services = [];
        services.AddGranitTemplatingWithMjml();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IRenderedContentTransformer) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitTemplatingWithMjml_Registers_ITextTemplateRenderer()
    {
        ServiceCollection services = [];
        services.AddGranitTemplatingWithMjml();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITextTemplateRenderer) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
