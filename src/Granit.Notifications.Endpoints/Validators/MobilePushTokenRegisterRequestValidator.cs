using FluentValidation;
using Granit.Notifications.Endpoints.Endpoints;
using Granit.Validation;

namespace Granit.Notifications.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="MobilePushTokenRegisterRequest"/> body for mobile push token registration.
/// </summary>
internal sealed class MobilePushTokenRegisterRequestValidator : GranitValidator<MobilePushTokenRegisterRequest>
{
    /// <summary>Maximum length for a device token (FCM ~163 chars, APNs ~64 hex chars).</summary>
    internal const int MaxDeviceTokenLength = 512;

    public MobilePushTokenRegisterRequestValidator()
    {
        RuleFor(x => x.DeviceToken)
            .NotEmpty()
            .MaximumLength(MaxDeviceTokenLength);

        RuleFor(x => x.Platform)
            .IsInEnum();
    }
}
