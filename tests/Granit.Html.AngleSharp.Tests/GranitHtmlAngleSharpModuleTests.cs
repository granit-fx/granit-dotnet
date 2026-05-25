using Granit.Html.AngleSharp.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Html.AngleSharp.Tests;

public sealed class GranitHtmlAngleSharpModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitHtmlAngleSharpModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitHtmlAngleSharpModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitHtmlModule()
    {
        var attrs =
            (DependsOnAttribute[])typeof(GranitHtmlAngleSharpModule)
                .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitHtmlModule));
    }

    [Fact]
    public void AddGranitHtmlAngleSharp_registers_trusted_keyed_converter()
    {
        ServiceCollection services = new();
        services.AddGranitHtmlAngleSharp();

        using ServiceProvider sp = services.BuildServiceProvider();
        IHtmlToPlainTextConverter converter =
            sp.GetRequiredKeyedService<IHtmlToPlainTextConverter>(HtmlConverterKeys.Trusted);

        converter.ShouldBeOfType<AngleSharpHtmlToPlainTextConverter>();
    }

    [Fact]
    public void AddGranitHtmlAngleSharp_registers_untrusted_keyed_converter()
    {
        ServiceCollection services = new();
        services.AddGranitHtmlAngleSharp();

        using ServiceProvider sp = services.BuildServiceProvider();
        IHtmlToPlainTextConverter converter =
            sp.GetRequiredKeyedService<IHtmlToPlainTextConverter>(HtmlConverterKeys.Untrusted);

        converter.ShouldBeOfType<AngleSharpHtmlToPlainTextConverter>();
    }

    [Fact]
    public void AddGranitHtmlAngleSharp_does_not_register_unkeyed_default()
    {
        ServiceCollection services = new();
        services.AddGranitHtmlAngleSharp();

        services.Count(d =>
            d.ServiceType == typeof(IHtmlToPlainTextConverter)
            && d.ServiceKey is null).ShouldBe(0);
    }

    [Fact]
    public void AddGranitHtmlAngleSharp_is_idempotent_via_TryAdd()
    {
        ServiceCollection services = new();
        services.AddGranitHtmlAngleSharp();
        services.AddGranitHtmlAngleSharp();

        services.Count(d =>
            d.ServiceType == typeof(IHtmlToPlainTextConverter)
            && Equals(d.ServiceKey, HtmlConverterKeys.Trusted)).ShouldBe(1);
        services.Count(d =>
            d.ServiceType == typeof(IHtmlToPlainTextConverter)
            && Equals(d.ServiceKey, HtmlConverterKeys.Untrusted)).ShouldBe(1);
    }
}
