using FluentValidation.Results;
using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.MultiTenancy.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Validators;

public sealed class CreateTenantRequestValidatorTests
{
    private readonly CreateTenantRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_Succeeds()
    {
        CreateTenantRequest request = new("Acme Corp", "acme-corp", "admin@acme.com", null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        CreateTenantRequest request = new("", "acme", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        CreateTenantRequest request = new(new string('A', 257), "acme", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void EmptyIdentifier_Fails()
    {
        CreateTenantRequest request = new("Acme", "", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Identifier");
    }

    [Fact]
    public void IdentifierTooLong_Fails()
    {
        CreateTenantRequest request = new("Acme", new string('a', 65), null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Identifier");
    }

    [Fact]
    public void IdentifierWithUpperCase_Fails()
    {
        CreateTenantRequest request = new("Acme", "Acme-Corp", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Identifier");
    }

    [Fact]
    public void IdentifierWithSpecialChars_Fails()
    {
        CreateTenantRequest request = new("Acme", "acme_corp!", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Identifier");
    }

    [Fact]
    public void ValidIdentifier_LowercaseWithHyphens_Succeeds()
    {
        CreateTenantRequest request = new("Acme", "acme-corp-123", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        CreateTenantRequest request = new("Acme", "acme", "not-an-email", null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PartyEmail");
    }

    [Fact]
    public void NullEmail_Succeeds()
    {
        CreateTenantRequest request = new("Acme", "acme", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void JurisdictionTooLong_Fails()
    {
        CreateTenantRequest request = new("Acme", "acme", null, new string('A', 17));

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Jurisdiction");
    }
}
