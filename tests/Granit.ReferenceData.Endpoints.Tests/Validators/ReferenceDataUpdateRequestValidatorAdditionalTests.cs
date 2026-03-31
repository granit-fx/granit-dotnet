using FluentValidation.Results;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests.Validators;

public sealed class ReferenceDataUpdateRequestValidatorAdditionalTests
{
    private readonly ReferenceDataUpdateRequestValidator _validator = new();

    private static ReferenceDataUpdateRequest ValidRequest() =>
        new(LabelEn: "Belgium");

    // -------------------------------------------------------------------------
    // Optional labels — MaxLength
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelFr))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelNl))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelDe))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelEs))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelIt))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelPt))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelZh))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelJa))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelPl))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelTr))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelKo))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelSv))]
    [InlineData(nameof(ReferenceDataUpdateRequest.LabelCs))]
    public void Validate_OptionalLabelExceedsMaxLength_Fails(string propertyName)
    {
        string longLabel = new('x', ReferenceDataMutableFieldsValidator<ReferenceDataUpdateRequest>.MaxLabelLength + 1);
        ReferenceDataUpdateRequest request = propertyName switch
        {
            nameof(ReferenceDataUpdateRequest.LabelFr) => ValidRequest() with { LabelFr = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelNl) => ValidRequest() with { LabelNl = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelDe) => ValidRequest() with { LabelDe = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelEs) => ValidRequest() with { LabelEs = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelIt) => ValidRequest() with { LabelIt = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelPt) => ValidRequest() with { LabelPt = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelZh) => ValidRequest() with { LabelZh = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelJa) => ValidRequest() with { LabelJa = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelPl) => ValidRequest() with { LabelPl = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelTr) => ValidRequest() with { LabelTr = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelKo) => ValidRequest() with { LabelKo = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelSv) => ValidRequest() with { LabelSv = longLabel },
            nameof(ReferenceDataUpdateRequest.LabelCs) => ValidRequest() with { LabelCs = longLabel },
            _ => throw new ArgumentException(propertyName),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == propertyName);
    }

    // -------------------------------------------------------------------------
    // LabelEn at max length — should pass
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_LabelEnAtMaxLength_Passes()
    {
        string maxLabel = new('x', ReferenceDataMutableFieldsValidator<ReferenceDataUpdateRequest>.MaxLabelLength);
        ReferenceDataUpdateRequest request = ValidRequest() with { LabelEn = maxLabel };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // SortOrder zero — should pass
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_SortOrderZero_Passes()
    {
        ReferenceDataUpdateRequest request = ValidRequest() with { SortOrder = 0 };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Only ValidTo without ValidFrom — should pass
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_OnlyValidTo_ReturnsValid()
    {
        ReferenceDataUpdateRequest request = ValidRequest() with
        {
            ValidFrom = null,
            ValidTo = DateTimeOffset.UtcNow.AddDays(30),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Only ValidFrom without ValidTo — should pass
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_OnlyValidFrom_ReturnsValid()
    {
        ReferenceDataUpdateRequest request = ValidRequest() with
        {
            ValidFrom = DateTimeOffset.UtcNow,
            ValidTo = null,
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
        ReferenceDataUpdateRequest request = ValidRequest() with
        {
            ValidFrom = now,
            ValidTo = now,
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }
}
