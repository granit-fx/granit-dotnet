using FluentValidation;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Notifications.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="WebPushSubscriptionRegisterRequest"/> body for browser Web Push
/// subscription registration.
/// </summary>
internal sealed class WebPushSubscriptionRegisterRequestValidator : GranitValidator<WebPushSubscriptionRegisterRequest>
{
    /// <summary>Maximum length for a push service endpoint URL.</summary>
    internal const int MaxEndpointLength = 2048;

    /// <summary>Maximum length for a Base64 URL-safe encryption key.</summary>
    internal const int MaxKeyLength = 512;

    public WebPushSubscriptionRegisterRequestValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .MaximumLength(MaxEndpointLength)
            .HttpsUrl();

        RuleFor(x => x.Keys)
            .NotNull()
            .ChildRules(keys =>
            {
                keys.RuleFor(k => k.P256dh)
                    .NotEmpty()
                    .MaximumLength(MaxKeyLength);

                keys.RuleFor(k => k.Auth)
                    .NotEmpty()
                    .MaximumLength(MaxKeyLength);
            });
    }
}
