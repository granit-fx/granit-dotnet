using FluentValidation;
using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Options;
using Microsoft.Extensions.Options;

namespace Granit.AI.Endpoints.Validators;

internal sealed class AIEmbeddingRequestValidator : AbstractValidator<AIEmbeddingRequest>
{
    public AIEmbeddingRequestValidator(IOptions<AIEndpointsOptions> options)
    {
        RuleFor(x => x.Inputs)
            .NotEmpty()
            .Must(i => i.Count <= options.Value.MaxEmbeddingInputs)
            .WithMessage($"Maximum {options.Value.MaxEmbeddingInputs} inputs allowed.");

        RuleForEach(x => x.Inputs)
            .NotEmpty()
            .MaximumLength(32_000);
    }
}
