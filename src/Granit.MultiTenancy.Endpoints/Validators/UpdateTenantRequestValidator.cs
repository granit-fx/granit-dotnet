using FluentValidation;
using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.MultiTenancy.Endpoints.Validators;

/// <summary>
/// Validates <see cref="UpdateTenantRequest"/>.
/// </summary>
internal sealed class UpdateTenantRequestValidator : GranitValidator<UpdateTenantRequest>
{
    internal const int MaxNameLength = 256;
    internal const int MaxEmailLength = 256;
    internal const int MaxJurisdictionLength = 16;

    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.ContactEmail)
            .MaximumLength(MaxEmailLength)
            .EmailAddress()
            .When(x => x.ContactEmail is not null);

        RuleFor(x => x.Jurisdiction)
            .MaximumLength(MaxJurisdictionLength)
            .When(x => x.Jurisdiction is not null);
    }
}
