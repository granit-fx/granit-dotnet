using FluentValidation;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;

namespace Granit.Timeline.Endpoints.Validators;

/// <summary>
/// Validates <see cref="PostTimelineEntryRequest"/> before processing.
/// Auto-discovered by <c>GranitValidationModule</c>.
/// </summary>
internal sealed class PostTimelineEntryRequestValidator : AbstractValidator<PostTimelineEntryRequest>
{
    public PostTimelineEntryRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.EntryType)
            .IsInEnum()
            .NotEqual(TimelineEntryType.SystemLog);
    }
}
