using System.Globalization;
using Granit.Core.Modularity;
using Granit.Localization.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Localization.Tests;

public sealed class GranitLocalizationModuleTests : IDisposable
{
    private readonly CultureInfo _originalUICulture = CultureInfo.CurrentUICulture;

    public void Dispose() =>
        CultureInfo.CurrentUICulture = _originalUICulture;

    private static (ServiceConfigurationContext context, HostApplicationBuilder builder) CreateContext()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        // IFusionCache is a transitive dependency (via GranitCachingModule) — register manually for unit tests
        builder.Services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);
        return (context, builder);
    }

    [Fact]
    public void ConfigureServices_RegistersStringLocalizerFactory()
    {
        // Arrange
        GranitLocalizationModule module = new();
        (ServiceConfigurationContext context, HostApplicationBuilder builder) = CreateContext();

        // Act
        module.ConfigureServices(context);
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        IStringLocalizerFactory? factory = sp.GetService<IStringLocalizerFactory>();
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersGenericStringLocalizer()
    {
        // Arrange
        GranitLocalizationModule module = new();
        (ServiceConfigurationContext context, HostApplicationBuilder builder) = CreateContext();

        // Act
        module.ConfigureServices(context);
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        IStringLocalizer<GranitLocalizationResource>? localizer =
            sp.GetService<IStringLocalizer<GranitLocalizationResource>>();
        localizer.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_ResolvesGranitTranslation_Fr()
    {
        // Arrange
        GranitLocalizationModule module = new();
        (ServiceConfigurationContext context, HostApplicationBuilder builder) = CreateContext();

        module.ConfigureServices(context);
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        CultureInfo.CurrentUICulture = new CultureInfo("fr");

        // Act
        IStringLocalizer<GranitLocalizationResource> localizer =
            sp.GetRequiredService<IStringLocalizer<GranitLocalizationResource>>();
        LocalizedString result = localizer["Granit:EntityNotFound", "Patient", "123"];

        // Assert
        result.ResourceNotFound.ShouldBeFalse();
        result.Value.ShouldContain("Patient");
        result.Value.ShouldContain("123");
        result.Value.ShouldContain("entité");
    }

    [Fact]
    public void ConfigureServices_ResolvesGranitTranslation_En()
    {
        // Arrange
        GranitLocalizationModule module = new();
        (ServiceConfigurationContext context, HostApplicationBuilder builder) = CreateContext();

        module.ConfigureServices(context);
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        CultureInfo.CurrentUICulture = new CultureInfo("en");

        // Act
        IStringLocalizer<GranitLocalizationResource> localizer =
            sp.GetRequiredService<IStringLocalizer<GranitLocalizationResource>>();
        LocalizedString result = localizer["Granit:EntityNotFound", "Patient", "123"];

        // Assert
        result.ResourceNotFound.ShouldBeFalse();
        result.Value.ShouldContain("Patient");
        result.Value.ShouldContain("123");
        result.Value.ShouldContain("Entity");
    }

    [Fact]
    public void ConfigureServices_RegistersRegionalLanguageVariants()
    {
        // Arrange
        GranitLocalizationModule module = new();
        (ServiceConfigurationContext context, HostApplicationBuilder builder) = CreateContext();

        module.ConfigureServices(context);
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Act
        GranitLocalizationOptions options =
            sp.GetRequiredService<IOptions<GranitLocalizationOptions>>().Value;

        // Assert — 4 languages: fr, fr-CA, en, en-GB
        options.Languages.Count.ShouldBe(4);
        options.Languages.ShouldContain(l => l.CultureName == "fr" && l.DisplayName == "Français (France)");
        options.Languages.ShouldContain(l => l.CultureName == "fr-CA" && l.DisplayName == "Français (Canada)");
        options.Languages.ShouldContain(l => l.CultureName == "en" && l.DisplayName == "English (United States)" && l.IsDefault);
        options.Languages.ShouldContain(l => l.CultureName == "en-GB" && l.DisplayName == "English (United Kingdom)");
    }

    [Fact]
    public void ConfigureServices_ResolvesAllGranitKeys()
    {
        // Arrange
        GranitLocalizationModule module = new();
        (ServiceConfigurationContext context, HostApplicationBuilder builder) = CreateContext();

        module.ConfigureServices(context);
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        CultureInfo.CurrentUICulture = new CultureInfo("fr");

        // Act
        IStringLocalizer<GranitLocalizationResource> localizer =
            sp.GetRequiredService<IStringLocalizer<GranitLocalizationResource>>();

        // Assert
        localizer["Granit:ValidationError"].ResourceNotFound.ShouldBeFalse();
        localizer["Granit:Unauthorized"].ResourceNotFound.ShouldBeFalse();
        localizer["Granit:Forbidden"].ResourceNotFound.ShouldBeFalse();
        localizer["Granit:InternalError"].ResourceNotFound.ShouldBeFalse();
    }
}
