using FluentValidation.TestHelper;
using Granit.Webhooks.Definitions;
using Granit.Webhooks.Endpoints.Dtos;
using Granit.Webhooks.Endpoints.Validators;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Validators;

public sealed class WebhookSubscriptionCreateRequestValidatorTests
{
    private readonly IWebhookEventTypeRegistry _registry;
    private readonly WebhookSubscriptionCreateRequestValidator _validator;

    public WebhookSubscriptionCreateRequestValidatorTests()
    {
        _registry = Substitute.For<IWebhookEventTypeRegistry>();
        _registry.Exists("order.created").Returns(true);
        _registry.Exists(Arg.Is<string>(s => s != "order.created")).Returns(false);
        _validator = new WebhookSubscriptionCreateRequestValidator(_registry);
    }

    // -------------------------------------------------------------------------
    // Valid request
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidRequest_ShouldPass()
    {
        var request = new WebhookSubscriptionCreateRequest("https://example.com/webhook", "order.created");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Target URL validation
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Event type validation
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Registry enforcement
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_KnownEventType_ShouldPass()
    {
        var request = new WebhookSubscriptionCreateRequest("https://example.com/webhook", "order.created");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.EventType);
    }

    [Fact]
    public void Validate_UnknownEventType_ShouldFail()
    {
        var request = new WebhookSubscriptionCreateRequest("https://example.com/webhook", "hack.event");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventType)
            .WithErrorCode("Validation:UnknownWebhookEventType");
    }

    [Fact]
    public void Validate_EmptyRegistry_ShouldRejectAllEventTypes()
    {
        IWebhookEventTypeRegistry emptyRegistry = Substitute.For<IWebhookEventTypeRegistry>();
        emptyRegistry.Exists(Arg.Any<string>()).Returns(false);
        var validator = new WebhookSubscriptionCreateRequestValidator(emptyRegistry);

        var request = new WebhookSubscriptionCreateRequest("https://example.com/webhook", "any.event");

        TestValidationResult<WebhookSubscriptionCreateRequest> result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventType)
            .WithErrorCode("Validation:UnknownWebhookEventType");
    }
}
