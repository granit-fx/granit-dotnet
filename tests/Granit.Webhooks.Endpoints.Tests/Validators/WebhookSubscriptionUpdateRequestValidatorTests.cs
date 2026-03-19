using FluentValidation.TestHelper;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Validators;

public sealed class WebhookSubscriptionUpdateRequestValidatorTests
{
    private readonly WebhookSubscriptionUpdateRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidHttpsUrl_ShouldPass()
    {
        var request = new WebhookSubscriptionUpdateRequest("https://example.com/webhook");

        TestValidationResult<WebhookSubscriptionUpdateRequest> result = _validator.TestValidate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyTargetUrl_ShouldFail(string? targetUrl)
    {
        var request = new WebhookSubscriptionUpdateRequest(targetUrl!);

        TestValidationResult<WebhookSubscriptionUpdateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TargetUrl);
    }

    [Fact]
    public void Validate_HttpUrl_ShouldFail()
    {
        var request = new WebhookSubscriptionUpdateRequest("http://example.com/webhook");

        TestValidationResult<WebhookSubscriptionUpdateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TargetUrl);
    }
}
