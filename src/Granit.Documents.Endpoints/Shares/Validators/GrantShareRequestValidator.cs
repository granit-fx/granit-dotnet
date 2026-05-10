using FluentValidation;
using Granit.Documents.Endpoints.Shares.Dtos;

namespace Granit.Documents.Endpoints.Shares.Validators;

/// <summary>
/// Validator for <see cref="GrantShareRequest"/> — both folder and document grants share the same shape.
/// </summary>
internal sealed class GrantShareRequestValidator : AbstractValidator<GrantShareRequest>
{
    public GrantShareRequestValidator()
    {
        RuleFor(x => x.GranteeId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.GranteeType)
            .IsInEnum();

        RuleFor(x => x.Permission)
            .IsInEnum();
    }
}
