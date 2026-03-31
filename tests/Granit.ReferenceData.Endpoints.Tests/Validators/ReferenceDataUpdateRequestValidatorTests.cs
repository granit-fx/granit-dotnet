using FluentValidation.Results;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Endpoints.Tests.Validators;

public sealed class ReferenceDataUpdateRequestValidatorTests
{
    private readonly ReferenceDataUpdateRequestValidator _validator = new();

    private static ReferenceDataUpdateRequest ValidRequest() =>
        new(LabelEn: "Belgium", LabelFr: "Belgique");

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
    // LabelEn (required)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyLabelEn_Fails(string? labelEn)
    {
        ReferenceDataUpdateRequest request = ValidRequest() with { LabelEn = labelEn! };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReferenceDataUpdateRequest.LabelEn));
    }

    [Fact]
    public void Validate_LabelEnExceedsMaxLength_Fails()
    {
        string longLabel = new('x', ReferenceDataMutableFieldsValidator<ReferenceDataUpdateRequest>.MaxLabelLength + 1);
        ReferenceDataUpdateRequest request = ValidRequest() with { LabelEn = longLabel };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // SortOrder
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_NegativeSortOrder_Fails()
    {
        ReferenceDataUpdateRequest request = ValidRequest() with { SortOrder = -1 };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReferenceDataUpdateRequest.SortOrder));
    }

    // -------------------------------------------------------------------------
    // ValidFrom / ValidTo
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidToBeforeValidFrom_Fails()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ReferenceDataUpdateRequest request = ValidRequest() with
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
        ReferenceDataUpdateRequest request = ValidRequest() with
        {
            ValidFrom = now,
            ValidTo = now.AddDays(30),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
