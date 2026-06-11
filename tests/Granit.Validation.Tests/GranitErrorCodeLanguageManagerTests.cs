// =============================================================================
// Tests - GranitErrorCodeLanguageManager
// =============================================================================
// Verifies:
//   - Returns "Validation:{key}" for any key
//   - Culture parameter has no effect on the returned code
//   - Enabled is true by default
//   - Culture is InvariantCulture by default
// =============================================================================

using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class GranitErrorCodeLanguageManagerTests
{
    // -------------------------------------------------------------------------
    // GetString
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("NotEmptyValidator", "Validation:Builtin:NotEmpty")]
    [InlineData("MaximumLengthValidator", "Validation:Builtin:MaximumLength")]
    [InlineData("EmailValidator", "Validation:Builtin:Email")]
    [InlineData("CustomKey", "Validation:CustomKey")]
    public void GetString_ReturnsGranitValidationCode(string key, string expected)
    {
        GranitErrorCodeLanguageManager manager = new();

        string result = manager.GetString(key);

        result.ShouldBe(expected);
    }

    [Fact]
    public void GetString_CultureParameterHasNoEffect()
    {
        GranitErrorCodeLanguageManager manager = new();

        string french = manager.GetString("NotEmptyValidator", new CultureInfo("fr"));
        string dutch = manager.GetString("NotEmptyValidator", new CultureInfo("nl"));
        string english = manager.GetString("NotEmptyValidator", new CultureInfo("en"));

        french.ShouldBe(dutch);
        french.ShouldBe(english);
    }

    // -------------------------------------------------------------------------
    // Default state
    // -------------------------------------------------------------------------

    [Fact]
    public void Enabled_IsTrueByDefault()
    {
        GranitErrorCodeLanguageManager manager = new();

        manager.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void Culture_IsInvariantCultureByDefault()
    {
        GranitErrorCodeLanguageManager manager = new();

        manager.Culture.ShouldBe(CultureInfo.InvariantCulture);
    }

    // -------------------------------------------------------------------------
    // Integration — AddGranitValidation sets the global language manager
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitValidation_SetsGranitErrorCodeLanguageManager()
    {
        Microsoft.Extensions.DependencyInjection.ServiceCollection services = new();

        services.AddGranitValidation();

        ValidatorOptions.Global.LanguageManager.ShouldBeOfType<GranitErrorCodeLanguageManager>();
    }

    [Fact]
    public void AddGranitValidation_BuiltInRule_EmitsErrorCode()
    {
        Microsoft.Extensions.DependencyInjection.ServiceCollection services = new();
        services.AddGranitValidation();

        InlineValidator<string> validator = [];
        validator.RuleFor(x => x).NotEmpty();

        ValidationResult result = validator.Validate(string.Empty);

        result.Errors[0].ErrorMessage.ShouldBe("Validation:Builtin:NotEmpty");
    }
}
