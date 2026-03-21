using System.Globalization;
using Granit.Localization.Internal;
using Granit.Localization.Options;
using Granit.Localization.Tests.TestResources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Localization.Tests;

public sealed class JsonStringLocalizerAdditionalTests : IDisposable
{
    private readonly CultureInfo _originalUICulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _originalUICulture;
        CultureInfo.CurrentCulture = _originalCulture;
    }

    private static IStringLocalizer CreateTestLocalizer(
        ILocalizationOverrideStoreReader? overrideStore = null)
    {
        ServiceCollection services = new();
        services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<TestResource>("fr")
                .AddJson(
                    typeof(JsonStringLocalizerAdditionalTests).Assembly,
                    "Granit.Localization.Tests.TestResources.Localization.Test");

            options.Resources
                .Add<ParentTestResource>("fr")
                .AddJson(
                    typeof(JsonStringLocalizerAdditionalTests).Assembly,
                    "Granit.Localization.Tests.TestResources.Localization.Parent");
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IOptions<GranitLocalizationOptions> opts = sp.GetRequiredService<IOptions<GranitLocalizationOptions>>();
        JsonStringLocalizerFactory factory = new(opts, overrideStore);
        return factory.Create(typeof(TestResource));
    }

    [Fact]
    public void GetAllStrings_WithIncludeParentCultures_IncludesDefaultCultureKeys()
    {
        IStringLocalizer localizer = CreateTestLocalizer();
        CultureInfo.CurrentUICulture = new CultureInfo("de");

        var all = localizer.GetAllStrings(includeParentCultures: true).ToList();

        // Should fallback to default culture (fr) and return keys
        all.ShouldNotBeEmpty();
        all.Select(s => s.Name).ShouldContain("Test:Hello");
    }

    [Fact]
    public void GetAllStrings_WithDbOverrides_OverridesArePrioritized()
    {
        ILocalizationOverrideStoreReader overrideStore = Substitute.For<ILocalizationOverrideStoreReader>();
        overrideStore.GetOverridesAsync("Test", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { ["Test:Hello"] = "Override Bonjour" }));

        IStringLocalizer localizer = CreateTestLocalizer(overrideStore);
        CultureInfo.CurrentUICulture = new CultureInfo("fr");

        var all = localizer.GetAllStrings(includeParentCultures: false).ToList();

        LocalizedString? helloEntry = all.FirstOrDefault(s => s.Name == "Test:Hello");
        helloEntry.ShouldNotBeNull();
        helloEntry.Value.ShouldBe("Override Bonjour");
    }

    [Fact]
    public void Indexer_WithDbOverride_ReturnsDatabaseValue()
    {
        ILocalizationOverrideStoreReader overrideStore = Substitute.For<ILocalizationOverrideStoreReader>();
        overrideStore.GetOverridesAsync("Test", "fr", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { ["Test:Hello"] = "Salut (DB)" }));

        IStringLocalizer localizer = CreateTestLocalizer(overrideStore);
        CultureInfo.CurrentUICulture = new CultureInfo("fr");

        LocalizedString result = localizer["Test:Hello"];

        result.Value.ShouldBe("Salut (DB)");
        result.ResourceNotFound.ShouldBeFalse();
    }

    [Fact]
    public void Indexer_WithArguments_NoArgs_ReturnsRawValue()
    {
        IStringLocalizer localizer = CreateTestLocalizer();
        CultureInfo.CurrentUICulture = new CultureInfo("fr");

        // Call the arguments overload with empty args
        LocalizedString result = localizer["Test:Hello", Array.Empty<object>()];

        result.Value.ShouldBe("Bonjour");
        result.ResourceNotFound.ShouldBeFalse();
    }

    [Fact]
    public void GetAllStrings_WithRegionalCulture_IncludesParentAndRegionalKeys()
    {
        IStringLocalizer localizer = CreateTestLocalizer();
        CultureInfo.CurrentUICulture = new CultureInfo("fr-CA");

        var all = localizer.GetAllStrings(includeParentCultures: true).ToList();

        // Should include keys from fr-CA, fr (parent), and default
        all.ShouldNotBeEmpty();
        all.Select(s => s.Name).ShouldContain("Test:Hello");
    }
}
