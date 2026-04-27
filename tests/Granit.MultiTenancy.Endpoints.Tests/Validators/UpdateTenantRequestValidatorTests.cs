using FluentValidation.Results;
using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.MultiTenancy.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Validators;

public sealed class UpdateTenantRequestValidatorTests
{
    private readonly UpdateTenantRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_Succeeds()
    {
        UpdateTenantRequest request = new("Updated Name", "admin@acme.com", null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        UpdateTenantRequest request = new("", "admin@acme.com", null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        UpdateTenantRequest request = new(new string('A', 257), null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        UpdateTenantRequest request = new("Acme", "not-an-email", null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PartyEmail");
    }

    [Fact]
    public void NullEmail_Succeeds()
    {
        UpdateTenantRequest request = new("Acme", null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void JurisdictionTooLong_Fails()
    {
        UpdateTenantRequest request = new("Acme", null, new string('A', 17));

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Jurisdiction");
    }
}
