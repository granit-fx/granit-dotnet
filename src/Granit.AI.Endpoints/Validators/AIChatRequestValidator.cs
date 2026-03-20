using FluentValidation;
using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Options;
using Microsoft.Extensions.Options;

namespace Granit.AI.Endpoints.Validators;

internal sealed class AIChatRequestValidator : AbstractValidator<AIChatRequest>
{
    public AIChatRequestValidator(IOptions<AIEndpointsOptions> options)
    {
        RuleFor(x => x.Messages)
            .NotEmpty()
            .Must(m => m.Count <= options.Value.MaxChatMessages)
            .WithMessage($"Maximum {options.Value.MaxChatMessages} messages allowed.");

        RuleForEach(x => x.Messages).ChildRules(m =>
        {
            m.RuleFor(x => x.Role)
                .NotEmpty()
                .Must(r => r is "user" or "assistant" or "system")
                .WithMessage("Role must be 'user', 'assistant', or 'system'.");

            m.RuleFor(x => x.Content)
                .NotEmpty()
                .MaximumLength(128_000);
        });
    }
}
