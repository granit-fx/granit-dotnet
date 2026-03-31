using FluentValidation;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.ReferenceData.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="ReferenceDataCreateRequest"/> body for reference data creation.
/// </summary>
internal sealed class ReferenceDataCreateRequestValidator : GranitValidator<ReferenceDataCreateRequest>
{
    public ReferenceDataCreateRequestValidator()
    {
        Include(new ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>());

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(ReferenceDataMutableFieldsValidator<ReferenceDataCreateRequest>.MaxCodeLength)
            .Matches(@"^[A-Za-z0-9_.\-]+$")
            .WithErrorCodeAndMessage("Granit:Validation:CodeInvalidFormat");
    }
}
