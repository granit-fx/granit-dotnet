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
        UpdateTenantRequest request = new(Name: "Updated Name", ConcurrencyStamp: AnyStamp, ContactEmail: "admin@acme.com", Jurisdiction: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        UpdateTenantRequest request = new(Name: "", ConcurrencyStamp: AnyStamp, ContactEmail: "admin@acme.com", Jurisdiction: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        UpdateTenantRequest request = new(Name: new string('A', 257), ConcurrencyStamp: AnyStamp, ContactEmail: null, Jurisdiction: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        UpdateTenantRequest request = new(Name: "Acme", ConcurrencyStamp: AnyStamp, ContactEmail: "not-an-email", Jurisdiction: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public void NullEmail_Succeeds()
    {
        UpdateTenantRequest request = new(Name: "Acme", ConcurrencyStamp: AnyStamp, ContactEmail: null, Jurisdiction: null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void JurisdictionTooLong_Fails()
    {
        UpdateTenantRequest request = new(Name: "Acme", ConcurrencyStamp: AnyStamp, ContactEmail: null, Jurisdiction: new string('A', 17));

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Jurisdiction");
    }
}
