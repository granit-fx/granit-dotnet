using FluentValidation;
using Granit.Privacy.Endpoints.Dtos;

namespace Granit.Privacy.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="PrivacyAcceptAgreementRequest"/>.
/// </summary>
internal sealed class PrivacyAcceptAgreementRequestValidator : AbstractValidator<PrivacyAcceptAgreementRequest>
{
    public PrivacyAcceptAgreementRequestValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Version)
            .NotEmpty()
            .MaximumLength(50);
    }
}
