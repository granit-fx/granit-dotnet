using FluentValidation;
using Granit.Notifications.WebPush.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Notifications.WebPush.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="WebPushSubscriptionRemoveRequest"/> body for browser Web Push
/// subscription removal.
/// </summary>
internal sealed class WebPushSubscriptionRemoveRequestValidator : GranitValidator<WebPushSubscriptionRemoveRequest>
{
    public WebPushSubscriptionRemoveRequestValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .HttpsUrl();
    }
}
