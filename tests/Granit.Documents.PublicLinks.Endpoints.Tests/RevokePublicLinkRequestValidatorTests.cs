using FluentValidation.Results;
using Granit.Documents.PublicLinks.Endpoints.Dtos;
using Granit.Documents.PublicLinks.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.Endpoints.Tests;

public sealed class RevokePublicLinkRequestValidatorTests
{
    private static readonly RevokePublicLinkRequestValidator _validator = new();

    [Fact]
    public void Null_reason_should_pass()
    {
        ValidationResult result = _validator.Validate(new RevokePublicLinkRequest(null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Short_reason_should_pass()
    {
        ValidationResult result = _validator.Validate(new RevokePublicLinkRequest("Operator request."));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Reason_at_max_length_should_pass()
    {
        string reason = new('a', RevokePublicLinkRequest.MaxReasonLength);
        ValidationResult result = _validator.Validate(new RevokePublicLinkRequest(reason));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Reason_above_max_length_should_fail()
    {
        string reason = new('a', RevokePublicLinkRequest.MaxReasonLength + 1);
        ValidationResult result = _validator.Validate(new RevokePublicLinkRequest(reason));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(RevokePublicLinkRequest.Reason));
    }
}
