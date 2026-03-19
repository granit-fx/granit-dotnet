using FluentValidation;
using Granit.Validation;
using Granit.Webhooks.Endpoints.Dtos;

namespace Granit.Webhooks.Endpoints.Validators;

internal sealed class WebhookSubscriptionCreateRequestValidator : GranitValidator<WebhookSubscriptionCreateRequest>
{
    public WebhookSubscriptionCreateRequestValidator()
    {
        RuleFor(x => x.TargetUrl).IsValidWebhookTargetUrl();
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(200);
    }
}
