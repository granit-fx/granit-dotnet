using FluentValidation.Results;
using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.MultiTenancy.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Validators;

public sealed class UpdateTenantRequestValidatorTests
{
    private readonly UpdateTenantRequestValidator _validator = new();
    private const string AnyStamp = "00000000-0000-0000-0000-000000000000";

    [Fact]
    public void ValidRequest_Succeeds()
    {
        UpdateTenantRequest request = new(Name: "Updated Name", ContactEmail: "admin@acme.com", Jurisdiction: null, ConcurrencyStamp: AnyStamp);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        UpdateTenantRequest request = new(Name: "", ContactEmail: "admin@acme.com", Jurisdiction: null, ConcurrencyStamp: AnyStamp);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        UpdateTenantRequest request = new(Name: new string('A', 257), ContactEmail: null, Jurisdiction: null, ConcurrencyStamp: AnyStamp);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        UpdateTenantRequest request = new(Name: "Acme", ContactEmail: "not-an-email", Jurisdiction: null, ConcurrencyStamp: AnyStamp);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public void NullEmail_Succeeds()
    {
        UpdateTenantRequest request = new(Name: "Acme", ContactEmail: null, Jurisdiction: null, ConcurrencyStamp: AnyStamp);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void JurisdictionTooLong_Fails()
    {
        UpdateTenantRequest request = new(Name: "Acme", ContactEmail: null, Jurisdiction: new string('A', 17), ConcurrencyStamp: AnyStamp);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Jurisdiction");
    }
}
