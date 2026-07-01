using FluentValidation.Results;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests.Validators;

public sealed class WebPushSubscriptionRegisterRequestValidatorTests
{
    private readonly WebPushSubscriptionRegisterRequestValidator _validator = new();

    private static WebPushSubscriptionRegisterRequest ValidRequest() => new()
    {
        Endpoint = "https://fcm.googleapis.com/fcm/send/abc-123",
        ExpirationTime = null,
        Keys = new WebPushSubscriptionKeys { P256dh = "BFooBarKey", Auth = "AuthSecret" },
    };

    [Fact]
    public void Validate_ValidRequest_ReturnsValid() =>
        _validator.Validate(ValidRequest()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyEndpoint_Fails(string? endpoint)
    {
        WebPushSubscriptionRegisterRequest request = ValidRequest() with { Endpoint = endpoint! };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(WebPushSubscriptionRegisterRequest.Endpoint));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    [InlineData("fcm.googleapis.com/send")]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")] // push endpoints must be HTTPS (RFC 8030)
    public void Validate_NonHttpsEndpoint_Fails(string endpoint)
    {
        WebPushSubscriptionRegisterRequest request = ValidRequest() with { Endpoint = endpoint };

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_EndpointExceedsMaxLength_Fails()
    {
        string longEndpoint = "https://push.example/" + new string('x', WebPushSubscriptionRegisterRequestValidator.MaxEndpointLength);
        WebPushSubscriptionRegisterRequest request = ValidRequest() with { Endpoint = longEndpoint };

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "auth")]
    [InlineData("p256dh", "")]
    public void Validate_EmptyKey_Fails(string p256dh, string auth)
    {
        WebPushSubscriptionRegisterRequest request = ValidRequest() with
        {
            Keys = new WebPushSubscriptionKeys { P256dh = p256dh, Auth = auth },
        };

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }
}
