using FluentValidation;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.AI.Chat.Endpoints.Validators;

/// <summary>Validates <see cref="RenameConversationRequest"/>.</summary>
internal sealed class RenameConversationRequestValidator : GranitValidator<RenameConversationRequest>
{
    public RenameConversationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(CreateConversationRequestValidator.MaxTitleLength);
    }
}
