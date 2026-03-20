// ---------------------------------------------------------------------------
// LocalizationAutoDiscoveryTests.cs
// Vérifie que l'auto-discovery détecte les ressources JSON embarquées par
// convention sans enregistrement explicite Add<T>() / AddJson().
// ---------------------------------------------------------------------------

using System.Globalization;
using Granit.Localization.Extensions;
using Granit.Localization.Tests.TestResources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Localization.Tests;

public sealed class LocalizationAutoDiscoveryTests
{
    private static ServiceProvider BuildProviderWithAutoDiscovery()
    {
        ServiceCollection services = new();
        services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        services.AddGranitLocalization(options =>
        {
            options.EnableAutoDiscovery = true;
            // Aucun Add<T>(), AddJson() ou AddBaseTypes() explicite
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void GivenAutoDiscovery_WhenRequestingLocalizer_ThenLocalizerIsResolved()
    {
        ServiceProvider provider = BuildProviderWithAutoDiscovery();

        IStringLocalizer<TestResource> localizer =
            provider.GetRequiredService<IStringLocalizer<TestResource>>();

        localizer.ShouldNotBeNull();
    }

    [Fact]
    public void GivenAutoDiscovery_WhenLocalizing_ThenFrenchTranslationIsFound()
    {
        ServiceProvider provider = BuildProviderWithAutoDiscovery();
        IStringLocalizer<TestResource> localizer =
            provider.GetRequiredService<IStringLocalizer<TestResource>>();

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr");

            LocalizedString result = localizer["Test:Hello"];

            result.ResourceNotFound.ShouldBeFalse();
            result.Value.ShouldBe("Bonjour");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void GivenAutoDiscovery_WhenLocalizing_ThenEnglishTranslationIsFound()
    {
        ServiceProvider provider = BuildProviderWithAutoDiscovery();
        IStringLocalizer<TestResource> localizer =
            provider.GetRequiredService<IStringLocalizer<TestResource>>();

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");

            LocalizedString result = localizer["Test:Hello"];

            result.ResourceNotFound.ShouldBeFalse();
            result.Value.ShouldBe("Hello");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void GivenAutoDiscovery_WhenExplicitRegistrationExists_ThenExplicitTakesPriority()
    {
        // Arrange : enregistrement explicite avec une source JSON différente
        ServiceCollection services = new();
        services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        services.AddGranitLocalization(options =>
        {
            options.EnableAutoDiscovery = true;

            // Enregistrement explicite : pointe sur la ressource Parent (qui a d'autres clés)
            options.Resources
                .Add<TestResource>(defaultCulture: "fr")
                .AddJson(
                    typeof(ParentTestResource).Assembly,
                    "Granit.Localization.Tests.TestResources.Localization.Parent");
        });
        ServiceProvider provider = services.BuildServiceProvider();
        IStringLocalizer<TestResource> localizer =
            provider.GetRequiredService<IStringLocalizer<TestResource>>();

        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr");

            // "Parent:OnlyInParent" existe dans Parent/fr.json mais pas dans Test/fr.json
            LocalizedString result = localizer["Parent:OnlyInParent"];

            // L'enregistrement explicite (source Parent) est utilisé, pas l'auto-discovery
            result.ResourceNotFound.ShouldBeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void GivenAutoDiscovery_WhenResourceHasNoJsonFiles_ThenLocalizerReturnsKey()
    {
        ServiceProvider provider = BuildProviderWithAutoDiscovery();
        IStringLocalizer<UnregisteredResource> localizer =
            provider.GetRequiredService<IStringLocalizer<UnregisteredResource>>();

        LocalizedString result = localizer["AnyKey"];

        result.ResourceNotFound.ShouldBeTrue();
        result.Value.ShouldBe("AnyKey");
    }
}
