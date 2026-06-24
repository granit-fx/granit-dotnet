using FluentValidation;
using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Timeline.Endpoints.Validators;

/// <summary>
/// Validates <see cref="PostTimelineEntryRequest"/> before processing.
/// Auto-discovered by <c>GranitValidationModule</c>.
/// </summary>
internal sealed class PostTimelineEntryRequestValidator : AbstractValidator<PostTimelineEntryRequest>
{
    public PostTimelineEntryRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(65_536);
        RuleFor(x => x.EntryType)
            .IsInEnum()
            .NotEqual(TimelineEntryType.SystemLog);
        RuleFor(x => x.AttachmentBlobIds)
            .Must(ids => ids is null || ids.Count <= 20)
            .WithErrorCodeAndMessage("Timeline:Validation:TooManyAttachments");
    }
}
