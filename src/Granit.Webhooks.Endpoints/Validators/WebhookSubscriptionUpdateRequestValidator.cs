using FluentValidation;
using Granit.Validation;
using Granit.Webhooks.Endpoints.Dtos;

namespace Granit.Webhooks.Endpoints.Validators;

internal sealed class WebhookSubscriptionUpdateRequestValidator : GranitValidator<WebhookSubscriptionUpdateRequest>
{
    public WebhookSubscriptionUpdateRequestValidator()
    {
        RuleFor(x => x.TargetUrl).IsValidWebhookTargetUrl();
    }
}
