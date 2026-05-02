using FluentValidation.Results;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.Endpoints.Dtos;
using Granit.Entities.Customization.Endpoints.Options;
using Granit.Entities.Customization.Endpoints.Validators;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Entities.Customization.Endpoints.Tests.Validators;

public sealed class EntityCustomizationRequestValidatorTests
{
    private static EntityCustomizationRequestValidator Sut(int maxDeltas = 100) =>
        new(MsOptions.Create(new EntitiesCustomizationEndpointsOptions { MaxDeltasPerRequest = maxDeltas }));

    [Fact]
    public void Empty_delta_list_is_valid()
    {
        ValidationResult result = Sut().Validate(new EntityCustomizationRequest([]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Well_formed_deltas_pass()
    {
        var request = new EntityCustomizationRequest(
        [
            new HideDelta("a"),
            new ReorderDelta("b", BeforeFieldName: "a", AfterFieldName: null),
            new RegroupDelta("c", "general"),
        ]);

        ValidationResult result = Sut().Validate(request);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Reorder_with_both_anchors_set_fails()
    {
        var request = new EntityCustomizationRequest(
        [
            new ReorderDelta("a", BeforeFieldName: "x", AfterFieldName: "y"),
        ]);

        ValidationResult result = Sut().Validate(request);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("BeforeFieldName"));
    }

    [Fact]
    public void Reorder_with_no_anchor_fails()
    {
        var request = new EntityCustomizationRequest(
        [
            new ReorderDelta("a", BeforeFieldName: null, AfterFieldName: null),
        ]);

        ValidationResult result = Sut().Validate(request);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Empty_field_name_fails()
    {
        var request = new EntityCustomizationRequest([new HideDelta("")]);
        ValidationResult result = Sut().Validate(request);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Empty_group_key_fails()
    {
        var request = new EntityCustomizationRequest([new RegroupDelta("a", "")]);
        ValidationResult result = Sut().Validate(request);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Over_max_deltas_fails()
    {
        var deltas = Enumerable.Range(0, 5).Select(i => (LayoutDelta)new HideDelta($"f{i}")).ToList();
        var request = new EntityCustomizationRequest(deltas);

        ValidationResult result = Sut(maxDeltas: 3).Validate(request);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains('3'));
    }
}
