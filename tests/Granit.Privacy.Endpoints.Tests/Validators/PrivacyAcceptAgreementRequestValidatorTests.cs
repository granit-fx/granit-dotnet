using FluentValidation.Results;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Validators;

public sealed class PrivacyAcceptAgreementRequestValidatorTests
{
    private readonly PrivacyAcceptAgreementRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        PrivacyAcceptAgreementRequest request = new("privacy-policy", "2.1.0");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "2.1.0")]
    [InlineData(null, "2.1.0")]
    public void Empty_DocumentId_Fails(string? documentId, string version)
    {
        PrivacyAcceptAgreementRequest request = new(documentId!, version);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DocumentId");
    }

    [Theory]
    [InlineData("privacy-policy", "")]
    [InlineData("privacy-policy", null)]
    public void Empty_Version_Fails(string documentId, string? version)
    {
        PrivacyAcceptAgreementRequest request = new(documentId, version!);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Version");
    }

    [Fact]
    public void DocumentId_ExceedingMaxLength_Fails()
    {
        PrivacyAcceptAgreementRequest request = new(new string('x', 201), "1.0");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DocumentId");
    }

    [Fact]
    public void Version_ExceedingMaxLength_Fails()
    {
        PrivacyAcceptAgreementRequest request = new("privacy-policy", new string('x', 51));

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Version");
    }
}
