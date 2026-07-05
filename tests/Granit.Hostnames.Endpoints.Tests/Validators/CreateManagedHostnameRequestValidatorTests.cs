using FluentValidation.Results;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Hostnames.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Endpoints.Tests.Validators;

public sealed class CreateManagedHostnameRequestValidatorTests
{
    private static readonly CreateManagedHostnameRequestValidator Validator = new();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ValidationResult Validate(CreateManagedHostnameRequest request) =>
        Validator.Validate(request);

    [Theory]
    [InlineData("acme.com")]
    [InlineData("www.shop.acme.com")]
    [InlineData("sub.domain.example.org")]
    public void ValidRequest_Passes(string host)
    {
        ValidationResult result = Validate(new(host, "cms.site", OwnerId));
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyHost_Fails(string host)
    {
        ValidationResult result = Validate(new(host, "cms.site", OwnerId));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateManagedHostnameRequest.Host));
    }

    [Fact]
    public void HostExceedingMaxLength_Fails()
    {
        string tooLong = $"{new string('a', 127)}.{new string('b', 126)}";
        tooLong.Length.ShouldBeGreaterThan(CreateManagedHostnameRequestValidator.MaxHostLength);

        ValidationResult result = Validate(new(tooLong, "cms.site", OwnerId));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateManagedHostnameRequest.Host));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOwnerType_Fails(string ownerType)
    {
        ValidationResult result = Validate(new("acme.com", ownerType, OwnerId));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateManagedHostnameRequest.OwnerType));
    }

    [Fact]
    public void OwnerTypeExceedingMaxLength_Fails()
    {
        string tooLong = new string('x', CreateManagedHostnameRequestValidator.MaxOwnerTypeLength + 1);

        ValidationResult result = Validate(new("acme.com", tooLong, OwnerId));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateManagedHostnameRequest.OwnerType));
    }

    [Fact]
    public void EmptyOwnerId_Fails()
    {
        ValidationResult result = Validate(new("acme.com", "cms.site", Guid.Empty));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateManagedHostnameRequest.OwnerId));
    }
}
