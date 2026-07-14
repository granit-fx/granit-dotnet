using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Validators;

namespace Granit.Http.Cookies.Endpoints.Tests;

/// <summary>
/// Rule matrix for <see cref="ConsentDecisionRequestValidator"/>. Messages are asserted by
/// error code — the wire contract — because the validator resolves the localized template
/// lazily through the module resource (<c>Cookies:Validation:*</c>, pinned across the
/// 18 cultures by <c>ConsentLedgerLocalizationTests</c> in the base package).
/// </summary>
public sealed class ConsentDecisionRequestValidatorTests
{
    private readonly ConsentDecisionRequestValidator _validator = new();

    [Theory]
    [InlineData(new[] { "analytics" }, null)]
    [InlineData(null, new[] { "marketing" })]
    [InlineData(new[] { "strictly_necessary", "preferences" }, new[] { "marketing", "sale_or_sharing" })]
    public void Validate_KnownDisjointCategories_Passes(string[]? granted, string[]? denied)
    {
        // Act
        ValidationResult result = _validator.Validate(new ConsentDecisionRequest(granted, denied));

        // Assert
        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("tracking")]
    [InlineData("Analytics")] // PascalCase is not the wire format
    [InlineData("")]
    public void Validate_UnknownGrantedCategory_FailsWithUnknownCategoryCode(string category)
    {
        // Act
        ValidationResult result = _validator.Validate(
            new ConsentDecisionRequest(GrantedCategories: [category]));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Cookies:Validation:UnknownCategory");
    }

    [Fact]
    public void Validate_UnknownDeniedCategory_FailsWithUnknownCategoryCode()
    {
        // Act
        ValidationResult result = _validator.Validate(
            new ConsentDecisionRequest(DeniedCategories: ["tracking"]));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Cookies:Validation:UnknownCategory");
    }

    [Fact]
    public void Validate_CategoryBothGrantedAndDenied_FailsWithOverlapCode()
    {
        // Act
        ValidationResult result = _validator.Validate(new ConsentDecisionRequest(
            GrantedCategories: ["analytics", "preferences"],
            DeniedCategories: ["analytics"]));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Cookies:Validation:CategoryOverlap");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(new string[0], new string[0])]
    public void Validate_DecisionWithoutAnyCategory_FailsWithEmptyDecisionCode(
        string[]? granted, string[]? denied)
    {
        // Act
        ValidationResult result = _validator.Validate(new ConsentDecisionRequest(granted, denied));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Cookies:Validation:EmptyDecision");
    }

    [Fact]
    public void Validate_EmptyCmpSource_Fails()
    {
        // Act
        ValidationResult result = _validator.Validate(new ConsentDecisionRequest(
            GrantedCategories: ["analytics"], CmpSource: string.Empty));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ConsentDecisionRequest.CmpSource));
    }

    [Fact]
    public void Validate_CmpSourceOver64Chars_Fails()
    {
        // Act
        ValidationResult result = _validator.Validate(new ConsentDecisionRequest(
            GrantedCategories: ["analytics"], CmpSource: new string('x', 65)));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ConsentDecisionRequest.CmpSource));
    }

    [Fact]
    public void Validate_DefaultCmpSource_Passes()
    {
        // Act — the record's default ("cookieconsent") satisfies the NotEmpty rule.
        ValidationResult result = _validator.Validate(
            new ConsentDecisionRequest(GrantedCategories: ["analytics"]));

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
