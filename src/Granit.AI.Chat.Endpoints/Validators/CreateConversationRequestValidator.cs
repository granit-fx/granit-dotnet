using FluentValidation;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.AI.Chat.Endpoints.Validators;

/// <summary>Validates <see cref="CreateConversationRequest"/>.</summary>
internal sealed class CreateConversationRequestValidator : GranitValidator<CreateConversationRequest>
{
    /// <summary>Maximum conversation title length.</summary>
    public const int MaxTitleLength = 500;

    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(MaxTitleLength);
    }
}
