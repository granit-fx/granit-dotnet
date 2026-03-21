using System.Globalization;
using Granit.Localization.Options;
using Shouldly;
using Xunit;

namespace Granit.Localization.Tests;

public sealed class GranitLocalizationOptionsTests
{
    [Fact]
    public void Resources_IsInitialized()
    {
        GranitLocalizationOptions options = new();

        options.Resources.ShouldNotBeNull();
    }

    [Fact]
    public void DefaultResourceType_DefaultsToNull()
    {
        GranitLocalizationOptions options = new();

        options.DefaultResourceType.ShouldBeNull();
    }

    [Fact]
    public void DefaultResourceType_CanBeSet()
    {
        GranitLocalizationOptions options = new()
        {
            DefaultResourceType = typeof(GranitLocalizationResource),
        };

        options.DefaultResourceType.ShouldBe(typeof(GranitLocalizationResource));
    }

    [Fact]
    public void Languages_IsInitializedEmpty()
    {
        GranitLocalizationOptions options = new();

        options.Languages.ShouldNotBeNull();
        options.Languages.ShouldBeEmpty();
    }

    [Fact]
    public void Languages_CanAddMultiple()
    {
        GranitLocalizationOptions options = new();

        options.Languages.Add(new LanguageInfo("fr", "Français", "fr"));
        options.Languages.Add(new LanguageInfo("en", "English", "gb"));

        options.Languages.Count.ShouldBe(2);
    }

    [Fact]
    public void FormattingCultures_IsInitializedEmpty()
    {
        GranitLocalizationOptions options = new();

        options.FormattingCultures.ShouldNotBeNull();
        options.FormattingCultures.ShouldBeEmpty();
    }

    [Fact]
    public void FormattingCultures_CanAddCultures()
    {
        GranitLocalizationOptions options = new();

        options.FormattingCultures.Add(new CultureInfo("en-US"));
        options.FormattingCultures.Add(new CultureInfo("fr-FR"));

        options.FormattingCultures.Count.ShouldBe(2);
    }

    [Fact]
    public void EnableAutoDiscovery_DefaultsToFalse()
    {
        GranitLocalizationOptions options = new();

        options.EnableAutoDiscovery.ShouldBeFalse();
    }

    [Fact]
    public void EnableAutoDiscovery_CanBeEnabled()
    {
        GranitLocalizationOptions options = new()
        {
            EnableAutoDiscovery = true,
        };

        options.EnableAutoDiscovery.ShouldBeTrue();
    }
}
