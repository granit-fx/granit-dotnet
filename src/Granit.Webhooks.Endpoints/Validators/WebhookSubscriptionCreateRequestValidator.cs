using FluentValidation;
using Granit.Validation;
using Granit.Validation.Extensions;
using Granit.Webhooks.Definitions;
using Granit.Webhooks.Endpoints.Dtos;

namespace Granit.Webhooks.Endpoints.Validators;

internal sealed class WebhookSubscriptionCreateRequestValidator : GranitValidator<WebhookSubscriptionCreateRequest>
{
    public WebhookSubscriptionCreateRequestValidator(IWebhookEventTypeRegistry eventTypeRegistry)
    {
        RuleFor(x => x.TargetUrl).IsValidWebhookTargetUrl();
        RuleFor(x => x.EventType)
            .NotEmpty()
            .MaximumLength(200)
            .Must(eventTypeRegistry.Exists)
                .WithErrorCodeAndMessage("Validation:UnknownWebhookEventType");
    }
}
