using System.Reflection;
using System.Text.Json;

namespace Granit.Http.Cookies.Tests.Ledger;

/// <summary>
/// Pins the consent-decision validation keys (ADR-066 <c>Cookies:Validation:*</c>) across
/// every base culture: a missing key would surface as a bare error code to the end user.
/// Regional cultures (fr-CA, en-GB, pt-BR) are diff-only and inherit from their base.
/// </summary>
public sealed class ConsentLedgerLocalizationTests
{
    private static readonly string[] BaseCultures =
        ["en", "fr", "nl", "de", "es", "it", "pt", "zh", "ja", "pl", "tr", "ko", "sv", "cs", "hi"];

    private static readonly string[] RequiredKeys =
    [
        "Cookies:Validation:UnknownCategory",
        "Cookies:Validation:CategoryOverlap",
        "Cookies:Validation:EmptyDecision",
    ];

    public static TheoryData<string, string> CultureKeyMatrix()
    {
        TheoryData<string, string> data = [];
        foreach (string culture in BaseCultures)
        {
            foreach (string key in RequiredKeys)
            {
                data.Add(culture, key);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CultureKeyMatrix))]
    public void EveryBaseCulture_CarriesEveryConsentValidationKey(string culture, string key)
    {
        // Arrange
        using JsonDocument document = LoadEmbeddedCulture(culture);

        // Assert
        document.RootElement.GetProperty("texts").TryGetProperty(key, out JsonElement text).ShouldBeTrue(
            $"Localization key '{key}' is missing from Localization/Cookies/{culture}.json");
        text.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    private static JsonDocument LoadEmbeddedCulture(string culture)
    {
        Assembly assembly = typeof(CookiesLocalizationResource).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($"Localization.Cookies.{culture}.json", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonDocument.Parse(stream);
    }
}
