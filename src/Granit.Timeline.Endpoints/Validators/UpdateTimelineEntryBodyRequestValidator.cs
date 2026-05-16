using FluentValidation;
using Granit.Timeline.Endpoints.Dtos;

namespace Granit.Timeline.Endpoints.Validators;

/// <summary>
/// Validates <see cref="UpdateTimelineEntryBodyRequest"/> before processing.
/// Auto-discovered by <c>GranitValidationModule</c>.
/// </summary>
internal sealed class UpdateTimelineEntryBodyRequestValidator : AbstractValidator<UpdateTimelineEntryBodyRequest>
{
    public UpdateTimelineEntryBodyRequestValidator() =>
        RuleFor(x => x.Body).NotEmpty().MaximumLength(65_536);
}
