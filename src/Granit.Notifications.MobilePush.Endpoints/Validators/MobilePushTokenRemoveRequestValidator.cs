using FluentValidation;
using Granit.Notifications.MobilePush.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Notifications.MobilePush.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="MobilePushTokenRemoveRequest"/> body for mobile push token removal.
/// </summary>
internal sealed class MobilePushTokenRemoveRequestValidator : GranitValidator<MobilePushTokenRemoveRequest>
{
    public MobilePushTokenRemoveRequestValidator()
    {
        RuleFor(x => x.DeviceToken)
            .NotEmpty()
            .MaximumLength(MobilePushTokenRegisterRequestValidator.MaxDeviceTokenLength);
    }
}
