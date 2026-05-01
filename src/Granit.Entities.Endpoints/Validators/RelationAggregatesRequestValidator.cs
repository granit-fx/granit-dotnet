using FluentValidation;
using Granit.Entities.Endpoints.Dtos;

namespace Granit.Entities.Endpoints.Validators;

internal sealed class RelationAggregatesRequestValidator : AbstractValidator<RelationAggregatesRequest>
{
    public RelationAggregatesRequestValidator()
    {
        RuleForEach(x => x.Relations)
            .NotEmpty()
            .MaximumLength(256)
            .When(x => x.Relations is not null);
    }
}
