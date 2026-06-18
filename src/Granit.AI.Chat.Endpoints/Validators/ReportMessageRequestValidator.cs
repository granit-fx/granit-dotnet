using FluentValidation;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.AI.Chat.Endpoints.Validators;

/// <summary>Validates <see cref="ReportMessageRequest"/>.</summary>
internal sealed class ReportMessageRequestValidator : GranitValidator<ReportMessageRequest>
{
    public ReportMessageRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(MessageReport.MaxReasonLength);

        RuleFor(x => x.Category)
            .IsInEnum()
            .When(x => x.Category is not null);
    }
}
