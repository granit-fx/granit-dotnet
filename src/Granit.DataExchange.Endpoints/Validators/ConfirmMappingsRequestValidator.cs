using FluentValidation;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.DataExchange.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="ConfirmMappingsRequest"/> body for confirming import column mappings.
/// </summary>
internal sealed class ConfirmMappingsRequestValidator : GranitValidator<ConfirmMappingsRequest>
{
    public ConfirmMappingsRequestValidator()
    {
        RuleFor(x => x.ConcurrencyStamp)
            .NotEmpty();

        RuleFor(x => x.Mappings)
            .NotEmpty()
            .Must(m => m.Any(mapping => mapping.TargetProperty is not null))
            .WithErrorCodeAndMessage("DataExchange:Validation:AtLeastOneMappingTarget");

        RuleForEach(x => x.Mappings).ChildRules(mapping =>
        {
            mapping.RuleFor(m => m.SourceColumn).NotEmpty().MaximumLength(500);
            mapping.RuleFor(m => m.TargetProperty).MaximumLength(500);
            mapping.RuleFor(m => m.Confidence).IsInEnum();
        });
    }
}
