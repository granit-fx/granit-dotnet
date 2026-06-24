using FluentValidation;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Timeline.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AnchorTimelineEntryRequest"/> before processing.
/// Auto-discovered by <c>GranitValidationModule</c>.
/// </summary>
internal sealed class AnchorTimelineEntryRequestValidator : AbstractValidator<AnchorTimelineEntryRequest>
{
    public AnchorTimelineEntryRequestValidator()
    {
        RuleFor(x => x.SourceKey)
            .NotEmpty()
            .Must(TimelineSourceKeys.IsValid)
            .WithErrorCodeAndMessage("Timeline:Validation:InvalidSourceKey");
        RuleFor(x => x.SourceId).NotEmpty().MaximumLength(128);
    }
}
