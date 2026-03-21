using FluentValidation.Results;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests.Validators;

public sealed class SaveTemplateCategoryRequestValidatorTests
{
    private readonly SaveTemplateCategoryRequestValidator _validator = new();

    // -------------------------------------------------------------------------
    // Valid requests
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        SaveTemplateCategoryRequest request = new("Invoices", "Invoice templates", "receipt", 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NullOptionalFields_ReturnsValid()
    {
        SaveTemplateCategoryRequest request = new("Invoices", null, null, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Name — validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        SaveTemplateCategoryRequest request = new("", null, null, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveTemplateCategoryRequest.Name));
    }

    [Fact]
    public void Validate_NameExceeds200Chars_Fails()
    {
        string longName = new('x', 201);
        SaveTemplateCategoryRequest request = new(longName, null, null, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveTemplateCategoryRequest.Name));
    }

    [Fact]
    public void Validate_NameExactly200Chars_ReturnsValid()
    {
        string name = new('x', 200);
        SaveTemplateCategoryRequest request = new(name, null, null, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Description — validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_DescriptionExceeds500Chars_Fails()
    {
        string longDesc = new('x', 501);
        SaveTemplateCategoryRequest request = new("Name", longDesc, null, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(SaveTemplateCategoryRequest.Description));
    }

    [Fact]
    public void Validate_DescriptionExactly500Chars_ReturnsValid()
    {
        string desc = new('x', 500);
        SaveTemplateCategoryRequest request = new("Name", desc, null, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Icon — validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_IconExceeds100Chars_Fails()
    {
        string longIcon = new('x', 101);
        SaveTemplateCategoryRequest request = new("Name", null, longIcon, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(SaveTemplateCategoryRequest.Icon));
    }

    [Fact]
    public void Validate_IconExactly100Chars_ReturnsValid()
    {
        string icon = new('x', 100);
        SaveTemplateCategoryRequest request = new("Name", null, icon, 0);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
