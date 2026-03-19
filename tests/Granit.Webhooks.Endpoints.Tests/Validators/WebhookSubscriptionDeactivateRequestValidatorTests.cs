using FluentValidation.TestHelper;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Validators;

public sealed class WebhookSubscriptionDeactivateRequestValidatorTests
{
    private readonly WebhookSubscriptionDeactivateRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidReason_ShouldPass()
    {
        var request = new WebhookSubscriptionDeactivateRequest("No longer needed");

        TestValidationResult<WebhookSubscriptionDeactivateRequest> result = _validator.TestValidate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyReason_ShouldFail(string? reason)
    {
        var request = new WebhookSubscriptionDeactivateRequest(reason!);

        TestValidationResult<WebhookSubscriptionDeactivateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Validate_ReasonTooLong_ShouldFail()
    {
        string longReason = new string('a', 1001);
        var request = new WebhookSubscriptionDeactivateRequest(longReason);

        TestValidationResult<WebhookSubscriptionDeactivateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}
