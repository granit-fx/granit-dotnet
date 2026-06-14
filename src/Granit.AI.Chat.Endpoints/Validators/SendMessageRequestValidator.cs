using FluentValidation;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.AI.Chat.Endpoints.Validators;

/// <summary>Validates <see cref="SendMessageRequest"/>.</summary>
internal sealed class SendMessageRequestValidator : GranitValidator<SendMessageRequest>
{
    /// <summary>Maximum message length.</summary>
    public const int MaxMessageLength = 16000;

    /// <summary>Maximum number of <c>@</c> mentions on a single turn.</summary>
    public const int MaxMentions = 25;

    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxMessageLength);

        RuleFor(x => x.Mentions)
            .Must(m => m is null || m.Count <= MaxMentions)
            .WithErrorCodeAndMessage("AIChat:Validation:TooManyMentions");

        RuleForEach(x => x.Mentions)
            .ChildRules(mention =>
            {
                mention.RuleFor(m => m.Type).NotEmpty();
                mention.RuleFor(m => m.Id).NotEmpty();
            })
            .When(x => x.Mentions is { Count: > 0 });
    }
}
