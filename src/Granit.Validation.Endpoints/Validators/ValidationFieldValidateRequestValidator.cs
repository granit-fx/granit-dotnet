using FluentValidation;
using Granit.Validation.Endpoints.Dtos;

namespace Granit.Validation.Endpoints.Validators;

/// <summary>
/// Validates a <see cref="ValidationFieldValidateRequest"/>.
/// </summary>
internal sealed class ValidationFieldValidateRequestValidator
    : AbstractValidator<ValidationFieldValidateRequest>
{
    public ValidationFieldValidateRequestValidator()
    {
        RuleFor(x => x.ErrorCode)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(@"^[A-Za-z0-9:._]+$");

        RuleFor(x => x.Value)
            .MaximumLength(500);
    }
}
