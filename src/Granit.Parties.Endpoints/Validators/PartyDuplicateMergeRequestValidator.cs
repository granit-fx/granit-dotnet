using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Parties.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="PartyDuplicateMergeRequest"/> — the body of
/// <c>POST /parties/duplicates/{id}/merge</c>. Mirrors
/// <see cref="PartyMergeRequestValidator"/>: <c>SurvivorId</c> required (the handler
/// resolves the loser from the candidate pair), <c>Reason</c> capped at 1000 chars,
/// every <c>Choices</c> value must be <c>"Survivor"</c> or <c>"Loser"</c>.
/// </summary>
internal sealed class PartyDuplicateMergeRequestValidator : GranitValidator<PartyDuplicateMergeRequest>
{
    public PartyDuplicateMergeRequestValidator()
    {
        RuleFor(x => x.SurvivorId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);

        RuleForEach(x => x.Choices)
            .Must(kv => kv.Value is "Survivor" or "Loser")
            .When(x => x.Choices is not null)
            .WithErrorCodeAndMessage("Granit:Validation:InvalidMergeChoice");
    }
}
