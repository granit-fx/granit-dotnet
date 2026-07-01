using Granit.Notifications.MobilePush.Endpoints.Dtos;
using Granit.Notifications.MobilePush.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Endpoints.Tests.Validators;

public sealed class MobilePushTokenRegisterRequestValidatorTests
{
    private readonly MobilePushTokenRegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_ReturnsValid() =>
        _validator.Validate(new MobilePushTokenRegisterRequest
        {
            DeviceToken = "fcm-token-abc",
            Platform = MobilePlatform.Android,
        }).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyDeviceToken_Fails(string? deviceToken) =>
        _validator.Validate(new MobilePushTokenRegisterRequest
        {
            DeviceToken = deviceToken!,
            Platform = MobilePlatform.Android,
        }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_DeviceTokenExceedsMaxLength_Fails() =>
        _validator.Validate(new MobilePushTokenRegisterRequest
        {
            DeviceToken = new string('x', MobilePushTokenRegisterRequestValidator.MaxDeviceTokenLength + 1),
            Platform = MobilePlatform.Android,
        }).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_UndefinedPlatform_Fails() =>
        _validator.Validate(new MobilePushTokenRegisterRequest
        {
            DeviceToken = "token",
            Platform = (MobilePlatform)999,
        }).IsValid.ShouldBeFalse();
}
