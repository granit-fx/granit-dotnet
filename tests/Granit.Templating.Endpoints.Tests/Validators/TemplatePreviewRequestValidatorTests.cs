using System.Text.Json;
using FluentValidation.Results;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Templating.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests.Validators;

public sealed class TemplatePreviewRequestValidatorTests
{
    private readonly TemplatePreviewRequestValidator _validator = new();

    // -------------------------------------------------------------------------
    // Valid request
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_NullCulture_ReturnsValid()
    {
        TemplatePreviewRequest request = new(Culture: null, Data: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("fr-BE")]
    [InlineData("en-GB")]
    [InlineData("pt")]
    public void Validate_ValidCulture_ReturnsValid(string culture)
    {
        TemplatePreviewRequest request = new(Culture: culture, Data: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithJsonData_ReturnsValid()
    {
        JsonElement data = JsonDocument.Parse("""{"name":"test"}""").RootElement;
        TemplatePreviewRequest request = new(Culture: "fr", Data: data);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Culture — invalid format
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("123")]
    [InlineData("fr_BE")]
    [InlineData("-fr")]
    [InlineData("a")]
    public void Validate_InvalidCultureFormat_Fails(string culture)
    {
        TemplatePreviewRequest request = new(Culture: culture, Data: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(TemplatePreviewRequest.Culture));
    }

    // -------------------------------------------------------------------------
    // Culture — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_CultureExceedsMaxLength_Fails()
    {
        string longCulture = new('x', TemplatingPatterns.MaxCultureLength + 1);
        TemplatePreviewRequest request = new(Culture: longCulture, Data: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }
}
