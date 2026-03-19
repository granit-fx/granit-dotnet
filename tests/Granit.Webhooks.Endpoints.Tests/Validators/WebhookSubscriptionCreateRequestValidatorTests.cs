using FluentValidation.TestHelper;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Validators;

public sealed class WebhookSubscriptionCreateRequestValidatorTests
{
    private readonly WebhookSubscriptionCreateRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_ShouldPass()
    {
        var request = new WebhookSubscriptionCreateRequest("https://example.com/webhook", "order.created");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyTargetUrl_ShouldFail(string? targetUrl)
    {
        var request = new WebhookSubscriptionCreateRequest(targetUrl!, "order.created");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TargetUrl);
    }

    [Fact]
    public void Validate_TargetUrlTooLong_ShouldFail()
    {
        string longUrl = $"https://example.com/{new string('a', 2048)}";
        var request = new WebhookSubscriptionCreateRequest(longUrl, "order.created");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TargetUrl);
    }

    [Fact]
    public void Validate_HttpUrl_ShouldFail()
    {
        var request = new WebhookSubscriptionCreateRequest("http://example.com/webhook", "order.created");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TargetUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyEventType_ShouldFail(string? eventType)
    {
        var request = new WebhookSubscriptionCreateRequest("https://example.com/webhook", eventType!);

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventType);
    }

    [Fact]
    public void Validate_EventTypeTooLong_ShouldFail()
    {
        string longEventType = new string('a', 201);
        var request = new WebhookSubscriptionCreateRequest("https://example.com/webhook", longEventType);

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventType);
    }
}
