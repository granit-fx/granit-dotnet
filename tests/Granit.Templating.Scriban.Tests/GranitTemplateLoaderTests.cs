using System.Globalization;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Scriban;
using Scriban.Parsing;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests;

public sealed class GranitTemplateLoaderTests
{
    [Fact]
    public void Load_Sync_ThrowsNotSupportedException()
    {
        GranitTemplateLoader sut = new(new ServiceCollection().BuildServiceProvider());

        Should.Throw<NotSupportedException>(() =>
            sut.Load(new TemplateContext(), default, "Test.Template"));
    }

    [Fact]
    public void GetPath_WithCulture_ReturnsCultureQualifiedKey()
    {
        GranitTemplateLoader sut = new(new ServiceCollection().BuildServiceProvider());

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-BE");
            string path = sut.GetPath(new TemplateContext(), default, "Notifications.Layout");

            path.ShouldBe("Notifications.Layout|fr-BE");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task LoadAsync_ResolvesFromResolverChain()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateDescriptor
            {
                Content = "<p>Layout content</p>",
                MimeType = "text/html",
            });

        ServiceCollection services = new();
        services.AddSingleton(resolver);
        ServiceProvider sp = services.BuildServiceProvider();

        GranitTemplateLoader sut = new(sp);
        string? result = await sut.LoadAsync(
            new TemplateContext(), default, "Notifications.Layout");

        result.ShouldBe("<p>Layout content</p>");
    }

    [Fact]
    public async Task LoadAsync_FallsBackToNeutralCulture()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);

        // Culture-specific returns null
        resolver.TryResolveAsync(
                Arg.Is<TemplateKey>(k => k.Culture == "fr"),
                Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        // Neutral returns content
        resolver.TryResolveAsync(
                Arg.Is<TemplateKey>(k => k.Culture == null),
                Arg.Any<CancellationToken>())
            .Returns(new TemplateDescriptor
            {
                Content = "<p>Neutral fallback</p>",
                MimeType = "text/html",
            });

        ServiceCollection services = new();
        services.AddSingleton(resolver);
        ServiceProvider sp = services.BuildServiceProvider();

        GranitTemplateLoader sut = new(sp);
        string? result = await sut.LoadAsync(
            new TemplateContext(), default, "Notifications.Layout|fr");

        result.ShouldBe("<p>Neutral fallback</p>");
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenNoResolverFindsTemplate()
    {
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        ServiceCollection services = new();
        services.AddSingleton(resolver);
        ServiceProvider sp = services.BuildServiceProvider();

        GranitTemplateLoader sut = new(sp);
        string? result = await sut.LoadAsync(
            new TemplateContext(), default, "Missing.Template");

        result.ShouldBeNull();
    }
}
