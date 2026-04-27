using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyMergeRequestValidator : GranitValidator<PartyMergeRequest>
{
    public PartyMergeRequestValidator()
    {
        RuleFor(x => x.LoserId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);

        // Each choice value must be either "Survivor" or "Loser" — case-sensitive for
        // wire-format predictability. Keys (field paths) are free-form: domain-level
        // validation lives in the orchestrator (an unknown field path becomes a no-op
        // fall-back to the default winner).
        RuleForEach(x => x.Choices)
            .Must(kv => kv.Value is "Survivor" or "Loser")
            .When(x => x.Choices is not null)
            .WithErrorCodeAndMessage("Granit:Validation:InvalidMergeChoice");
    }
}
