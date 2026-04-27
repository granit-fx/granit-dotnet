using FluentValidation;
using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.MultiTenancy.Endpoints.Validators;

/// <summary>
/// Validates <see cref="CreateTenantRequest"/>.
/// </summary>
internal sealed class CreateTenantRequestValidator : GranitValidator<CreateTenantRequest>
{
    internal const int MaxNameLength = 256;
    internal const int MaxIdentifierLength = 64;
    internal const int MaxEmailLength = 256;
    internal const int MaxJurisdictionLength = 16;
    internal const string IdentifierPattern = @"^[a-z0-9-]+$";

    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Identifier)
            .NotEmpty()
            .MaximumLength(MaxIdentifierLength)
            .Matches(IdentifierPattern);

        RuleFor(x => x.PartyEmail)
            .MaximumLength(MaxEmailLength)
            .EmailAddress()
            .When(x => x.PartyEmail is not null);

        RuleFor(x => x.Jurisdiction)
            .MaximumLength(MaxJurisdictionLength)
            .When(x => x.Jurisdiction is not null);
    }
}
