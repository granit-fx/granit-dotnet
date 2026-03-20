using FluentValidation;
using Granit.Validation.Endpoints.Dtos;

namespace Granit.Validation.Endpoints.Validators;

/// <summary>
/// Validates a <see cref="ValidationFieldValidateBatchRequest"/>.
/// </summary>
internal sealed class ValidationFieldValidateBatchRequestValidator
    : AbstractValidator<ValidationFieldValidateBatchRequest>
{
    public ValidationFieldValidateBatchRequestValidator()
    {
        RuleFor(x => x.Fields)
            .NotEmpty()
            .Must(fields => fields.Count <= 20)
            .WithMessage("A maximum of 20 fields can be validated in a single batch request.");

        RuleForEach(x => x.Fields)
            .SetValidator(new ValidationFieldValidateRequestValidator());
    }
}
