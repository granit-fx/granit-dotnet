using FluentValidation;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.AI.Chat.Endpoints.Validators;

/// <summary>Validates <see cref="SendMessageRequest"/>.</summary>
internal sealed class SendMessageRequestValidator : GranitValidator<SendMessageRequest>
{
    /// <summary>Maximum message length.</summary>
    public const int MaxMessageLength = 16000;

    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxMessageLength);
    }
}
