using FluentValidation;
using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Validation.Endpoints.Validators;

/// <summary>
/// Validates a <see cref="ValidationFieldValidateBatchRequest"/>.
/// </summary>
internal sealed class ValidationFieldValidateBatchRequestValidator
    : GranitValidator<ValidationFieldValidateBatchRequest>
{
    public ValidationFieldValidateBatchRequestValidator()
    {
        RuleFor(x => x.Fields)
            .NotEmpty()
            .Must(fields => fields.Count <= 20)
            .WithErrorCodeAndMessage("Validation:MaxBatchSize");

        RuleForEach(x => x.Fields)
            .SetValidator(new ValidationFieldValidateRequestValidator());
    }
}
