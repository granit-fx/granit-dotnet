using FluentValidation.Results;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests.Validators;

public sealed class ReferenceDataCreateRequestValidatorTests
{
    private readonly ReferenceDataCreateRequestValidator _validator = new();

    private static ReferenceDataCreateRequest ValidRequest() =>
        new(Code: "BE", LabelEn: "Belgium", LabelFr: "Belgique");

    // -------------------------------------------------------------------------
    // Valid request
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        ValidationResult result = _validator.Validate(ValidRequest());

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Code
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyCode_Fails(string? code)
    {
        ReferenceDataCreateRequest request = ValidRequest() with { Code = code! };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReferenceDataCreateRequest.Code));
    }

    [Fact]
    public void Validate_CodeExceedsMaxLength_Fails()
    {
        string longCode = new('X', ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>.MaxCodeLength + 1);
        ReferenceDataCreateRequest request = ValidRequest() with { Code = longCode };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReferenceDataCreateRequest.Code));
    }

    // -------------------------------------------------------------------------
    // LabelEn (required)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyLabelEn_Fails(string? labelEn)
    {
        ReferenceDataCreateRequest request = ValidRequest() with { LabelEn = labelEn! };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReferenceDataCreateRequest.LabelEn));
    }

    [Fact]
    public void Validate_LabelEnExceedsMaxLength_Fails()
    {
        string longLabel = new('x', ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>.MaxLabelLength + 1);
        ReferenceDataCreateRequest request = ValidRequest() with { LabelEn = longLabel };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Optional labels — MaxLength
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelFr))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelNl))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelDe))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelEs))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelIt))]
    [InlineData(nameof(ReferenceDataCreateRequest.LabelPt))]
    public void Validate_OptionalLabelExceedsMaxLength_Fails(string propertyName)
    {
        string longLabel = new('x', ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>.MaxLabelLength + 1);
        ReferenceDataCreateRequest request = propertyName switch
        {
            nameof(ReferenceDataCreateRequest.LabelFr) => ValidRequest() with { LabelFr = longLabel },
            nameof(ReferenceDataCreateRequest.LabelNl) => ValidRequest() with { LabelNl = longLabel },
            nameof(ReferenceDataCreateRequest.LabelDe) => ValidRequest() with { LabelDe = longLabel },
            nameof(ReferenceDataCreateRequest.LabelEs) => ValidRequest() with { LabelEs = longLabel },
            nameof(ReferenceDataCreateRequest.LabelIt) => ValidRequest() with { LabelIt = longLabel },
            nameof(ReferenceDataCreateRequest.LabelPt) => ValidRequest() with { LabelPt = longLabel },
            _ => throw new ArgumentException(propertyName),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == propertyName);
    }

    // -------------------------------------------------------------------------
    // SortOrder
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_NegativeSortOrder_Fails()
    {
        ReferenceDataCreateRequest request = ValidRequest() with { SortOrder = -1 };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReferenceDataCreateRequest.SortOrder));
    }

    // -------------------------------------------------------------------------
    // ValidFrom / ValidTo
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidToBeforeValidFrom_Fails()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ReferenceDataCreateRequest request = ValidRequest() with
        {
            ValidFrom = now,
            ValidTo = now.AddDays(-1),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ValidToAfterValidFrom_ReturnsValid()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ReferenceDataCreateRequest request = ValidRequest() with
        {
            ValidFrom = now,
            ValidTo = now.AddDays(30),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_OnlyValidFrom_ReturnsValid()
    {
        ReferenceDataCreateRequest request = ValidRequest() with
        {
            ValidFrom = DateTimeOffset.UtcNow,
            ValidTo = null,
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
