using Granit.Localization.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

public sealed class LocalizationDtosTests
{
    // -------------------------------------------------------------------------
    // LanguageInfoResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void LanguageInfoResponse_Constructor_SetsAllProperties()
    {
        LanguageInfoResponse response = new("fr", "Français", "fr", true);

        response.CultureName.ShouldBe("fr");
        response.DisplayName.ShouldBe("Français");
        response.FlagIcon.ShouldBe("fr");
        response.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void LanguageInfoResponse_WithNullFlagIcon_IsValid()
    {
        LanguageInfoResponse response = new("en", "English", null, false);

        response.FlagIcon.ShouldBeNull();
        response.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void LanguageInfoResponse_Equality_WorksCorrectly()
    {
        LanguageInfoResponse response1 = new("fr", "Français", "fr", true);
        LanguageInfoResponse response2 = new("fr", "Français", "fr", true);

        response1.ShouldBe(response2);
    }

    [Fact]
    public void LanguageInfoResponse_Inequality_WhenDifferentCulture()
    {
        LanguageInfoResponse response1 = new("fr", "Français", "fr", true);
        LanguageInfoResponse response2 = new("en", "English", "gb", false);

        response1.ShouldNotBe(response2);
    }

    // -------------------------------------------------------------------------
    // SetLocalizationOverrideRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void SetLocalizationOverrideRequest_Constructor_SetsValue()
    {
        SetLocalizationOverrideRequest request = new("Bonjour");

        request.Value.ShouldBe("Bonjour");
    }

    [Fact]
    public void SetLocalizationOverrideRequest_Equality_WorksCorrectly()
    {
        SetLocalizationOverrideRequest request1 = new("Bonjour");
        SetLocalizationOverrideRequest request2 = new("Bonjour");

        request1.ShouldBe(request2);
    }

    // -------------------------------------------------------------------------
    // ApplicationLocalizationResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplicationLocalizationResponse_Constructor_SetsAllProperties()
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> resources = new()
        {
            ["Test"] = new Dictionary<string, string> { ["Hello"] = "Bonjour" },
        };
        List<LanguageInfoResponse> languages =
        [
            new("fr", "Français", "fr", true),
        ];

        ApplicationLocalizationResponse response = new("fr", resources, languages);

        response.CultureName.ShouldBe("fr");
        response.Resources.Count.ShouldBe(1);
        response.Languages.Count.ShouldBe(1);
    }
}
