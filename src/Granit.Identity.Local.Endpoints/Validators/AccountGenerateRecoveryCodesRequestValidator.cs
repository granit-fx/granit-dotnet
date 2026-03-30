using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountGenerateRecoveryCodesRequest"/>.
/// </summary>
internal sealed class AccountGenerateRecoveryCodesRequestValidator : GranitValidator<AccountGenerateRecoveryCodesRequest>
{
    public AccountGenerateRecoveryCodesRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
