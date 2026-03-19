using FluentValidation;
using Granit.Validation;
using Granit.Webhooks.Endpoints.Dtos;

namespace Granit.Webhooks.Endpoints.Validators;

internal sealed class WebhookSubscriptionDeactivateRequestValidator : GranitValidator<WebhookSubscriptionDeactivateRequest>
{
    public WebhookSubscriptionDeactivateRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
