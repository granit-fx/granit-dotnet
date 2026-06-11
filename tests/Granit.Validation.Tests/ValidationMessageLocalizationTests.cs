// =============================================================================
// Tests - server-side validation message localization + interpolation
// =============================================================================
// Verifies the end-to-end behavior of the validation pipeline:
//   - GranitErrorCodeLanguageManager resolves "Validation:{key}" to the localized
//     template for the current UI culture (en / fr), falling back to the bare code.
//   - Built-in validators emit fully interpolated, human-readable messages
//     ('{PropertyName}', numeric params) instead of bare codes.
//   - WithErrorCodeAndMessage resolves custom codes to localized templates.
//   - The PascalCase property name is humanized ('NewPassword' -> 'New password').
// =============================================================================

using System.Globalization;
using FluentValidation.Internal;
using Granit.Localization;
using Granit.Localization.Extensions;
using Granit.Localization.Options;
using Granit.Validation.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class ValidationMessageLocalizationTests : IDisposable
{
    private readonly CultureInfo _originalUICulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUICulture;

    private static IStringLocalizer CreateValidationLocalizer()
    {
        ServiceCollection services = new();

        // Pre-register a no-op override reader so AddGranitLocalization's TryAdd skips the
        // FusionCache-backed CachedLocalizationOverrideStore (not wired in this unit test).
        services.AddSingleton<ILocalizationOverrideStoreReader, NoOpOverrideStoreReader>();

        services.AddGranitLocalization();
        services.Configure<GranitLocalizationOptions>(options =>
            options.Resources
                .Add<ValidationLocalizationResource>("fr")
                .AddJson(
                    typeof(ValidationLocalizationResource).Assembly,
                    "Granit.Validation.Localization.Validation"));

        ServiceProvider sp = services.BuildServiceProvider();
        IStringLocalizerFactory factory = sp.GetRequiredService<IStringLocalizerFactory>();
        return factory.Create(typeof(ValidationLocalizationResource));
    }

    // -------------------------------------------------------------------------
    // Resolution — GetString returns the localized template (no FV global state)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("en", "MinimumLengthValidator",
        "'{PropertyName}' must be at least {MinLength} characters. You entered {TotalLength} characters.")]
    [InlineData("fr", "MinimumLengthValidator",
        "'{PropertyName}' doit contenir au minimum {MinLength} caractères. Vous avez saisi {TotalLength} caractères.")]
    [InlineData("en", "NotEmptyValidator", "'{PropertyName}' must not be empty.")]
    public void GetString_ResolvesLocalizedTemplate(string culture, string key, string expected)
    {
        GranitErrorCodeLanguageManager manager = new() { Localizer = CreateValidationLocalizer() };
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        manager.GetString(key).ShouldBe(expected);
    }

    [Fact]
    public void GetString_UnknownKey_FallsBackToCode()
    {
        GranitErrorCodeLanguageManager manager = new() { Localizer = CreateValidationLocalizer() };
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        manager.GetString("NoSuchValidator").ShouldBe("Validation:Builtin:NoSuch");
    }

    [Fact]
    public void GetString_NoLocalizer_FallsBackToCode()
    {
        GranitErrorCodeLanguageManager manager = new();

        manager.GetString("MinimumLengthValidator").ShouldBe("Validation:Builtin:MinimumLength");
    }

    // -------------------------------------------------------------------------
    // Interpolation — the resolved template + FluentValidation's MessageFormatter
    // (the exact path RuleComponent.GetErrorMessage uses) produce a real sentence.
    // Property name is humanized; numeric placeholders are filled.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("en", "'New password' must be at least 8 characters. You entered 3 characters.")]
    [InlineData("fr", "'New password' doit contenir au minimum 8 caractères. Vous avez saisi 3 caractères.")]
    public void ResolvedTemplate_InterpolatesToLocalizedSentence(string culture, string expected)
    {
        GranitErrorCodeLanguageManager manager = new() { Localizer = CreateValidationLocalizer() };
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        string template = manager.GetString("MinimumLengthValidator");

        MessageFormatter formatter = new();
        formatter.AppendPropertyName(PropertyNameHumanizer.Humanize(nameof(ChangePassword.NewPassword)));
        formatter.AppendArgument("MinLength", 8);
        formatter.AppendArgument("TotalLength", 3);

        formatter.BuildMessage(template).ShouldBe(expected);
    }

    // -------------------------------------------------------------------------
    // Custom code resolution — the path used by WithErrorCodeAndMessage. The custom
    // code is itself the message key; it resolves to its localized sentence.
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveTemplate_CustomCode_ReturnsLocalizedSentence()
    {
        GranitErrorCodeLanguageManager manager = new() { Localizer = CreateValidationLocalizer() };
        CultureInfo.CurrentUICulture = new CultureInfo("en");

        manager.ResolveTemplate("Validation:Format:UrlHttps")
            .ShouldBe("The URL must use HTTPS.");
    }

    // -------------------------------------------------------------------------
    // Property name humanization
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("NewPassword", "New password")]
    [InlineData("ConfirmEmailAddress", "Confirm email address")]
    [InlineData("Email", "Email")]
    [InlineData("Already friendly", "Already friendly")]
    public void Humanize_SplitsPascalCaseIntoSentence(string input, string expected) =>
        PropertyNameHumanizer.Humanize(input).ShouldBe(expected);

    private sealed record ChangePassword(string NewPassword);

    private sealed class NoOpOverrideStoreReader : ILocalizationOverrideStoreReader
    {
        public Task<IReadOnlyDictionary<string, string>> GetOverridesAsync(
            string resourceName, string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>());
    }
}
