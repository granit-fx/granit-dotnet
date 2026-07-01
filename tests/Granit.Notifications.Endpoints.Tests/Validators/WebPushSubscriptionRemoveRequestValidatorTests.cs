using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests.Validators;

public sealed class WebPushSubscriptionRemoveRequestValidatorTests
{
    private readonly WebPushSubscriptionRemoveRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidEndpoint_ReturnsValid() =>
        _validator.Validate(new WebPushSubscriptionRemoveRequest
        {
            Endpoint = "https://fcm.googleapis.com/fcm/send/abc-123",
        }).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    public void Validate_InvalidEndpoint_Fails(string? endpoint) =>
        _validator.Validate(new WebPushSubscriptionRemoveRequest { Endpoint = endpoint! }).IsValid.ShouldBeFalse();
}
