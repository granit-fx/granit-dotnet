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
    public void AddGranitHtmlAngleSharp_registers_default_converter()
    {
        ServiceCollection services = new();
        services.AddGranitHtmlAngleSharp();

        using ServiceProvider sp = services.BuildServiceProvider();
        IHtmlToPlainTextConverter converter = sp.GetRequiredService<IHtmlToPlainTextConverter>();

        converter.ShouldBeOfType<AngleSharpHtmlToPlainTextConverter>();
    }

    [Fact]
    public void AddGranitHtmlAngleSharp_is_idempotent_via_TryAdd()
    {
        ServiceCollection services = new();
        services.AddGranitHtmlAngleSharp();
        services.AddGranitHtmlAngleSharp();

        services.Count(d => d.ServiceType == typeof(IHtmlToPlainTextConverter)).ShouldBe(1);
    }
}
