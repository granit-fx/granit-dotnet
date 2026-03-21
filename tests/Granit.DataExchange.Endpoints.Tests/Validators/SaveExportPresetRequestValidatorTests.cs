using FluentValidation.Results;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Validators;

public sealed class SaveExportPresetRequestValidatorTests
{
    private readonly SaveExportPresetRequestValidator _validator = new();

    private static SaveExportPresetRequest ValidRequest() =>
        new("Acme.Export", "Monthly", ["Name", "Email"], "xlsx", false);

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        SaveExportPresetRequest request = ValidRequest();

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyDefinitionName_Fails()
    {
        SaveExportPresetRequest request = new("", "Monthly", ["Name"], "xlsx", false);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveExportPresetRequest.DefinitionName));
    }

    [Fact]
    public void Validate_EmptyPresetName_Fails()
    {
        SaveExportPresetRequest request = new("Acme.Export", "", ["Name"], "xlsx", false);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveExportPresetRequest.PresetName));
    }

    [Fact]
    public void Validate_EmptySelectedFields_Fails()
    {
        SaveExportPresetRequest request = new("Acme.Export", "Monthly", [], "xlsx", false);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveExportPresetRequest.SelectedFields));
    }

    [Fact]
    public void Validate_EmptyFormat_Fails()
    {
        SaveExportPresetRequest request = new("Acme.Export", "Monthly", ["Name"], "", false);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveExportPresetRequest.Format));
    }

    [Fact]
    public void Validate_AllFieldsEmpty_FailsWithMultipleErrors()
    {
        SaveExportPresetRequest request = new("", "", [], "", false);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Validate_WithIncludeIdForImport_ReturnsValid()
    {
        SaveExportPresetRequest request = new("Acme.Export", "Preset", ["Id", "Name"], "csv", true);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
