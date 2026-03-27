using FluentValidation.Results;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Endpoints.Validators;
using Granit.DataExchange.Import.Mapping;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Validators;

public sealed class ConfirmMappingsRequestValidatorTests
{
    private readonly ConfirmMappingsRequestValidator _validator = new();

    private static ImportColumnMapping ValidMapping(string source = "Email", string? target = "Email") =>
        new(SourceColumn: source, TargetProperty: target, Confidence: MappingConfidence.Manual);

    // -------------------------------------------------------------------------
    // Valid request
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        ConfirmMappingsRequest request = new([ValidMapping()]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_UnmappedColumn_ReturnsValid()
    {
        ConfirmMappingsRequest request = new([ValidMapping(), ValidMapping(source: "Phone", target: null)]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Mappings — empty collection
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyMappings_Fails()
    {
        ConfirmMappingsRequest request = new([]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ConfirmMappingsRequest.Mappings));
    }

    // -------------------------------------------------------------------------
    // Individual mapping — SourceColumn
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmptySourceColumn_Fails()
    {
        ImportColumnMapping mapping = new(SourceColumn: "", TargetProperty: "Email", Confidence: MappingConfidence.Manual);
        ConfirmMappingsRequest request = new([mapping]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Individual mapping — Confidence enum
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_InvalidConfidenceEnum_Fails()
    {
        ImportColumnMapping mapping = new(SourceColumn: "Email", TargetProperty: "Email", Confidence: (MappingConfidence)99);
        ConfirmMappingsRequest request = new([mapping]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(MappingConfidence.Manual)]
    [InlineData(MappingConfidence.Saved)]
    [InlineData(MappingConfidence.Exact)]
    [InlineData(MappingConfidence.Fuzzy)]
    [InlineData(MappingConfidence.Semantic)]
    public void Validate_AllValidConfidenceValues_ReturnsValid(MappingConfidence confidence)
    {
        ImportColumnMapping mapping = new(SourceColumn: "Col1", TargetProperty: "Prop1", Confidence: confidence);
        ConfirmMappingsRequest request = new([mapping]);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
