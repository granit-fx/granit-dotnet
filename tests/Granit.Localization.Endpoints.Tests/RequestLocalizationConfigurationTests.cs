using System.Globalization;
using Granit.Localization.Endpoints.Extensions;
using Granit.Localization.Options;
using Microsoft.AspNetCore.Builder;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

public sealed class RequestLocalizationConfigurationTests
{
    [Fact]
    public void ConfigureFromGranitOptions_WithLanguagesOnly_SetsBothCulturesFromLanguages()
    {
        // Arrange
        var granitOptions = new GranitLocalizationOptions();
        granitOptions.Languages.Add(new LanguageInfo("fr", "Français", "fr", isDefault: true));
        granitOptions.Languages.Add(new LanguageInfo("en", "English", "gb"));

        var requestOptions = new RequestLocalizationOptions();

        // Act
        LocalizationApplicationBuilderExtensions.ConfigureFromGranitOptions(requestOptions, granitOptions);

        // Assert
        requestOptions.SupportedCultures.ShouldNotBeNull();
        requestOptions.SupportedUICultures.ShouldNotBeNull();

        var cultureNames = requestOptions.SupportedCultures!.Select(c => c.Name).ToList();
        cultureNames.ShouldContain("fr");
        cultureNames.ShouldContain("en");

        var uiCultureNames = requestOptions.SupportedUICultures!.Select(c => c.Name).ToList();
        uiCultureNames.ShouldContain("fr");
        uiCultureNames.ShouldContain("en");

        cultureNames.ShouldBe(uiCultureNames);
    }

    [Fact]
    public void ConfigureFromGranitOptions_WithFormattingCultures_SeparatesCulturesAndUICultures()
    {
        // Arrange
        var granitOptions = new GranitLocalizationOptions();
        granitOptions.Languages.Add(new LanguageInfo("fr", "Français", "fr", isDefault: true));
        granitOptions.Languages.Add(new LanguageInfo("de", "Deutsch", "de"));
        granitOptions.FormattingCultures.Add(new CultureInfo("en-US"));

        var requestOptions = new RequestLocalizationOptions();

        // Act
        LocalizationApplicationBuilderExtensions.ConfigureFromGranitOptions(requestOptions, granitOptions);

        // Assert — formatting cultures = en-US only
        requestOptions.SupportedCultures.ShouldNotBeNull();
        requestOptions.SupportedCultures!.Select(c => c.Name).ShouldBe(["en-US"]);

        // Assert — UI cultures = fr + de (from Languages)
        requestOptions.SupportedUICultures.ShouldNotBeNull();
        var uiCultureNames = requestOptions.SupportedUICultures!.Select(c => c.Name).ToList();
        uiCultureNames.ShouldContain("fr");
        uiCultureNames.ShouldContain("de");
        uiCultureNames.ShouldNotContain("en-US");
    }

    [Fact]
    public void ConfigureFromGranitOptions_SetsDefaultCultureFromIsDefaultLanguage()
    {
        // Arrange
        var granitOptions = new GranitLocalizationOptions();
        granitOptions.Languages.Add(new LanguageInfo("fr", "Français", "fr"));
        granitOptions.Languages.Add(new LanguageInfo("en", "English", "gb", isDefault: true));

        var requestOptions = new RequestLocalizationOptions();

        // Act
        LocalizationApplicationBuilderExtensions.ConfigureFromGranitOptions(requestOptions, granitOptions);

        // Assert
        requestOptions.DefaultRequestCulture.Culture.Name.ShouldBe("en");
        requestOptions.DefaultRequestCulture.UICulture.Name.ShouldBe("en");
    }

    [Fact]
    public void ConfigureFromGranitOptions_WithNoIsDefault_FallsBackToFirstLanguage()
    {
        // Arrange
        var granitOptions = new GranitLocalizationOptions();
        granitOptions.Languages.Add(new LanguageInfo("nl", "Nederlands", "nl"));
        granitOptions.Languages.Add(new LanguageInfo("fr", "Français", "fr"));

        var requestOptions = new RequestLocalizationOptions();

        // Act
        LocalizationApplicationBuilderExtensions.ConfigureFromGranitOptions(requestOptions, granitOptions);

        // Assert
        requestOptions.DefaultRequestCulture.Culture.Name.ShouldBe("nl");
    }

    [Fact]
    public void ConfigureFromGranitOptions_WithEmptyLanguages_DoesNotThrow()
    {
        // Arrange
        var granitOptions = new GranitLocalizationOptions();
        var requestOptions = new RequestLocalizationOptions();

        // Act & Assert
        Should.NotThrow(() =>
            LocalizationApplicationBuilderExtensions.ConfigureFromGranitOptions(requestOptions, granitOptions));
    }

    [Fact]
    public void ConfigureFromGranitOptions_WithMultipleFormattingCultures_SetsAll()
    {
        // Arrange — Swiss app: UI in de/fr/it, formatting in de-CH/fr-CH/it-CH
        var granitOptions = new GranitLocalizationOptions();
        granitOptions.Languages.Add(new LanguageInfo("de", "Deutsch", "de", isDefault: true));
        granitOptions.Languages.Add(new LanguageInfo("fr", "Français", "fr"));
        granitOptions.Languages.Add(new LanguageInfo("it", "Italiano", "it"));
        granitOptions.FormattingCultures.Add(new CultureInfo("de-CH"));
        granitOptions.FormattingCultures.Add(new CultureInfo("fr-CH"));
        granitOptions.FormattingCultures.Add(new CultureInfo("it-CH"));

        var requestOptions = new RequestLocalizationOptions();

        // Act
        LocalizationApplicationBuilderExtensions.ConfigureFromGranitOptions(requestOptions, granitOptions);

        // Assert
        var formattingNames = requestOptions.SupportedCultures!.Select(c => c.Name).ToList();
        formattingNames.ShouldBe(["de-CH", "fr-CH", "it-CH"]);

        var uiNames = requestOptions.SupportedUICultures!.Select(c => c.Name).ToList();
        uiNames.ShouldBe(["de", "fr", "it"]);
    }
}
