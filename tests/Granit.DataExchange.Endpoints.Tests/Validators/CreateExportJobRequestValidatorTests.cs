using FluentValidation.Results;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Validators;

public sealed class CreateExportJobRequestValidatorTests
{
    private readonly CreateExportJobRequestValidator _validator = new();

    private static CreateExportJobRequest ValidRequest() =>
        new("Acme.PatientExport", "xlsx", null, false, null, null, null, null);

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        CreateExportJobRequest request = ValidRequest();

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyDefinitionName_Fails()
    {
        CreateExportJobRequest request = new("", "xlsx", null, false, null, null, null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateExportJobRequest.DefinitionName));
    }

    [Fact]
    public void Validate_EmptyFormat_Fails()
    {
        CreateExportJobRequest request = new("Acme.Export", "", null, false, null, null, null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateExportJobRequest.Format));
    }

    [Fact]
    public void Validate_BothFieldsEmpty_FailsWithTwoErrors()
    {
        CreateExportJobRequest request = new("", "", null, false, null, null, null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Validate_WithSelectedFields_ReturnsValid()
    {
        CreateExportJobRequest request = new(
            "Acme.Export", "csv", ["Name", "Email"], true, "-name", null, null, "search");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
