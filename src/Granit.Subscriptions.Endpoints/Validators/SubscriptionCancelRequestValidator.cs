using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="SubscriptionCancelRequest"/>.
/// </summary>
internal sealed class SubscriptionCancelRequestValidator : GranitValidator<SubscriptionCancelRequest>
{
    internal const int MaxReasonLength = 1000;

    public SubscriptionCancelRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(MaxReasonLength)
            .When(x => x.Reason is not null);
    }
}
