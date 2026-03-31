using FluentValidation.Results;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests.Validators;

public sealed class ReferenceDataCreateRequestValidatorAdditionalTests
{
    private readonly ReferenceDataCreateRequestValidator _validator = new();

    private static ReferenceDataCreateRequest ValidRequest() =>
        new(Code: "BE", LabelEn: "Belgium");

    // -------------------------------------------------------------------------
    // Remaining optional labels — MaxLength (Zh, Ja, Pl, Tr, Ko, Sv, Cs)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelZh))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelJa))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelPl))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelTr))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelKo))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelSv))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelCs))]
    public void Validate_RemainingOptionalLabelExceedsMaxLength_Fails(string propertyName)
    {
        string longLabel = new('x', ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>.MaxLabelLength + 1);
        ReferenceDataCreateRequest request = propertyName switch
        {
            nameof(ReferenceDataCreateRequest.LabelZh) => ValidRequest() with { LabelZh = longLabel },
            nameof(ReferenceDataCreateRequest.LabelJa) => ValidRequest() with { LabelJa = longLabel },
            nameof(ReferenceDataCreateRequest.LabelPl) => ValidRequest() with { LabelPl = longLabel },
            nameof(ReferenceDataCreateRequest.LabelTr) => ValidRequest() with { LabelTr = longLabel },
            nameof(ReferenceDataCreateRequest.LabelKo) => ValidRequest() with { LabelKo = longLabel },
            nameof(ReferenceDataCreateRequest.LabelSv) => ValidRequest() with { LabelSv = longLabel },
            nameof(ReferenceDataCreateRequest.LabelCs) => ValidRequest() with { LabelCs = longLabel },
            _ => throw new ArgumentException(propertyName),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == propertyName);
    }

    // -------------------------------------------------------------------------
    // Code at max length — should pass
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_CodeAtMaxLength_Passes()
    {
        string maxCode = new('X', ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>.MaxCodeLength);
        ReferenceDataCreateRequest request = ValidRequest() with { Code = maxCode };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // LabelEn at max length — should pass
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_LabelEnAtMaxLength_Passes()
    {
        string maxLabel = new('x', ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>.MaxLabelLength);
        ReferenceDataCreateRequest request = ValidRequest() with { LabelEn = maxLabel };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // SortOrder at zero — should pass
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_SortOrderZero_Passes()
    {
        ReferenceDataCreateRequest request = ValidRequest() with { SortOrder = 0 };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Only ValidTo without ValidFrom — should pass (conditional rule)
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_OnlyValidTo_ReturnsValid()
    {
        ReferenceDataCreateRequest request = ValidRequest() with
        {
            ValidFrom = null,
            ValidTo = DateTimeOffset.UtcNow.AddDays(30),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // ValidTo equal to ValidFrom — should fail
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidToEqualsValidFrom_Fails()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ReferenceDataCreateRequest request = ValidRequest() with
        {
            ValidFrom = now,
            ValidTo = now,
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }
}
