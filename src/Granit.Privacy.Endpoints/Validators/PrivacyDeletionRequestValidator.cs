using FluentValidation;
using Granit.Privacy.Endpoints.Dtos;

namespace Granit.Privacy.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="PrivacyDeletionRequest"/>.
/// </summary>
internal sealed class PrivacyDeletionRequestValidator : AbstractValidator<PrivacyDeletionRequest>
{
    public PrivacyDeletionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(2000);
    }
}
