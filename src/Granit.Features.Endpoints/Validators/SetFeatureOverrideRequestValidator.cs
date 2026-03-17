using FluentValidation;
using Granit.Features.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Features.Endpoints.Validators;

/// <summary>
/// Validates <see cref="SetFeatureOverrideRequest"/> (static constraints only).
/// Value-type–specific validation (toggle, numeric bounds, selection membership) is
/// performed in the endpoint handler using <c>IFeatureDefinitionStore</c>.
/// </summary>
internal sealed class SetFeatureOverrideRequestValidator : GranitValidator<SetFeatureOverrideRequest>
{
    internal const int MaxValueLength = 2000;

    public SetFeatureOverrideRequestValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty()
            .MaximumLength(MaxValueLength);
    }
}
